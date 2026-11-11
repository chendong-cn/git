using NetConfigPro.Models;

namespace NetConfigPro.Generators;

/// <summary>配置模块标识与中文名。</summary>
public static class Mod
{
    public const string Basic = "basic";
    public const string Vlan = "vlan";
    public const string Interface = "interface";
    public const string Stp = "stp";
    public const string Routing = "routing";
    public const string Acl = "acl";
    public const string Nat = "nat";
    public const string Firewall = "firewall";
    public const string Dhcp = "dhcp";
    public const string Wireless = "wireless";
    public const string Snmp = "snmp";
    public const string Ssh = "ssh";
    public const string Security = "security";
    // v4：路由拆分为独立协议模块
    public const string StatRoute = "statroute";
    public const string OspfR = "ospf";
    public const string BgpR = "bgp";
    public const string DynR = "dynr";
    // v2 扩展类别
    public const string Lag = "lag";
    public const string Vrrp = "vrrp";
    public const string DhcpSnoop = "dhcpsnoop";
    public const string Mirror = "mirror";
    public const string PortSec = "portsec";
    public const string Dot1x = "dot1x";
    public const string Lldp = "lldp";
    // v3 扩展类别（远程管理接入）
    public const string LocalUser = "localuser";
    public const string Telnet = "telnet";
    public const string WebAdmin = "webadmin";
    // v6 功能补齐
    public const string PortMap = "portmap";
    public const string StaticNat = "staticnat";
    public const string DhcpRelay = "dhcprelay";
    public const string DhcpBind = "dhcpbind";
    public const string ArpBind = "arpbind";
    public const string Storm = "storm";
    public const string IgmpSnoop = "igmp";
    // v11：策略路由 + QoS
    public const string Pbr = "pbr";
    public const string Qos = "qos";
    // v12：ACL 时间段 + BFD + PoE
    public const string TimeRange = "timerange";
    public const string Bfd = "bfd";
    public const string Poe = "poe";
    // v13：IPv6 / GRE / 语音 VLAN / 端口隔离
    public const string Ipv6 = "ipv6";
    public const string Gre = "gre";
    public const string Voice = "voice";
    public const string Isolate = "isolate";

    /// <summary>三层设备（路由器/防火墙/交换机）可用的模块范围。</summary>
    public static readonly DeviceType[] L3Scope = [DeviceType.Router, DeviceType.Firewall, DeviceType.Switch];
    /// <summary>二三层交换机的模块范围。</summary>
    public static readonly DeviceType[] SwitchScope = [DeviceType.Switch];

    public static readonly (string Key, string Name, string Tip, DeviceType[]? Scope)[] Catalog =
    [
        (Basic, "基础配置", "主机名、管理IP、默认网关、DNS、NTP、Syslog、上网方式", null),
        // VLAN 数据库与 VLANIF 仅路由器（需带二层交换口型号）/ 三层交换机提供；防火墙多网段走子接口
        (Vlan, "VLAN 配置", "创建 VLAN、命名、三层 VLANIF 接口地址", [DeviceType.Router, DeviceType.Switch]),
        (Interface, "接口配置", "接口描述、Access/Trunk/三层口、IP 地址", L3Scope),
        (Stp, "生成树", "STP/RSTP/MSTP 模式、根桥优先级、实例映射", SwitchScope),
        // v4：路由细分为独立协议模块，可多选同时生成（仅三层网络设备）
        (StatRoute, "静态路由", "目的网段/掩码/下一跳/优先级", L3Scope),
        (OspfR, "OSPF 路由", "进程号/Router-ID/网络宣告/区域类型/重分布", L3Scope),
        (BgpR, "BGP 路由", "本地 AS/Router-ID/BGP 邻居", L3Scope),
        (DynR, "RIP / EIGRP", "华三/华为/锐捷按 RIPv2，思科按 EIGRP 输出", L3Scope),
        (Acl, "ACL 策略", "标准/扩展访问控制列表", L3Scope),
        (Nat, "NAT 配置", "Easy IP 出网、静态端口映射", [DeviceType.Router, DeviceType.Firewall]),
        (Firewall, "防火墙策略", "安全区域划分与区域间访问策略", [DeviceType.Firewall]),
        // DHCP 服务由三层设备提供（二层交换请使用中继）；FAT AP 网关场景不单列 DHCP
        (Dhcp, "DHCP 服务", "地址池、网关、DNS、排除地址、租期", L3Scope),
        (Wireless, "无线配置", "SSID、WPA2/WPA3、VLAN 绑定、信道功率", [DeviceType.AccessPoint]),
        (Snmp, "SNMP 监控", "v2c/v3 团体字、Trap 主机、位置联系人", null),
        (Ssh, "SSH 远程", "SSH 服务端、认证超时与重试次数", null),
        (Security, "安全加固", "本地账号、超时退出、登录锁定、密码策略", null),
        // v2 扩展
        (Lag, "链路聚合", "Eth-Trunk / Port-Channel 聚合、LACP 或静态、成员接口", L3Scope),
        (Vrrp, "VRRP 冗余", "虚拟网关冗余、优先级、抢占", L3Scope),
        (DhcpSnoop, "DHCP Snooping", "防私接 DHCP、信任口、Option82", SwitchScope),
        (Mirror, "端口镜像", "本地镜像会话、源/目的端口、方向", SwitchScope),
        (PortSec, "端口安全", "MAC 数上限、Sticky、违规动作", SwitchScope),
        (Dot1x, "802.1X 认证", "RADIUS 服务器、端口控制方式", SwitchScope),
        (Lldp, "LLDP 发现", "链路层邻居发现（全局开关）", L3Scope),
        // v3 扩展（远程管理接入）
        (LocalUser, "本地用户", "多账号管理：用户名/密码/权限/接入方式", null),
        (Telnet, "Telnet 接入", "启用/停用 Telnet、VTY 来源限制", null),
        (WebAdmin, "Web 管理", "HTTP/HTTPS 图形化管理、端口、来源限制", null),
        // v6 功能补齐（NAT 类仅路由器/防火墙；交换机与 AP 不承担出口 NAT）
        (PortMap, "端口映射", "NAT Server：公网端口发布到内网服务器（多条）", [DeviceType.Router, DeviceType.Firewall]),
        (StaticNat, "NAT 静态一对一", "公网 IP 一对一映射内网服务器（多条）", [DeviceType.Router, DeviceType.Firewall]),
        (DhcpRelay, "DHCP 中继", "跨网段转发 DHCP 请求到服务器", L3Scope),
        (DhcpBind, "DHCP 静态绑定", "按 MAC 固定分配 IP（防私改 IP）", L3Scope),
        (ArpBind, "IP-MAC 绑定", "ARP 静态绑定防欺骗", L3Scope),
        (Storm, "风暴抑制", "广播/组播/未知单播限速", SwitchScope),
        (IgmpSnoop, "IGMP Snooping", "组播侦听（指定 VLAN）", SwitchScope),
        // v11：策略路由仅出口路由器/防火墙；QoS 限速对三层设备通用
        (Pbr, "策略路由 PBR", "按 ACL 匹配流量改走指定下一跳/出接口（多出口分流）", [DeviceType.Router, DeviceType.Firewall]),
        (Qos, "QoS 限速", "接口承诺速率限制（CIR，单位 kbps）", L3Scope),
        // v12：时间段供 ACL 引用；BFD 三层路由设备；PoE 仅交换机（供电下联 AP/摄像头）
        (TimeRange, "ACL 时间段", "定义生效时间段（每天/工作日/周末）供 ACL 规则引用", L3Scope),
        (Bfd, "BFD 检测", "毫秒级故障检测，联动静态/OSPF/BGP（需对端配合）", L3Scope),
        (Poe, "PoE 供电", "交换机 PoE 供电开关、优先级（为 AP/摄像头供电）", SwitchScope),
        // v13：IPv6 / GRE / 语音 VLAN / 端口隔离
        (Ipv6, "IPv6 基础", "三层接口 IPv6 地址与前缀、ND 路由通告（实验/过渡）", L3Scope),
        (Gre, "GRE 隧道", "站点间内网互通（轻量通道，不加密，需公网可达）", L3Scope),
        (Voice, "语音 VLAN", "IP 话机与电脑共口：数据/语音分流（仅交换机）", SwitchScope),
        (Isolate, "端口隔离", "同组端口二层互不通、均可上联（仅交换机）", SwitchScope)
    ];

    public static IEnumerable<(string Key, string Name, string Tip, DeviceType[]? Scope)> For(DeviceType dt)
        => Catalog.Where(m => m.Scope == null || m.Scope.Contains(dt));
}

public abstract class VendorGenerator
{
    public abstract Vendor Vendor { get; }

    /// <summary>注释符号：华为/华三/锐捷为 #，思科为 !。</summary>
    protected abstract string Comment { get; }

    /// <summary>进入配置视图的命令序列。</summary>
    protected abstract string[] EnterCommands { get; }

    /// <summary>当前设备类型下的示例接口名，用于表单默认值。</summary>
    public abstract string ExampleInterface(DeviceType dt);

    /// <summary>头部展示的操作系统名；思科防火墙（ASA）在此区分。</summary>
    protected virtual string OsNameFor(DeviceType dt) => EnumText.OsName(Vendor);

    public string Generate(ConfigSpec spec, DeviceType dt)
    {
        var b = new CfgBuilder(Comment);

        var bar = new string(Comment[0], 64);
        b.Line(bar);
        b.Line($"{Comment} 设备名称 : {spec.Basic.Hostname}");
        b.Line($"{Comment} 厂商平台 : {EnumText.VendorName(Vendor)}  系统: {OsNameFor(dt)}");
        b.Line($"{Comment} 设备类型 : {EnumText.DeviceName(dt)}");
        b.Line($"{Comment} 生成时间 : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        b.Line($"{Comment} 生成工具 : NetConfigPro v1.0");
        b.Line(bar);
        b.Blank();

        foreach (var cmd in EnterCommands) b.Line(cmd);
        b.Blank();

        // 只输出当前设备类型允许的模块（防止载入模板/切换设备后残留不适用的勾选）
        var allowed = Mod.For(dt).Select(m => m.Key).ToHashSet(StringComparer.Ordinal);
        var on = new HashSet<string>(spec.EnabledModules.Where(allowed.Contains), StringComparer.Ordinal);
        if (on.Contains(Mod.Basic)) GenBasic(b, spec, dt);
        if (on.Contains(Mod.Vlan)) GenVlans(b, spec, dt);
        if (on.Contains(Mod.Interface)) GenInterfaces(b, spec, dt);
        if (on.Contains(Mod.Stp)) GenStp(b, spec, dt);
        if (on.Contains(Mod.StatRoute) || on.Contains(Mod.OspfR) || on.Contains(Mod.BgpR) || on.Contains(Mod.DynR))
            GenRouting(b, spec, dt);
        if (on.Contains(Mod.Acl)) GenAcl(b, spec, dt);
        if (on.Contains(Mod.Nat)) GenNat(b, spec, dt);
        if (on.Contains(Mod.Firewall)) GenFirewall(b, spec, dt);
        if (on.Contains(Mod.Dhcp)) GenDhcp(b, spec, dt);
        if (on.Contains(Mod.Wireless)) GenWireless(b, spec, dt);
        if (on.Contains(Mod.Snmp)) GenSnmp(b, spec, dt);
        if (on.Contains(Mod.Ssh)) GenSsh(b, spec, dt);
        if (on.Contains(Mod.Security)) GenSecurity(b, spec, dt);

        if (on.Contains(Mod.Lag)) GenLag(b, spec, dt);
        if (on.Contains(Mod.Vrrp)) GenVrrp(b, spec, dt);
        if (on.Contains(Mod.DhcpSnoop)) GenDhcpSnoop(b, spec, dt);
        if (on.Contains(Mod.Mirror)) GenMirror(b, spec, dt);
        if (on.Contains(Mod.PortSec)) GenPortSec(b, spec, dt);
        if (on.Contains(Mod.Dot1x)) GenDot1x(b, spec, dt);
        if (on.Contains(Mod.Lldp)) GenLldp(b, spec, dt);

        if (on.Contains(Mod.LocalUser)) GenLocalUsers(b, spec, dt);
        if (on.Contains(Mod.Telnet)) GenTelnet(b, spec, dt);
        if (on.Contains(Mod.WebAdmin)) GenWeb(b, spec, dt);

        if (on.Contains(Mod.PortMap)) GenPortMaps(b, spec, dt);
        if (on.Contains(Mod.StaticNat)) GenStaticNats(b, spec, dt);
        if (on.Contains(Mod.DhcpRelay)) GenDhcpRelays(b, spec, dt);
        if (on.Contains(Mod.DhcpBind)) GenDhcpBinds(b, spec, dt);
        if (on.Contains(Mod.ArpBind)) GenArpBinds(b, spec, dt);
        if (on.Contains(Mod.Storm)) GenStorms(b, spec, dt);
        if (on.Contains(Mod.IgmpSnoop)) GenIgmp(b, spec, dt);

        if (on.Contains(Mod.Pbr)) GenPbr(b, spec, dt);
        if (on.Contains(Mod.Qos)) GenQos(b, spec, dt);

        if (on.Contains(Mod.TimeRange)) GenTimeRanges(b, spec, dt);
        if (on.Contains(Mod.Bfd)) GenBfd(b, spec, dt);
        if (on.Contains(Mod.Poe)) GenPoe(b, spec, dt);
        if (on.Contains(Mod.Ipv6)) GenIpv6(b, spec, dt);
        if (on.Contains(Mod.Gre)) GenGre(b, spec, dt);
        if (on.Contains(Mod.Voice)) GenVoice(b, spec, dt);
        if (on.Contains(Mod.Isolate)) GenIsolate(b, spec, dt);

        b.Blank();
        b.Section("配置结束");
        b.Line(ExitCommand);
        b.Note("保存配置命令：" + SaveCommand + "（建议确认无误后再执行）");
        b.Note("生成内容仅供参考，请结合实际网络环境与设备型号核对后使用。");

        return b.ToString();
    }

    protected abstract string ExitCommand { get; }
    protected abstract string SaveCommand { get; }

    protected abstract void GenBasic(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenVlans(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenInterfaces(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenStp(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenRouting(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenAcl(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenNat(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenFirewall(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenDhcp(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenWireless(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenSnmp(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenSsh(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenSecurity(CfgBuilder b, ConfigSpec s, DeviceType dt);

    // v2 扩展类别
    protected abstract void GenLag(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenVrrp(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenDhcpSnoop(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenMirror(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenPortSec(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenDot1x(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenLldp(CfgBuilder b, ConfigSpec s, DeviceType dt);

    // v3 扩展类别（远程管理接入）
    protected abstract void GenLocalUsers(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenTelnet(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenWeb(CfgBuilder b, ConfigSpec s, DeviceType dt);

    // v6 功能补齐
    protected abstract void GenPortMaps(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenStaticNats(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenDhcpRelays(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenDhcpBinds(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenArpBinds(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenStorms(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenIgmp(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenPbr(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenQos(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenTimeRanges(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenBfd(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenPoe(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenIpv6(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenGre(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenVoice(CfgBuilder b, ConfigSpec s, DeviceType dt);
    protected abstract void GenIsolate(CfgBuilder b, ConfigSpec s, DeviceType dt);

    protected static bool IsRouter(DeviceType dt) => dt == DeviceType.Router;
    protected static bool IsFirewall(DeviceType dt) => dt == DeviceType.Firewall;
    protected static bool IsSwitch(DeviceType dt) => dt == DeviceType.Switch;
    protected static bool IsAp(DeviceType dt) => dt == DeviceType.AccessPoint;

    /// <summary>把 any / host 形式的地址+反掩码转成思科风格写法。</summary>
    protected static string CiscoAddr(string ip, string wildcard)
    {
        var a = (ip ?? "").Trim();
        if (a.Equals("any", StringComparison.OrdinalIgnoreCase)) return "any";
        var w = (wildcard ?? "").Trim();
        if (w.Equals("0.0.0.0") || w.Length == 0) return $"host {a}";
        if (w.Equals("255.255.255.255")) return "any";
        return $"{a} {w}";
    }

    /// <summary>
    /// 把用户填写的 VLAN 三层接口名按本厂商归一：无论填 Vlanif10 / Vlan-interface10 / VLAN 10，
    /// 华为输出 Vlanif10、华三 Vlan-interface10、思科 Vlan10、锐捷 VLAN 10；非 VLAN 接口名原样返回。
    /// </summary>
    protected string NormVlanIface(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw ?? "";
        var m = System.Text.RegularExpressions.Regex.Match(raw.Trim(),
            @"^(?:Vlanif|Vlan-interface|VLAN)\s*(\d+)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success) return raw.Trim();
        var id = m.Groups[1].Value;
        return Vendor switch
        {
            Vendor.H3C => $"Vlan-interface{id}",
            Vendor.Cisco => $"Vlan{id}",
            Vendor.Ruijie => $"VLAN {id}",
            _ => $"Vlanif{id}"
        };
    }

    /// <summary>把 any 转成华为/华三/锐捷可用的 0.0.0.0 255.255.255.255。</summary>
    protected static string HwAddr(string ip, string wildcard)
    {
        var a = (ip ?? "any").Trim();
        if (a.Equals("any", StringComparison.OrdinalIgnoreCase)) return "0.0.0.0 255.255.255.255";
        var w = (wildcard ?? "").Trim();
        return w.Length == 0 ? $"{a} 0.0.0.0" : $"{a} {w}";
    }

    /// <summary>ACL 规则行的 time-range 引用后缀（未填写时间段返回空串）。</summary>
    protected static string TrSuffix(AclRuleItem rl) =>
        string.IsNullOrWhiteSpace(rl.TimeRange) ? "" : $" time-range {rl.TimeRange.Trim()}";

    /// <summary>从用户填写的接入方式串中筛出厂商支持的服务类型词，结果为空时用 fallback。</summary>
    protected static string FilterTokens(string? svc, string fallback, params string[] allowed)
    {
        var set = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var picked = (svc ?? "")
            .Split([' ', ',', '，', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(set.Contains)
            .Distinct()
            .ToList();
        return picked.Count > 0 ? string.Join(' ', picked) : fallback;
    }

    /// <summary>提取 MAC 中的十六进制字符（兼容 : - . 分隔）。</summary>
    protected static string CleanHexMac(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var ch in mac!.Trim().ToLowerInvariant())
            if ((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f')) sb.Append(ch);
        return sb.Length is 12 or 16 ? sb.ToString() : mac!.Trim().ToLowerInvariant();
    }

    /// <summary>华为/华三风格 MAC：xxxx-xxxx-xxxx。</summary>
    protected static string DashedMac(string? mac)
    {
        var h = CleanHexMac(mac);
        return h.Length == 12 ? $"{h[..4]}-{h.Substring(4, 4)}-{h.Substring(8, 4)}" : h;
    }

    /// <summary>思科/锐捷风格 MAC：xxxx.xxxx.xxxx。</summary>
    protected static string DottedMac(string? mac)
    {
        var h = CleanHexMac(mac);
        return h.Length == 12 ? $"{h[..4]}.{h.Substring(4, 4)}.{h.Substring(8, 4)}" : h;
    }

    /// <summary>展开接口成员描述：支持空格/逗号分隔，以及 "A to B" 接口区间
    /// （端点须同前缀、仅末段数字不同）。锐捷 "GigabitEthernet 0/1" 这类
    /// "类型名+槽位" 的空格会先合并再处理。返回全部单接口名。</summary>
    protected static IEnumerable<string> ExpandMembers(string? text)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;
        var tokens = text!.Split([' ', ',', '，', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).ToList();

        // 合并 "类型名 槽位" 形式（如 GigabitEthernet 0/1）
        var merged = new List<string>();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (IsIfaceTypeName(tokens[i]) && i + 1 < tokens.Count &&
                System.Text.RegularExpressions.Regex.IsMatch(tokens[i + 1], @"^\d+(/\d+){0,3}$"))
            {
                merged.Add($"{tokens[i]} {tokens[++i]}");
                continue;
            }
            merged.Add(tokens[i]);
        }

        for (var i = 0; i < merged.Count; i++)
        {
            var cur = merged[i];
            if (cur.Equals("to", StringComparison.OrdinalIgnoreCase)) continue;
            if (i + 2 < merged.Count && merged[i + 1].Equals("to", StringComparison.OrdinalIgnoreCase))
            {
                var end = merged[i + 2];
                var a = System.Text.RegularExpressions.Regex.Match(cur, @"^(?<p>.*?)(?<a>\d+)$");
                var b = System.Text.RegularExpressions.Regex.Match(end, @"^(?<p>.*?)(?<a>\d+)$");
                if (a.Success && b.Success && a.Groups["p"].Value == b.Groups["p"].Value &&
                    int.TryParse(a.Groups["a"].Value, out var from) &&
                    int.TryParse(b.Groups["a"].Value, out var to) && from <= to && to - from <= 256)
                {
                    for (var v = from; v <= to; v++) result.Add($"{a.Groups["p"].Value}{v}");
                }
                else
                {
                    result.Add($"{cur} to {end}");
                }
                i += 2;
                continue;
            }
            result.Add(cur);
        }
        return result;
    }

    private static readonly HashSet<string> IfaceTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ethernet", "fastethernet", "gigabitethernet", "ten-gigabitethernet", "xgigabitethernet",
        "25gigabitethernet", "40gigabitethernet", "100gigabitethernet", "aggregateport", "tunnel"
    };

    private static bool IsIfaceTypeName(string s) => IfaceTypeNames.Contains(s.Trim());

    /// <summary>把 ExpandMembers 展开的单接口列表聚合为连续区间文本（同前缀且末段数字连续）。
    /// style="to"：华为/华三 port-group 的 "A to B"；style="list"：思科/锐捷 interface range 的 "A1-A2"。
    /// 无法解析出结尾数字的接口名原样单列。</summary>
    protected static IEnumerable<string> PortRuns(IEnumerable<string> ports, string style = "to")
    {
        var parsed = ports
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p =>
            {
                var m = System.Text.RegularExpressions.Regex.Match(p.Trim(), @"^(?<prefix>.*?)(?<num>\d+)$");
                return m.Success
                    ? (Prefix: m.Groups["prefix"].Value, Num: int.Parse(m.Groups["num"].Value), Raw: p.Trim())
                    : (Prefix: p.Trim(), Num: int.MinValue, Raw: p.Trim());
            })
            .OrderBy(x => x.Prefix, StringComparer.Ordinal)
            .ThenBy(x => x.Num)
            .ToList();

        var runs = new List<(string Prefix, int From, int To)>();
        foreach (var x in parsed)
        {
            if (x.Num == int.MinValue)
            {
                runs.Add((x.Raw, int.MinValue, int.MinValue));
                continue;
            }
            if (runs.Count > 0 && runs[^1].From != int.MinValue &&
                runs[^1].Prefix == x.Prefix && runs[^1].To + 1 == x.Num)
            {
                runs[^1] = (x.Prefix, runs[^1].From, x.Num);
            }
            else
            {
                runs.Add((x.Prefix, x.Num, x.Num));
            }
        }

        foreach (var (prefix, from, to) in runs)
        {
            if (from == int.MinValue) { yield return prefix; continue; }
            if (from == to) yield return prefix + from;
            else if (style == "list") yield return $"{prefix}{from}-{to}";
            else yield return $"{prefix}{from} to {prefix}{to}";
        }
    }
}
