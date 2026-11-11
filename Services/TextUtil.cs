using System.IO;
using System.Text;

namespace NetConfigPro.Services;

/// <summary>导出相关的文本工具：CSV 字段转义、文件名净化。</summary>
public static class TextUtil
{
    /// <summary>CSV 字段转义：含逗号/引号/换行时用双引号包裹并把内部引号翻倍（RFC 4180）。</summary>
    public static string EscapeCsv(string? field)
    {
        var f = field ?? "";
        if (f.Contains(',') || f.Contains('"') || f.Contains('\n') || f.Contains('\r'))
            return "\"" + f.Replace("\"", "\"\"") + "\"";
        return f;
    }

    /// <summary>把任意文本净化成可用的文件主名（替换 Windows 非法字符、截断超长、去尾点）。</summary>
    public static string SanitizeFileName(string? name, string fallback = "config")
    {
        var s = (name ?? "").Trim();
        if (s.Length == 0) return fallback;

        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            var bad = Array.IndexOf(Path.GetInvalidFileNameChars(), ch) >= 0;
            sb.Append(bad ? '_' : ch);
        }
        var r = sb.ToString();
        if (r.Length > 80) r = r[..80];
        while (r.EndsWith('.') || r.EndsWith(' ')) r = r[..^1];
        return r.Length == 0 ? fallback : r;
    }
}
