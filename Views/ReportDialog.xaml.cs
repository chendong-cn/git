using System.Text;
using System.Windows;
using System.Windows.Controls;
using NetConfigPro.Services;

namespace NetConfigPro.Views;

/// <summary>检查报告生成：粘贴输出 → 自动分析 → HTML/MD 预览导出。</summary>
public partial class ReportDialog : Window
{
    private string _html = "";
    private string _md = "";
    private string _meta = "";

    public ReportDialog(string? presetMeta = null, string? presetRaw = null)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(presetMeta)) TxtMeta.Text = presetMeta!;
        if (!string.IsNullOrWhiteSpace(presetRaw)) TxtRaw.Text = presetRaw!;
        _meta = TxtMeta.Text;
    }

    private void BtnMin_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>载入「网络命令」页当前执行结果（由 DiagView 调用）。</summary>
    public void LoadResult(string raw)
    {
        TxtRaw.Text = raw;
        BtnBuild_Click(this, new RoutedEventArgs());
    }

    private void BtnLoad_Click(object sender, RoutedEventArgs e)
    {
        // 把当前剪贴板文本载入（终端里复制的结果直接粘贴进来）
        try
        {
            if (Clipboard.ContainsText()) TxtRaw.Text = Clipboard.GetText();
            else MessageBox.Show("剪贴板中没有文本。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("读取剪贴板失败：" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnBuild_Click(object sender, RoutedEventArgs e)
    {
        var raw = TxtRaw.Text ?? "";
        if (raw.Trim().Length == 0)
        {
            MessageBox.Show("请先粘贴一段命令输出，或在「网络命令」页执行后点“载入当前结果”。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _meta = string.IsNullOrWhiteSpace(TxtMeta.Text) ? "设备信息未填写" : TxtMeta.Text.Trim();
        var issues = ReportBuilder.Analyze(raw);
        var errs = issues.Count(i => i.Kind == "Err");
        var warns = issues.Count(i => i.Kind == "Warn");
        TxtAnalyze.Text = $"分析完成：异常 {errs} · 注意 {warns}（含正常行自动忽略）";
        TxtAnalyze.Foreground = (System.Windows.Media.Brush)(errs > 0
            ? FindResource("AccentRedBrush") : warns > 0 ? FindResource("AccentAmberBrush") ?? FindResource("AccentCyanBrush")
            : FindResource("AccentGreenBrush"));

        var title = string.IsNullOrWhiteSpace(TxtTitle.Text) ? "网络设备检查报告" : TxtTitle.Text.Trim();
        _html = ReportBuilder.BuildHtml(title, _meta, ReportBuilder.Summary(issues), issues, raw);
        BtnExport.IsEnabled = true;
        BtnExportMd.IsEnabled = true;
        BuildMarkdown(title, issues, raw);

        // 预览用原生控件展示（WebBrowser 属 HwndHost，在 AllowsTransparency 分层窗口中无法渲染）
        ListIssues.Items.Clear();
        var problems = issues.Where(i => i.Kind != "Ok").ToList();
        if (problems.Count == 0)
            ListIssues.Items.Add(new ListBoxItem
            {
                Content = "✔ 未发现明显异常（仍需人工确认关键输出）",
                Foreground = (System.Windows.Media.Brush)FindResource("AccentGreenBrush")
            });
        foreach (var p in problems.Take(120))
        {
            var brush = p.Kind == "Err"
                ? (System.Windows.Media.Brush)FindResource("AccentRedBrush")
                : (System.Windows.Media.Brush)FindResource("AccentAmberBrush");
            ListIssues.Items.Add(new ListBoxItem
            {
                Content = p.Kind == "Err" ? $"【异常】 {p.Text}" : $"【注意】 {p.Text}",
                Foreground = brush,
                ToolTip = p.Text
            });
        }
        var lines = raw.Split('\n');
        TxtRawPreview.Text = lines.Length > 120 ? string.Join('\n', lines.Take(120)) + "\n…（原始输出过长已截断，完整内容见导出的报告附录）" : raw;
        TxtPreviewTitle.Text = $"报告预览（异常 {errs} · 注意 {warns}——导出的 HTML 为完整版）";
    }

    private void BuildMarkdown(string title, IReadOnlyList<ReportIssue> issues, string raw)
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(title).AppendLine();
        sb.AppendLine();
        sb.Append("> 设备信息：").Append(_meta).AppendLine();
        sb.Append("> 生成时间：").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).AppendLine();
        sb.AppendLine();
        sb.Append("## 检查结果").AppendLine();
        sb.Append("- 异常：").Append(issues.Count(i => i.Kind == "Err")).AppendLine();
        sb.Append("- 注意：").Append(issues.Count(i => i.Kind == "Warn")).AppendLine();
        sb.AppendLine();
        sb.Append("## 疑似问题清单").AppendLine();
        var problems = issues.Where(i => i.Kind != "Ok").ToList();
        if (problems.Count == 0) sb.AppendLine("未发现明显异常。");
        else foreach (var p in problems.Take(50)) sb.AppendLine("- **[").Append(p.Kind).Append("]** ").Append(p.Text);
        sb.AppendLine();
        sb.Append("## 结论建议").AppendLine().AppendLine().Append(ReportBuilder.Summary(issues)).AppendLine();
        sb.AppendLine();
        sb.AppendLine("## 附录：原始输出").AppendLine().AppendLine("```").Append(raw).AppendLine().AppendLine("```");
        _md = sb.ToString();
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (_html.Length == 0) return;
        var tag = TextUtil.SanitizeFileName(_meta.Replace(" ", "_"));
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出检查报告",
            Filter = "HTML 报告 (*.html)|*.html",
            FileName = $"检查报告_{tag}.html"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            System.IO.File.WriteAllText(dlg.FileName, _html, new UTF8Encoding(false));
            MessageBox.Show($"报告已导出：\n{dlg.FileName}\n\n可直接双击用浏览器打开并发送给客户。", "完成",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("导出失败：" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnExportMd_Click(object sender, RoutedEventArgs e)
    {
        if (_md.Length == 0) return;
        var tag = TextUtil.SanitizeFileName(_meta.Replace(" ", "_"));
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出 Markdown 摘要",
            Filter = "Markdown (*.md)|*.md|文本文件 (*.txt)|*.txt",
            FileName = $"检查报告_{tag}.md"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            System.IO.File.WriteAllText(dlg.FileName, _md, new UTF8Encoding(false));
            MessageBox.Show("Markdown 摘要已导出：" + dlg.FileName, "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("导出失败：" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
