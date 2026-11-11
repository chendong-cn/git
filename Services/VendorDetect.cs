using System.Text.RegularExpressions;
using NetConfigPro.Models;

namespace NetConfigPro.Services;

/// <summary>厂商识别：从配置文本特征判断所属平台（部署一致性校验 / 现网导入共用）。</summary>
public static class VendorDetect
{
    public static Vendor? Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // 华三强特征（与华为同源 Comware，Vlan-interface 拼写为其独有）
        if (text.Contains("Vlan-interface") || text.Contains("security-zone name")
            || text.Contains("public-key local create"))
            return Vendor.H3C;

        // 华为独有拼写/命令
        if (text.Contains("Vlanif") || text.Contains("stelnet"))
            return Vendor.Huawei;

        var lower = text.ToLowerInvariant();
        if (lower.Contains("configure terminal") || lower.Contains("hostname "))
        {
            var isRuijie = Regex.IsMatch(text, @"GigabitEthernet\s+\d", RegexOptions.IgnoreCase)
                           || lower.Contains("enable service ssh-server")
                           || lower.Contains("enable service web-server")
                           || lower.Contains("service dhcp")
                           || lower.Contains("interface vlan ")
                           || lower.Contains("interface gigabitethernet ");
            return isRuijie ? Vendor.Ruijie : Vendor.Cisco;
        }

        // Comware 通用词（sysname/nat outbound/ip route-static）无独有特征时不下结论，
        // 避免华为/华三误判——由用户人工确认平台。
        return null;
    }
}
