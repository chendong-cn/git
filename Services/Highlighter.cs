using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace NetConfigPro.Services;

/// <summary>把配置文本渲染成带语法着色的只读文档。</summary>
public static class Highlighter
{
    // 浅色输出面板配色：深蓝命令 / 琥珀数字 / 深灰正文 / 绿色注释
    private static readonly SolidColorBrush CmdBrush = new(Color.FromRgb(0x0B, 0x6E, 0xCC));
    private static readonly SolidColorBrush NumBrush = new(Color.FromRgb(0xC0, 0x74, 0x08));
    private static readonly SolidColorBrush TextBrush = new(Color.FromRgb(0x24, 0x33, 0x4F));
    private static readonly SolidColorBrush NoteBrush = new(Color.FromRgb(0x17, 0x84, 0x5C));
    private static readonly Regex TokenRe = new(@"(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}|\b\d+\b)", RegexOptions.Compiled);

    public static FlowDocument Render(string text)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(6, 4, 6, 10),
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New"),
            FontSize = 12.5,
            LineHeight = 17.5
        };

        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var para = new Paragraph { Margin = new Thickness(0) };

            var head = line.TrimStart();
            if (head.StartsWith('#') || head.StartsWith('!'))
            {
                para.Inlines.Add(new Run(line) { Foreground = NoteBrush });
                doc.Blocks.Add(para);
                continue;
            }

            var indentLen = line.Length - head.Length;
            if (indentLen > 0) para.Inlines.Add(new Run(line[..indentLen]));

            var split = head.Split(' ', 2);
            para.Inlines.Add(new Run(split[0]) { Foreground = CmdBrush, FontWeight = FontWeights.SemiBold });

            if (split.Length > 1)
            {
                var rest = " " + split[1];
                var last = 0;
                foreach (Match m in TokenRe.Matches(rest))
                {
                    if (m.Index > last) para.Inlines.Add(new Run(rest[last..m.Index]) { Foreground = TextBrush });
                    para.Inlines.Add(new Run(m.Value) { Foreground = NumBrush });
                    last = m.Index + m.Length;
                }
                if (last < rest.Length) para.Inlines.Add(new Run(rest[last..]) { Foreground = TextBrush });
            }

            doc.Blocks.Add(para);
        }

        return doc;
    }
}
