using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NetConfigPro.Services;

/// <summary>一次扫描结果条目：在线主机的 IP ↔ MAC 映射。</summary>
public class ArpEntry
{
    public string Ip { get; set; } = "";
    public string Mac { get; set; } = "";
    public string Type { get; set; } = "";      // 动态 / 静态（arp -a 类型列原文）
    public bool IsLocal { get; set; }            // 是否本机某网卡地址
    public int Ordinal { get; set; }             // 表格序号（结果排序后填）
    public string Vendor { get; set; } = "";     // OUI 识别的厂商
    public string Note { get; set; } = "";       // 本机 / 同 MAC 多 IP 等标注

    public string MacNorm => Mac.Replace("-", ":").ToLowerInvariant();
    public string MacOui => MacNorm.Length >= 8 ? MacNorm[..8] : MacNorm;
}

/// <summary>IP-MAC 地址扫描：对目标网段并发 ping 触发 ARP 解析，随后读取本机 ARP 表列出在线主机。
/// 原理：本机向目标发包前必须先解析目标 MAC（ARP 请求）；目标即使禁 ping（不回 ICMP），
/// 其 IP 栈也会应答 ARP —— 因此 arp 表能发现“禁 ping 但在线”的设备。</summary>
public static class IpMacScanner
{
    public const int MaxHosts = 2048;

    /// <summary>常见厂商 OUI（MAC 前 3 字节，键为小写 6 位 hex）。识别不到的显示 “--”。</summary>
    private static readonly Dictionary<string, string> Oui = new(StringComparer.Ordinal)
    {
        // 华为
        ["00e0fc"] = "华为", ["4c7766"] = "华为", ["8c47be"] = "华为", ["ac3c0b"] = "华为",
        ["9c34eb"] = "华为", ["6c9b02"] = "华为", ["cc50e3"] = "华为", ["d8cb8a"] = "华为", ["98e7f5"] = "华为",
        // 华三 H3C
        ["000fe2"] = "华三", ["4c0f6e"] = "华三", ["148833"] = "华三", ["d46a6a"] = "华三", ["646e97"] = "华三",
        // 思科 Cisco
        ["00000c"] = "思科", ["001b0c"] = "思科", ["0023ac"] = "思科", ["001aa2"] = "思科", ["f47f35"] = "思科",
        ["2cab00"] = "思科", ["649ef3"] = "思科", ["001d45"] = "思科",
        // 锐捷
        ["00d0f8"] = "锐捷",
        // TP-Link / 普联
        ["ec888f"] = "TP-Link", ["50fa84"] = "TP-Link", ["8c210a"] = "TP-Link", ["a42bb0"] = "TP-Link",
        ["50c7bf"] = "TP-Link", ["30b49e"] = "TP-Link", ["5c6a7d"] = "TP-Link", ["40167e"] = "TP-Link",
        ["b09575"] = "TP-Link", ["c4e984"] = "TP-Link", ["1027f5"] = "TP-Link",
        // 小米
        ["640980"] = "小米", ["7811dc"] = "小米", ["8cbebe"] = "小米", ["2cf05d"] = "小米",
        ["f0b429"] = "小米", ["9c99a0"] = "小米",
        // 苹果 Apple
        ["000393"] = "苹果", ["3c0754"] = "苹果", ["8c5877"] = "苹果", ["040cce"] = "苹果",
        ["4860bc"] = "苹果", ["f01898"] = "苹果", ["acbc32"] = "苹果",
        // 网卡 / 电脑厂商
        ["001b21"] = "Intel 网卡", ["3c970e"] = "Intel 网卡", ["3cfdfe"] = "Intel 网卡",
        ["00e04c"] = "Realtek", ["c83a35"] = "Realtek",
        ["00215e"] = "联想", ["3cd92b"] = "惠普", ["0018f3"] = "戴尔", ["001e8c"] = "华硕",
        // 安防 / 网络周边
        ["4408b7"] = "海康威视", ["3ce5a6"] = "大华", ["c82e46"] = "腾达",
    };

    /// <summary>解析扫描目标输入，支持多段逗号分隔；每段支持 CIDR / 起止区间 / 单 IP。
    /// 示例：192.168.1.0/24、192.168.1.10-192.168.1.100、192.168.1.10-100、192.168.1.5。</summary>
    public static bool TryExpandTarget(string? input, out List<string> ips, out string error)
    {
        ips = new List<string>();
        error = "";
        if (string.IsNullOrWhiteSpace(input)) { error = "请先填写要扫描的网段"; return false; }

        var seen = new HashSet<uint>();
        foreach (var raw in input.Split(','))
        {
            var seg = raw.Trim();
            if (seg.Length == 0) continue;
            uint start, end;
            if (seg.Contains('/'))
            {
                var p = seg.Split('/', 2);
                if (!IPAddress.TryParse(p[0].Trim(), out var ip) || !int.TryParse(p[1].Trim(), out var bits) || bits is < 0 or > 32)
                { error = $"无法识别的网段：{seg}（示例 192.168.1.0/24）"; return false; }
                var ipv = IpToUint(ip.ToString());
                var mask = bits == 0 ? 0u : (0xFFFFFFFFu << (32 - bits)) & 0xFFFFFFFFu;
                var net = ipv & mask;
                var hosts = bits == 0 ? 4294967296UL : (1UL << (32 - bits)); // /0 = 全 IPv4，用 ulong 防移位溢出
                if (bits >= 31) { start = net; end = (uint)(net + hosts - 1); }    // /31 /32 点对点全部可用
                else { start = net + 1; end = (uint)(net + hosts - 2); }           // 跳过网络号与广播地址
                if (end < start) { error = $"网段 {seg} 没有可用主机地址"; return false; }
            }
            else if (seg.Contains('-'))
            {
                var p = seg.Split('-', 2);
                var left = p[0].Trim();
                var right = p[1].Trim();
                if (!IPAddress.TryParse(left, out _))
                { error = $"无法识别的区间起点：{left}（示例 192.168.1.10-100）"; return false; }
                var a = IpToUint(left);
                uint b;
                if (right.All(char.IsAsciiDigit) && right.Length <= 3
                    && int.TryParse(right, out var last) && last is >= 0 and <= 255)
                {
                    b = (a & 0xFFFFFF00u) | (uint)last;   // 缩写端点：继承起点前 3 段
                }
                else if (IPAddress.TryParse(right, out var rip))
                {
                    b = IpToUint(rip.ToString());
                }
                else { error = $"无法识别的区间终点：{right}"; return false; }
                if (a > b) { error = $"区间起点大于终点：{seg}"; return false; }
                start = a; end = b;
            }
            else
            {
                if (!IPAddress.TryParse(seg, out var ip)) { error = $"无法识别的地址：{seg}"; return false; }
                start = end = IpToUint(ip.ToString());
            }

            if (end - start + 1 > MaxHosts) { error = $"范围过大（> {MaxHosts} 个地址），请缩小网段"; return false; }
            for (var v = start; v <= end; v++) seen.Add(v);
        }

        if (seen.Count == 0) { error = "没有可扫描的地址"; return false; }
        if (seen.Count > MaxHosts) { error = $"合计超过 {MaxHosts} 个地址，请缩小范围"; return false; }
        ips = seen.OrderBy(v => v).Select(UintToIp).ToList();
        return true;
    }

    private static uint IpToUint(string ip)
    {
        var b = IPAddress.Parse(ip).GetAddressBytes();
        return (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
    }

    private static string UintToIp(uint v) =>
        new IPAddress(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }).ToString();

    /// <summary>对目标地址并发发 ping（限并发 96），全部完成后短暂等待 ARP 表沉淀。返回前可取消。</summary>
    public static async Task ProbeAsync(List<string> ips, IProgress<int>? progress, CancellationToken ct)
    {
        var total = ips.Count;
        if (total == 0) return;
        var done = 0;
        using var sem = new SemaphoreSlim(96);
        var tasks = ips.Select(async ip =>
        {
            await sem.WaitAsync(ct);
            try
            {
                using var p = new Ping();
                try { await p.SendPingAsync(IPAddress.Parse(ip), 500).WaitAsync(ct); } catch { /* 离线或已取消，忽略 */ }
            }
            finally
            {
                sem.Release();
                var n = Interlocked.Increment(ref done);
                progress?.Report(n * 100 / total);
            }
        });
        try { await Task.WhenAll(tasks); }
        catch (OperationCanceledException) { throw; }
        if (!ct.IsCancellationRequested)
            await Task.Delay(400, ct); // 等最后一批 ARP reply 写入缓存
    }

    /// <summary>执行 arp -a 并返回原始输出（GBK 解码，中文系统正常）。</summary>
    public static string RunArp()
    {
        try
        {
            Encoding enc;
            try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); enc = Encoding.GetEncoding(936); }
            catch { enc = Encoding.UTF8; }
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "arp.exe",
                Arguments = "-a",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = enc,
                StandardErrorEncoding = enc
            };
            using var p = new System.Diagnostics.Process { StartInfo = psi };
            var sb = new StringBuilder(4096);
            p.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) lock (sb) sb.AppendLine(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) lock (sb) sb.AppendLine(e.Data); };
            if (!p.Start()) return "";
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit(5000);
            lock (sb) return sb.ToString();
        }
        catch { return ""; }
    }

    private static readonly Regex ReIface = new(@"^接口:\s*(\d+\.\d+\.\d+\.\d+)", RegexOptions.Compiled);
    private static readonly Regex ReRow = new(@"^\s*(\d+\.\d+\.\d+\.\d+)\s+([0-9a-fA-F]{2}(?:-[0-9a-fA-F]{2}){5})\s+(\S+)", RegexOptions.Compiled);

    /// <summary>解析 arp -a 文本 → (本机接口 IP 列表, 有效条目)。过滤广播/组播 MAC（首字节最低位=1）。</summary>
    public static (List<string> LocalIps, List<ArpEntry> Entries) ParseArpText(string text)
    {
        var locals = new List<string>();
        var entries = new List<ArpEntry>();
        if (string.IsNullOrWhiteSpace(text)) return (locals, entries);

        foreach (var line in text.Split('\n'))
        {
            var s = line.TrimEnd('\r');
            var im = ReIface.Match(s);
            if (im.Success) { locals.Add(im.Groups[1].Value); continue; }
            var rm = ReRow.Match(s);
            if (!rm.Success) continue;
            var mac = rm.Groups[2].Value;
            var first = Convert.ToByte(mac.Substring(0, 2), 16);
            if ((first & 0x01) == 1) continue; // 广播(ff:..) / 组播(01:00:5e / 33:33 等)
            entries.Add(new ArpEntry
            {
                Ip = rm.Groups[1].Value,
                Mac = mac,
                Type = rm.Groups[3].Value,
                IsLocal = locals.Contains(rm.Groups[1].Value)
            });
        }
        return (locals, entries);
    }

    /// <summary>按 OUI 识别厂商（识别不到返回空串）。</summary>
    public static string LookupVendor(string mac)
    {
        var norm = mac.Replace("-", "").Replace(":", "").ToLowerInvariant();
        return norm.Length >= 6 && Oui.TryGetValue(norm[..6], out var v) ? v : "";
    }

    /// <summary>标注：本机 / 同 MAC 多 IP（同一网卡疑似多 IP 或桥接）。</summary>
    public static void Annotate(List<ArpEntry> entries)
    {
        var byMac = entries.GroupBy(e => e.MacNorm).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var e in entries)
        {
            var note = new List<string>();
            if (e.IsLocal) note.Add("本机");
            if (byMac.TryGetValue(e.MacNorm, out var same) && same.Count > 1 && !e.IsLocal)
                note.Add($"同 MAC ×{same.Count} 个 IP");
            e.Note = string.Join("，", note);
            e.Vendor = LookupVendor(e.Mac);
        }
    }

    /// <summary>返回本机在扫描集合内各网卡的 (IP, MAC)。本机通常不出现在自身 ARP 表，
    /// 扫描结果用它补齐“本机行”，让台账完整。类型列填 “—”。</summary>
    public static List<ArpEntry> LocalSelf(HashSet<string> targetSet)
    {
        var list = new List<ArpEntry>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                var mac = ni.GetPhysicalAddress()?.ToString() ?? "";
                if (mac.Length != 12) continue;
                var macStr = string.Join("-", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2))).ToLowerInvariant();
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                    var ip = ua.Address.ToString();
                    if (targetSet.Contains(ip))
                        list.Add(new ArpEntry { Ip = ip, Mac = macStr, Type = "—", IsLocal = true });
                }
            }
        }
        catch { /* 读取失败则省略本机行 */ }
        return list;
    }
}
