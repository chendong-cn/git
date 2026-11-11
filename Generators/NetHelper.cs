using System.Net;

namespace NetConfigPro.Generators;

public static class NetHelper
{
    /// <summary>把 "192.168.1.0/24" 或 "192.168.1.0 255.255.255.0" 解析成 (地址, 掩码)。解析失败返回 null。</summary>
    public static (string Ip, string Mask)? ParseCidr(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var s = value.Trim();
        if (s.Equals("any", StringComparison.OrdinalIgnoreCase)) return null;

        if (s.Contains('/'))
        {
            var parts = s.Split('/', 2);
            if (!IPAddress.TryParse(parts[0].Trim(), out var ip)) return null;
            if (!int.TryParse(parts[1].Trim(), out var bits) || bits is < 0 or > 32) return null;
            return (parts[0].Trim(), MaskFromBits(bits));
        }

        var seg = s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (seg.Length == 2 && IPAddress.TryParse(seg[0], out _) && IPAddress.TryParse(seg[1], out _))
            return (seg[0], seg[1]);

        if (IPAddress.TryParse(s, out _)) return (s, "255.255.255.255");
        return null;
    }

    public static string MaskFromBits(int bits)
    {
        if (bits <= 0) return "0.0.0.0";
        var mask = bits == 32 ? 0xFFFFFFFFu : (0xFFFFFFFFu << (32 - bits)) & 0xFFFFFFFFu;
        return new IPAddress(BitConverter.GetBytes(mask).Reverse().ToArray()).ToString();
    }

    /// <summary>把 CIDR 转成反掩码（通配符）。</summary>
    public static string WildcardFromMask(string mask)
    {
        if (!IPAddress.TryParse(mask, out var m)) return "0.0.0.0";
        var bytes = m.GetAddressBytes();
        if (bytes.Length != 4) return "0.0.0.0";
        var wild = bytes.Select(b => (byte)(255 - b)).ToArray();
        return new IPAddress(wild).ToString();
    }

    public static int BitsFromMask(string mask)
    {
        if (!IPAddress.TryParse(mask, out var m)) return 24;
        return m.GetAddressBytes().Select(b => Convert.ToString(b, 2).Count(c => c == '1')).Sum();
    }

    /// <summary>把 "10-20,30" 展开成整数列表。</summary>
    public static List<int> ExpandVlanList(string? value)
    {
        var result = new List<int>();
        if (string.IsNullOrWhiteSpace(value)) return result;
        foreach (var raw in value.Replace('，', ',').Split(','))
        {
            var token = raw.Trim();
            if (token.Length == 0) continue;
            if (token.Contains('-'))
            {
                var parts = token.Split('-', 2);
                if (int.TryParse(parts[0].Trim(), out var a) && int.TryParse(parts[1].Trim(), out var b) && a <= b)
                    for (var i = a; i <= b; i++) result.Add(i);
            }
            else if (int.TryParse(token, out var v)) result.Add(v);
        }
        return result.Distinct().OrderBy(x => x).ToList();
    }

    /// <summary>华为/华三风格 VLAN 区间串：10 to 20。</summary>
    public static string ToHuaweiRange(List<int> vlans)
    {
        if (vlans.Count == 0) return "";
        var sb = new List<string>();
        var start = vlans[0];
        var prev = vlans[0];
        foreach (var v in vlans.Skip(1))
        {
            if (v == prev + 1) { prev = v; continue; }
            sb.Add(start == prev ? start.ToString() : $"{start} to {prev}");
            start = prev = v;
        }
        sb.Add(start == prev ? start.ToString() : $"{start} to {prev}");
        return string.Join(' ', sb);
    }

    /// <summary>思科/锐捷风格 VLAN 区间串：10-20,30。</summary>
    public static string ToCiscoRange(List<int> vlans)
    {
        if (vlans.Count == 0) return "";
        var parts = new List<string>();
        var start = vlans[0];
        var prev = vlans[0];
        foreach (var v in vlans.Skip(1))
        {
            if (v == prev + 1) { prev = v; continue; }
            parts.Add(start == prev ? start.ToString() : $"{start}-{prev}");
            start = prev = v;
        }
        parts.Add(start == prev ? start.ToString() : $"{start}-{prev}");
        return string.Join(',', parts);
    }

    /// <summary>协议/关键字映射：http、https、ssh、telnet、ftp、dns、icmp、any。</summary>
    public static string ServiceToPort(string? service) => (service ?? "").Trim().ToLowerInvariant() switch
    {
        "http" or "web" or "80" => "80",
        "https" or "443" => "443",
        "ssh" or "22" => "22",
        "telnet" or "23" => "23",
        "ftp" or "21" => "21",
        "dns" or "domain" or "53" => "53",
        "icmp" or "ping" => "icmp",
        "smtp" or "25" => "25",
        "rdp" or "3389" => "3389",
        _ => service ?? "any"
    };

    public static bool IsValidIp(string? ip) => !string.IsNullOrWhiteSpace(ip) && IPAddress.TryParse(ip.Trim(), out _);

    private static uint ToUint(string ip)
    {
        var b = IPAddress.Parse(ip).GetAddressBytes();
        return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
    }

    private static string FromUint(uint v) =>
        new IPAddress([(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v]).ToString();

    /// <summary>返回下一个 IP 地址，输入非法时返回 null。</summary>
    public static string? NextIp(string? ip)
    {
        if (!IsValidIp(ip)) return null;
        var next = ToUint(ip!.Trim()) + 1;
        return next > 0xFFFFFFFFu ? null : FromUint(next);
    }

    /// <summary>返回网段内最后一个可用主机地址（广播地址减一）。</summary>
    public static string LastUsableHost(string network, string mask)
    {
        if (!IsValidIp(network) || !IsValidIp(mask)) return network;
        var broadcast = ToUint(network.Trim()) | ~ToUint(mask.Trim());
        return FromUint(broadcast - 1);
    }
}
