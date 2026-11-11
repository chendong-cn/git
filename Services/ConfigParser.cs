using System.Text.RegularExpressions;
using NetConfigPro.Generators;
using NetConfigPro.Models;

namespace NetConfigPro.Services;

/// <summary>现网配置解析结果（识别到但未自动回填的内容放在 Notes 里提示人工核对）。</summary>
public sealed class ParseOutcome
{
    public bool Applied { get; set; }
    public string VendorText { get; set; } = "";
    public List<string> Notes { get; } = [];
    public int AppliedCount => Notes.Count(n => n.StartsWith("[已填]"));
    public int DetectedCount { get; set; }
}

/// <summary>
/// 现网配置（running-config 导出文本）解析：自动识别厂商并回填 ConfigSpec 核心字段。
/// 目标：让“拿现网改需求”替代“从零手填”；深度细节（ACL 规则/策略全文）以 Notes 提示人工在页签内核对。
/// </summary>
public static class ConfigParser
{
    public static ParseOutcome Parse(string text, ConfigSpec spec)
    {
        var outcome = new ParseOutcome();
        if (string.IsNullOrWhiteSpace(text)) return outcome;

        var vendor = VendorDetect.Detect(text);
        if (vendor is null)
        {
            outcome.Notes.Add("未能识别厂商特征——请确认粘贴的是设备 running-config / display current-configuration 导出文本");
            return outcome;
        }
        outcome.VendorText = EnumText.VendorName(vendor.Value);
        var lines = text.Split('\n').Select(l => l.Trim()).ToList();
        var ctx = ""; // 当前所在块（interface / vlan / ...）

        // ---- 主机名 / DNS / 默认网关 ----
        foreach (var line in lines)
        {
            var m = Regex.Match(line, @"^(sysname|hostname)\s+(\S+)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                spec.Basic.Hostname = m.Groups[2].Value;
                outcome.Notes.Add("[已填] 主机名 → " + m.Groups[2].Value);
            }

            m = Regex.Match(line, @"^dns\s+(domain\s+)?server\s+(\S+)", RegexOptions.IgnoreCase);
            if (!m.Success) m = Regex.Match(line, @"^ip\s+name-server\s+(\S+)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var ip = m.Groups[^1].Value;
                if (NetHelper.IsValidIp(ip) && spec.Basic.DnsServers == "223.5.5.5 114.114.114.114")
                {
                    spec.Basic.DnsServers = ip;
                    outcome.Notes.Add("[已填] DNS 服务器 → " + ip);
                }
            }

            m = Regex.Match(line, @"^ip\s+route(-static)?\s+0\.0\.0\.0\s+0(\.0\.0\.0)?\s+(\S+)", RegexOptions.IgnoreCase);
            if (!m.Success && line.StartsWith("route outside 0 0", StringComparison.OrdinalIgnoreCase))
                m = Regex.Match(line, @"route outside 0 0 (\S+)", RegexOptions.IgnoreCase);
            if (m.Success && spec.Basic.DefaultGateway == "192.168.100.254")
            {
                spec.Basic.DefaultGateway = m.Groups[^1].Value;
                outcome.Notes.Add("[已填] 默认网关 → " + m.Groups[^1].Value);
            }
        }

        // ---- 接口块扫描（管理地址）----
        var curVlan = 0;
        foreach (var line in lines)
        {
            var im = Regex.Match(line, @"^interface\s+(Vlanif|Vlan-interface|Vlan|VLAN)\s*(\d+)", RegexOptions.IgnoreCase);
            if (im.Success)
            {
                ctx = "vlanif";
                curVlan = int.TryParse(im.Groups[2].Value, out var v) ? v : 0;
                continue;
            }
            if (Regex.IsMatch(line, @"^interface\s+", RegexOptions.IgnoreCase)) { ctx = "other"; continue; }

            if (ctx == "vlanif")
            {
                var am = Regex.Match(line, @"^ip\s+address\s+(\d+\.\d+\.\d+\.\d+)\s+(\d+\.\d+\.\d+\.\d+)");
                if (am.Success && spec.Basic.MgmtIp == "192.168.100.1")
                {
                    spec.Basic.MgmtIp = am.Groups[1].Value;
                    spec.Basic.MgmtMask = am.Groups[2].Value;
                    spec.Basic.MgmtVlanId = curVlan;
                    outcome.Notes.Add($"[已填] 管理地址 → {am.Groups[1].Value}/{am.Groups[2].Value}" +
                        (curVlan > 0 ? $"（VLAN {curVlan}）" : ""));
                }
            }
        }

        // ---- VLAN 列表 ----
        var vlanIds = new SortedSet<int>();
        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, @"^interface\s+", RegexOptions.IgnoreCase)) { ctx = "other"; continue; }

            var vm = Regex.Match(line, @"^vlan\s+batch\s+(.+)$", RegexOptions.IgnoreCase);
            if (vm.Success)
            {
                // batch 值可逗号/空格分隔，支持 "10 to 20" 区间（华为/华三写法）
                var tokens = vm.Groups[1].Value
                    .Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                for (var ti = 0; ti < tokens.Length; ti++)
                {
                    if (ti + 2 < tokens.Length && tokens[ti + 1].Equals("to", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(tokens[ti], out var a) && int.TryParse(tokens[ti + 2], out var b))
                    {
                        for (var v = a; v <= b && v <= 4094; v++) vlanIds.Add(v);
                        ti += 2;
                    }
                    else if (int.TryParse(tokens[ti], out var s)) vlanIds.Add(s);
                }
                continue;
            }

            // vlan N（思科/锐捷 "vlan 10" 及华为/华三视图内 name）
            if (Regex.IsMatch(line, @"^vlan\s+\d+$", RegexOptions.IgnoreCase))
            {
                if (int.TryParse(line.Split(' ')[^1], out var v)) vlanIds.Add(v);
                ctx = "vlan";
            }
            else if (ctx == "vlan" && Regex.IsMatch(line, @"^(name|description)\s+(\S+)", RegexOptions.IgnoreCase) && vlanIds.Count > 0)
            {
                // 名称暂不逐个回填（表内容少，id 回填后用户在页内可补名）
            }
        }
        if (vlanIds.Count > 0)
        {
            foreach (var id in vlanIds)
                if (spec.Vlans.All(x => x.Id != id)) spec.Vlans.Add(new VlanItem { Id = id, Name = $"vlan{id}" });
            outcome.Notes.Add($"[已填] VLAN → {string.Join(", ", vlanIds)}");
        }

        // ---- 静态路由 ----
        var routeCount = 0;
        foreach (var line in lines)
        {
            var m = Regex.Match(line, @"^ip\s+route(-static)?\s+(\S+)\s+(\S+)\s+(\S+)", RegexOptions.IgnoreCase);
            if (m.Success && !(m.Groups[2].Value == "0.0.0.0" || m.Groups[3].Value == "0.0.0.0"))
            {
                var prefix = m.Groups[2].Value;
                var mask = m.Groups[3].Value == "0" ? "0.0.0.0" : m.Groups[3].Value;
                var hop = m.Groups[4].Value;
                // 防重：重复执行导入/同一路由多次出现时不叠加
                if (spec.StaticRoutes.All(r => !(r.Prefix == prefix && r.Mask == mask && r.NextHop == hop)))
                {
                    spec.StaticRoutes.Add(new StaticRouteItem { Prefix = prefix, Mask = mask, NextHop = hop });
                    routeCount++;
                }
            }
        }
        if (routeCount > 0) outcome.Notes.Add($"[已填] 静态路由 → {routeCount} 条");

        // ---- 只识别不回填的项（提示人工核对）----
        int aclNums = 0, natCount = 0, users = 0, ssids = 0, snmp = 0, pools = 0, ifaces = 0;
        var seenAcl = new HashSet<string>();
        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, @"^acl\s+number\s+(\d+)") || Regex.IsMatch(line, @"^(ip\s+)?access-list\s+(\d+)", RegexOptions.IgnoreCase)
                || Regex.IsMatch(line, @"^acl\s+(basic|advanced)\s+(\d+)", RegexOptions.IgnoreCase))
            {
                var num = Regex.Match(line, @"(\d{3,4})").Groups[1].Value;
                if (seenAcl.Add(num)) aclNums++;
            }
            else if (Regex.IsMatch(line, @"^nat\s+(server|static)", RegexOptions.IgnoreCase)
                     || Regex.IsMatch(line, @"^ip\s+nat\s+inside\s+source\s+static", RegexOptions.IgnoreCase)) natCount++;
            else if (Regex.IsMatch(line, @"^local-user\s+(\S+)", RegexOptions.IgnoreCase)
                     || Regex.IsMatch(line, @"^username\s+(\S+)\s+", RegexOptions.IgnoreCase)) users++;
            else if (Regex.IsMatch(line, @"^ssid\s+(\S+)", RegexOptions.IgnoreCase)) ssids++;
            else if (Regex.IsMatch(line, @"^(snmp-agent\s+)?community\s+(read|write)", RegexOptions.IgnoreCase)
                     || Regex.IsMatch(line, @"^snmp-server\s+community", RegexOptions.IgnoreCase)) snmp++;
            else if (Regex.IsMatch(line, @"^ip\s+pool\s+(\S+)", RegexOptions.IgnoreCase)
                     || Regex.IsMatch(line, @"^ip\s+dhcp\s+pool\s+(\S+)", RegexOptions.IgnoreCase)) pools++;
            else if (Regex.IsMatch(line, @"^interface\s+(GigabitEthernet|Ethernet|GigabitEthernet|FastEthernet)", RegexOptions.IgnoreCase)) ifaces++;
        }
        if (aclNums > 0) outcome.Notes.Add($"识别到 ACL 编号 {aclNums} 组——详细规则请在「ACL」页签按需核对重建");
        if (natCount > 0) outcome.Notes.Add($"识别到 NAT 条目 {natCount} 条——请到「端口映射/NAT」页签核对重建");
        if (users > 0) outcome.Notes.Add($"识别到本地账号 {users} 个——密码为密文不回填，请在「本地用户」页签重建账号与口令");
        if (ssids > 0) outcome.Notes.Add($"识别到 SSID {ssids} 个——请在「无线」页签核对重建");
        if (snmp > 0) outcome.Notes.Add($"识别到 SNMP 团体字/只读项——请到「SNMP」页签按需重建（团体字属敏感信息已跳过）");
        if (pools > 0) outcome.Notes.Add($"识别到 DHCP 地址池 {pools} 个——请在「DHCP」页签按需重建池参数");
        if (ifaces > 0) outcome.Notes.Add($"识别到物理接口 {ifaces} 个——接口明细请在「接口」页签按需补配");

        outcome.DetectedCount = aclNums + natCount + users + ssids + snmp + pools + ifaces;
        outcome.Applied = true;
        return outcome;
    }
}
