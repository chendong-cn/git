using System.Text;
using System.Text.RegularExpressions;

namespace NetConfigPro.Services;

/// <summary>一条诊断结论。</summary>
public sealed class ReportIssue
{
    public string Kind { get; init; } = "Info"; // Ok / Warn / Err
    public string Text { get; init; } = "";
}

/// <summary>巡检/诊断结果报告生成（分析异常 → 单文件 HTML）。</summary>
public static class ReportBuilder
{
    /// <summary>把一段命令执行输出切分为“疑似问题”结论行；正常行归为通过。</summary>
    public static List<ReportIssue> Analyze(string output)
    {
        var issues = new List<ReportIssue>();
        if (string.IsNullOrWhiteSpace(output)) return issues;

        // 判定模式（按权重先后；time=/bytes= 的正常行必须放行）
        var rules = new (string Kind, Regex Rx)[]
        {
            ("Err", new Regex(@"\bunreachable\b|无法访问目标主机|Destination host unreachable|网络不可达", RegexOptions.IgnoreCase)),
            ("Err", new Regex(@"\btimed out\b|Request timed out|请求超时|超时", RegexOptions.IgnoreCase)),
            ("Err", new Regex(@"% Unknown command|% Invalid input|% Ambiguous|parse error|语法错误", RegexOptions.IgnoreCase)),
            ("Err", new Regex(@"\bAccess denied\b|拒绝访问|Permission denied|需要管理员", RegexOptions.IgnoreCase)),
            ("Err", new Regex(@"\bfailed\b|失败|错误|找不到|not found|无法连接", RegexOptions.IgnoreCase)),
            ("Warn", new Regex(@"\d{1,3}(?:\.\d{1,3}){3}.*\b(?:timed out|超时|丢失|loss)\b", RegexOptions.IgnoreCase)),
            ("Warn", new Regex(@"\d+% loss|丢包[率:：]?\s*\d+%|loss[^0-9]{0,20}\d+%", RegexOptions.IgnoreCase)),
            ("Warn", new Regex(@"\b100%\s+loss\b", RegexOptions.IgnoreCase)),
            ("Warn", new Regex(@"\bWarning\b|警告|Alarm", RegexOptions.IgnoreCase))
        };

        var seen = new HashSet<string>();
        foreach (var line in output.Split('\n'))
        {
            var s = line.Trim();
            if (s.Length == 0 || s.StartsWith('#') || s.StartsWith("══") || s.StartsWith("[")) continue;
            // 正常响应行放行（bytes=… time<… TTL、=20ms 等）
            if (Regex.IsMatch(s, @"bytes=\d+|time[<=]\s*\d+ms|TTL=\d+|Reply from|来自 \d|已接收", RegexOptions.IgnoreCase)) continue;

            var matched = false;
            foreach (var (kind, rx) in rules)
            {
                if (!rx.IsMatch(s)) continue;
                // 丢包统计里 "0% loss" 不是问题
                if (rx.ToString().Contains("% loss") && Regex.IsMatch(s, @"0% loss|丢包[率:：]?\s*0%")) break;
                var key = kind + "|" + s;
                if (seen.Add(key)) issues.Add(new ReportIssue { Kind = kind, Text = s.Length > 160 ? s[..160] : s });
                matched = true;
                break;
            }
            if (!matched && issues.Count == 0) { /* 不输出正常行明细 */ }
        }
        return issues;
    }

    /// <summary>生成单文件 HTML 报告（内联浅色科技风样式）。</summary>
    public static string BuildHtml(string title, string meta, string summary,
        IReadOnlyList<ReportIssue> issues, string rawOutput)
    {
        var sb = new StringBuilder(8192);
        sb.Append("<!DOCTYPE html><html lang=\"zh\"><head><meta charset=\"utf-8\"><title>").Append(title).Append("</title>");
        sb.Append("<style>");
        sb.Append("body{font-family:'Segoe UI',Microsoft YaHei,sans-serif;background:#F3F7FC;color:#1C2B46;margin:0;padding:24px}");
        sb.Append(".card{background:#fff;border:1px solid #DCE7F5;border-radius:10px;padding:16px 20px;margin-bottom:14px;box-shadow:0 1px 4px rgba(28,43,70,.06)}");
        sb.Append("h1{font-size:20px;margin:0 0 4px;color:#0C7FDB}h2{font-size:14px;color:#0C7FDB;margin:0 0 10px}");
        sb.Append(".dim{color:#6B7C99;font-size:12px}.meta{font-size:12px;color:#1C2B46;line-height:1.9}");
        sb.Append(".ok{color:#0A7A3D;font-weight:600}.warn{color:#B25E00;font-weight:600}.err{color:#C62828;font-weight:600}");
        sb.Append("table{width:100%;border-collapse:collapse;font-size:12.5px}td,th{border:1px solid #E2EAF4;padding:7px 10px;text-align:left;vertical-align:top}");
        sb.Append("th{background:#EAF2FB}.badge{display:inline-block;min-width:38px;text-align:center;border-radius:10px;padding:1px 8px;font-size:11px;color:#fff}");
        sb.Append(".b-ok{background:#2E9E5B}.b-warn{background:#E0912F}.b-err{background:#D64545}");
        sb.Append("pre{background:#0E1728;color:#CFE3F7;font-family:Consolas,'Courier New';font-size:11.5px;padding:12px;border-radius:8px;overflow-x:auto;white-space:pre-wrap;word-break:break-all}");
        sb.Append(".stat{display:inline-block;margin-right:18px;font-size:13px}");
        sb.Append("</style></head><body>");
        sb.Append("<div class=\"card\"><h1>").Append(HtmlEsc(title)).Append("</h1>");
        sb.Append("<div class=\"dim\">NetConfigPro 一键报告 · 生成时间 ")
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("</div></div>");

        sb.Append("<div class=\"card\"><h2>设备与检查信息</h2><div class=\"meta\">").Append(HtmlEsc(meta)).Append("</div>");
        sb.Append("<div style=\"margin-top:10px\"><span class=\"stat ok\">✔ 正常 ").Append(issues.Count(i => i.Kind == "Ok")).Append("</span>");
        sb.Append("<span class=\"stat warn\">⚠ 注意 ").Append(issues.Count(i => i.Kind == "Warn")).Append("</span>");
        sb.Append("<span class=\"stat err\">✘ 异常 ").Append(issues.Count(i => i.Kind == "Err")).Append("</span></div></div>");

        var problems = issues.Where(i => i.Kind != "Ok").ToList();
        sb.Append("<div class=\"card\"><h2>疑似问题清单（").Append(problems.Count).Append(" 条）</h2>");
        if (problems.Count == 0) sb.Append("<div class=\"ok\">未发现明显异常（仍需人工确认关键输出）。</div>");
        else
        {
            sb.Append("<table><tr><th style=\"width:70px\">级别</th><th>输出片段</th></tr>");
            foreach (var p in problems.Take(80))
            {
                var cls = p.Kind == "Err" ? "b-err" : "b-warn";
                var tag = p.Kind == "Err" ? "异常" : "注意";
                sb.Append("<tr><td><span class=\"badge ").Append(cls).Append("\">").Append(tag).Append("</span></td><td>").Append(HtmlEsc(p.Text)).Append("</td></tr>");
            }
            if (problems.Count > 80) sb.Append("<tr><td colspan=\"2\" class=\"dim\">…仅显示前 80 条，其余见附录原始输出</td></tr>");
            sb.Append("</table>");
        }
        sb.Append("</div>");

        sb.Append("<div class=\"card\"><h2>结论建议</h2><div style=\"font-size:12.5px;line-height:1.9\">").Append(summary).Append("</div></div>");

        sb.Append("<div class=\"card\"><h2>附录：原始输出</h2><pre>").Append(HtmlEsc(rawOutput)).Append("</pre></div>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    /// <summary>按问题类型生成简短结论文字。</summary>
    public static string Summary(IReadOnlyList<ReportIssue> issues)
    {
        var errs = issues.Count(i => i.Kind == "Err");
        var warns = issues.Count(i => i.Kind == "Warn");
        if (errs == 0 && warns == 0) return "本次检查未发现明显异常。建议把关键输出（版本/接口状态/资源占用）再人工过目一遍后归档。";
        var parts = new List<string>();
        if (errs > 0) parts.Add($"检测到 {errs} 处疑似异常（不通/超时/命令报错/拒绝访问等），请按附录定位到具体命令与设备核对处理");
        if (warns > 0) parts.Add($"另有 {warns} 处需人工注意项（丢包、告警等），建议结合时段与业务判断");
        parts.Add("处理完成后建议重新执行检查确认问题消失。");
        return string.Join("；", parts);
    }

    private static string HtmlEsc(string s) =>
        System.Net.WebUtility.HtmlEncode(s ?? "");
}
