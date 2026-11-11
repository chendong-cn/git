using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using NetConfigPro.Generators;
using NetConfigPro.Services;

namespace NetConfigPro.Views;

/// <summary>网络命令：Windows 本机网络诊断命令（生成/复制/导出 + 本机直接执行）。</summary>
public partial class DiagView : UserControl
{
    private readonly Action<string> _setStatus;

    /// <summary>用户勾选的命令 key（跨目标切换保持）。</summary>
    private readonly HashSet<string> _checked = [];

    /// <summary>XAML 解析期默认勾选会提前触发事件，此期间命名控件尚未创建，用闸门跳过。</summary>
    private bool _loading = true;

    private bool _suspendGen;
    private bool _running;
    private string _current = "";

    public DiagView(Store _, Action<string> setStatus)
    {
        InitializeComponent();
        _setStatus = setStatus;
        _loading = false;
        RebuildItems();
        if (Environment.GetEnvironmentVariable("NCP_DIAG_TARGET") is { } tg && tg.Length > 0)
            TxtTarget.Text = tg;
        GenerateNow();

        // [诊断用] NCP_DIAG_RUN=执行条数上限（如 2），自动执行前 N 条可执行命令，便于无人值守冒烟
        if (Environment.GetEnvironmentVariable("NCP_DIAG_RUN") is { } lim)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                async () => await RunPickedAsync(lim));
        }
    }

    // ------------------------------ 勾选列表 ------------------------------
    private void RebuildItems()
    {
        GroupHost.Children.Clear();
        var titleStyle = (Style)FindResource("SubTitle");
        foreach (var g in DiagCmds.Groups)
        {
            var gTitle = new TextBlock
            {
                Text = g.Name,
                Style = titleStyle,
                Margin = new Thickness(0, 4, 0, 6)
            };
            GroupHost.Children.Add(gTitle);

            foreach (var it in g.Items)
            {
                var cb = new CheckBox
                {
                    Content = it.Name,
                    Tag = it.Key,
                    ToolTip = it.Tip,
                    Margin = new Thickness(0, 0, 0, 8),
                    IsChecked = _checked.Add(it.Key) // 新出现项默认勾选
                };
                cb.Checked += Item_Toggled;
                cb.Unchecked += Item_Toggled;
                GroupHost.Children.Add(cb);
            }
        }
        UpdateSummary();
    }

    private void Item_Toggled(object? sender, RoutedEventArgs e)
    {
        if (_loading || _suspendGen) return;
        if (sender is CheckBox { Tag: string key } cb)
        {
            if (cb.IsChecked == true) _checked.Add(key);
            else _checked.Remove(key);
        }
        UpdateSummary();
        GenerateNow();
    }

    private void UpdateSummary()
    {
        var all = 0;
        var runnable = 0;
        foreach (var g in DiagCmds.Groups)
        foreach (var it in g.Items)
        {
            if (!_checked.Contains(it.Key)) continue;
            all++;
            if (DiagCmds.CanRun(it)) runnable++;
        }
        if (TxtSummary != null)
            TxtSummary.Text = $"已选 {all} 条（其中可执行 {runnable} 条，交互式命令仅可复制）";
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e) => SetAll(true);
    private void BtnClearAll_Click(object sender, RoutedEventArgs e) => SetAll(false);

    private void SetAll(bool check)
    {
        // 逐项赋值会同步触发 Checked/Unchecked（被 _suspendGen 屏蔽）——
        // 因此赋值完成后必须以 UI 实际状态重建勾选集合，否则预览与勾选会不同步。
        _suspendGen = true;
        foreach (var cb in GroupHost.Children.OfType<CheckBox>()) cb.IsChecked = check;
        _checked.Clear();
        foreach (var cb in GroupHost.Children.OfType<CheckBox>())
            if (cb.IsChecked == true && cb.Tag is string key) _checked.Add(key);
        _suspendGen = false;
        UpdateSummary();
        GenerateNow();
    }

    // ------------------------------ 收集 / 生成 / 复制 / 导出 ------------------------------
    private void Target_TextChanged(object sender, TextChangedEventArgs e) => GenerateNow();

    private void BtnGenerate_Click(object sender, RoutedEventArgs e) => GenerateNow();

    /// <summary>按勾选收集命令：仅当未填目标时跳过“需要目标”的命令；填了目标则全部纳入。</summary>
    private List<DiagCmd> CollectPicked()
    {
        var hasTarget = TxtTarget.Text.Trim().Length > 0;
        var list = new List<DiagCmd>();
        foreach (var g in DiagCmds.Groups)
        foreach (var it in g.Items)
        {
            if (!_checked.Contains(it.Key)) continue;
            if (it.NeedsTarget && !hasTarget) continue;
            list.Add(it);
        }
        return list;
    }

    private void GenerateNow()
    {
        if (OutputBox == null || GroupHost == null) return;

        var picked = CollectPicked();
        if (picked.Count == 0)
        {
            var hasAny = DiagCmds.Groups.SelectMany(g => g.Items).Any(i => _checked.Contains(i.Key));
            OutputBox.Document = Highlighter.Render(hasAny
                ? "# 当前勾选的命令都需要目标主机 —— 请在左栏填写目标 IP 或域名"
                : "（请先勾选至少一条诊断命令）");
            TxtStat.Text = "0 条命令";
            _current = "";
            return;
        }

        var text = DiagCmds.Render(picked, TxtTarget.Text, TxtPort.Text);
        _current = text;
        OutputBox.Document = Highlighter.Render(text);
        TxtStat.Text = $"{picked.Count} 条命令 ｜ 纯命令 {DiagCmds.Plain(text).Split('\n').Length} 行";
        _setStatus($"已生成 {picked.Count} 条网络诊断命令" + (TxtTarget.Text.Trim().Length == 0 ? "（未填目标）" : ""));
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_current.Length == 0)
        {
            _setStatus("请先勾选命令并生成");
            return;
        }
        try
        {
            var plain = DiagCmds.Plain(_current);
            Clipboard.SetText(plain);
            _setStatus($"已复制 {plain.Split('\n').Length} 条纯命令到剪贴板（可直接粘贴到 cmd / PowerShell）");
        }
        catch (Exception ex)
        {
            MessageBox.Show("复制失败：" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (_current.Length == 0)
        {
            _setStatus("请先勾选命令并生成");
            return;
        }
        var plain = DiagCmds.Plain(_current);
        var tag = TxtTarget.Text.Trim();
        var fileTag = tag.Length == 0 ? "" : "_" + TextUtil.SanitizeFileName(tag);
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出网络诊断命令",
            Filter = "命令脚本 (*.cmd)|*.cmd|文本文件 (*.txt)|*.txt",
            FileName = $"网络诊断{fileTag}.cmd"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            System.IO.File.WriteAllText(dlg.FileName, plain, new UTF8Encoding(false));
            _setStatus($"已导出纯命令：{dlg.FileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show("导出失败：" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ------------------------------ 本机执行 ------------------------------
    private void BtnRun_Click(object sender, RoutedEventArgs e) => _ = RunPickedAsync(null);

    private void BtnClearResult_Click(object sender, RoutedEventArgs e)
    {
        ResultBox.Document.Blocks.Clear();
        _setStatus("已清空执行结果");
    }

    /// <summary>把本机执行结果整理成检查报告（A3）。</summary>
    private void BtnReport_Click(object sender, RoutedEventArgs e)
    {
        var raw = new TextRange(ResultBox.Document.ContentStart, ResultBox.Document.ContentEnd).Text;
        if (raw.Trim().Length == 0)
        {
            _setStatus("请先执行命令（或在报告窗口手动粘贴输出）");
            var dlg0 = new ReportDialog(
                $"网络诊断 · 目标 {TxtTarget.Text.Trim()}", null)
            { Owner = Window.GetWindow(this) };
            dlg0.ShowDialog();
            return;
        }
        var meta = $"网络诊断命令执行结果 ｜ 目标 {TxtTarget.Text.Trim()} ｜ 端口 {TxtPort.Text.Trim()} ｜ {DateTime.Now:yyyy-MM-dd HH:mm}";
        var dlg = new ReportDialog(meta, raw) { Owner = Window.GetWindow(this) };
        dlg.ShowDialog();
    }

    private System.Threading.CancellationTokenSource? _cts;

    private async System.Threading.Tasks.Task RunPickedAsync(string? limitEnv)
    {
        if (_running) return;

        // 在 UI 线程取值后传入后台，避免跨线程访问控件
        var target = TxtTarget.Text;
        var port = TxtPort.Text;
        var runnable = CollectPicked().Where(DiagCmds.CanRun).ToList();
        if (runnable.Count == 0)
        {
            _setStatus("没有可执行的命令——请勾选命令，需要目标的命令请先填写目标主机");
            return;
        }

        // 慢命令（tracert/pathping/续租/扫描等）放最后执行：先出快速结果，避免点击后长时间无反馈
        var ordered = runnable.Where(c => !c.Slow).Concat(runnable.Where(c => c.Slow)).ToList();

        _cts = new System.Threading.CancellationTokenSource();
        var token = _cts.Token;
        _running = true;
        BtnRun.IsEnabled = false;
        BtnStop.IsEnabled = true;
        BtnRun.Content = "执行中…";
        var summary = target.Trim().Length == 0 ? "（未填目标，依赖目标的命令已跳过）" : $"目标 {target.Trim()}";
        AppendResult($"══ 开始执行 {ordered.Count} 条命令 · {summary} · 交互式跳过 · 超时自动终止 ══", AccentBrush(), true);
        try
        {
            IProgress<(string Header, string Output)> progress =
                new Progress<(string Header, string Output)>(r =>
            {
                AppendResult(r.Header, AccentBrush(), true);
                AppendResult(r.Output, null, false);
            });

            await System.Threading.Tasks.Task.Run(() =>
            {
                var idx = 0;
                var limit = limitEnv != null && int.TryParse(limitEnv, out var l) ? (int?)l : null;
                foreach (var c in ordered)
                {
                    if (token.IsCancellationRequested) break;
                    idx++;
                    if (limit is { } lim && idx > lim) break;
                    var cmd = DiagCmds.ExecCommand(c, target, port);
                    var output = DiagCmds.Execute(cmd, c.Slow ? 60_000 : 20_000, token);
                    progress.Report(($"[{idx}/{ordered.Count}] > {cmd}", Truncate(output)));
                }
            });

            AppendResult(token.IsCancellationRequested ? "══ 已停止 ══" : "══ 执行结束 ══", AccentBrush(), true);
            _setStatus(token.IsCancellationRequested
                ? "已停止执行"
                : "本机执行完成，结果见「本机执行结果」区");
        }
        catch (Exception ex)
        {
            AppendResult("执行异常：" + ex.Message, DangerBrush(), true);
            _setStatus("执行异常：" + ex.Message);
        }
        finally
        {
            _running = false;
            BtnRun.IsEnabled = true;
            BtnStop.IsEnabled = false;
            BtnRun.Content = "本机执行";
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>停止执行：正在跑的命令条最多 200ms 内被杀，后续命令不再启动。</summary>
    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        if (!_running || _cts is null) return;
        _cts.Cancel();
        BtnStop.IsEnabled = false;
        BtnStop.Content = "正在停止…";
        AppendResult("（收到停止请求，等待当前命令退出…）", DangerBrush(), true);
    }

    /// <summary>执行结果过长时截断，防止结果区被刷爆。</summary>
    private static string Truncate(string s)
    {
        const int maxLines = 400;
        const int maxChars = 12_000;
        if (string.IsNullOrWhiteSpace(s)) return "（无输出）";
        var cut = false;
        var lines = s.Split('\n');
        if (lines.Length > maxLines)
        {
            s = string.Join('\n', lines.Take(maxLines));
            cut = true;
        }
        if (s.Length > maxChars)
        {
            s = s[..maxChars];
            cut = true;
        }
        return s + (cut ? "\n…（输出过长已截断）" : "");
    }

    private void AppendResult(string text, Brush? brush, bool bold)
    {
        var para = new Paragraph(new Run(text))
        {
            Margin = new Thickness(0, 1, 0, 2),
            Foreground = brush ?? (Brush)FindResource("TextPrimaryBrush"),
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal
        };
        ResultBox.Document.Blocks.Add(para);
        ResultBox.ScrollToEnd();
    }

    private Brush AccentBrush() => (Brush)FindResource("AccentCyanBrush");
    private Brush DangerBrush() => (Brush)FindResource("AccentRedBrush");
}
