using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NetConfigPro.Services;

namespace NetConfigPro.Views;

/// <summary>清单中的一个待打包条目。</summary>
public sealed class PackItem
{
    public bool IsSelected { get; set; }
    public string Name { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Content { get; set; } = "";
}

/// <summary>一键交付包：勾选配置 → 自动生成交付说明 → 打包 ZIP。</summary>
public partial class PackView : UserControl
{
    private readonly Store _store;
    private readonly Action<string> _setStatus;
    private readonly ObservableCollection<PackItem> _items = [];

    public PackView(Store store, Action<string> setStatus)
    {
        InitializeComponent();
        _store = store;
        _setStatus = setStatus;
        GridPack.ItemsSource = _items;
        TxtPackName.Text = $"配置交付包_{DateTime.Now:yyyyMMdd_HHmm}";
    }

    public void Refresh() => LoadVersions();

    // ------------------------------ 清单来源 ------------------------------
    private void BtnLoadVersions_Click(object sender, RoutedEventArgs e) => LoadVersions();

    private void LoadVersions()
    {
        var byDevice = _store.Versions
            .GroupBy(v => v.DeviceName)
            .OrderByDescending(g => g.Max(v => v.CreatedAt));
        _items.Clear();
        var added = 0;
        foreach (var g in byDevice)
        {
            foreach (var v in g.Take(5).OrderBy(x => x.VersionNo)) // 每台设备取最近 5 个版本
            {
                _items.Add(new PackItem
                {
                    IsSelected = added < 6, // 默认勾选最新的几份（按设备分组）
                    Name = v.DeviceName.Length == 0 ? $"v{v.VersionNo}" : v.DeviceName,
                    Detail = $"v{v.VersionNo} · {v.CreatedAt:MM-dd HH:mm} · {v.Content.Split('\n').Length} 行",
                    Content = v.Content
                });
                added++;
            }
        }
        if (added == 0) TxtStatus.Text = "版本库为空——先在「配置生成器」里把配置「存为版本」，或点左侧“从文件添加”";
        RefreshSummary();
    }

    private void BtnAddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "配置文件 (*.cfg;*.txt)|*.cfg;*.txt|所有文件 (*.*)|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true) return;
        foreach (var f in dlg.FileNames)
        {
            try
            {
                _items.Add(new PackItem
                {
                    IsSelected = true,
                    Name = Path.GetFileNameWithoutExtension(f),
                    Detail = $"文件 · {Path.GetFileName(f)} · {new FileInfo(f).Length / 1024.0:F1} KB",
                    Content = File.ReadAllText(f, Encoding.UTF8)
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取失败 {Path.GetFileName(f)}：{ex.Message}", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        RefreshSummary();
    }

    // ------------------------------ 清单操作 ------------------------------
    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var it in _items) it.IsSelected = true;
        GridPack.Items.Refresh();
        RefreshSummary();
    }

    private void BtnClearSel_Click(object sender, RoutedEventArgs e)
    {
        foreach (var it in _items) it.IsSelected = false;
        GridPack.Items.Refresh();
        RefreshSummary();
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        var targets = _items.Where(i => i.IsSelected).ToList();
        foreach (var t in targets) _items.Remove(t);
        RefreshSummary();
    }

    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        _items.Clear();
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        var chosen = _items.Where(i => i.IsSelected).ToList();
        var lines = chosen.Sum(i => i.Content.Split('\n').Length);
        TxtSummary.Text = $"清单共 {_items.Count} 项 ｜ 将打包 {chosen.Count} 项（合计约 {lines} 行）";
        TxtStatus.Text = "";
        RefreshReadme();
    }

    // ------------------------------ 说明 / 打包 ------------------------------
    private void RefreshReadme()
    {
        var chosen = _items.Where(i => i.IsSelected).ToList();
        if (chosen.Count == 0)
        {
            TxtReadme.Text = "（勾选配置后自动生成交付说明）";
            TxtPreview.Text = "";
            return;
        }
        var sb = new StringBuilder();
        sb.Append("交付包内容（").Append(chosen.Count).Append(" 项，").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).AppendLine("）：");
        foreach (var it in chosen)
            sb.Append("  · ").Append(it.Name).Append("  ").Append(it.Detail).AppendLine();
        sb.AppendLine("请按实际设备型号与软件版本核对后再下发；生产设备操作请做好备份。");
        TxtReadme.Text = $"将打包 {chosen.Count} 项配置 + 交付说明.txt";
        TxtPreview.Text = sb.ToString();
    }

    private string BuildReadme()
    {
        var chosen = _items.Where(i => i.IsSelected).ToList();
        var sb = new StringBuilder();
        sb.Append("════════════════════════════════════════════\n");
        sb.Append("  网络设备配置交付包\n");
        sb.Append("  生成工具：NetConfigPro（PCXX）\n");
        sb.Append("  生成时间：").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
        sb.Append("════════════════════════════════════════════\n\n");
        sb.Append("一、包内文件清单\n");
        for (var i = 0; i < chosen.Count; i++)
            sb.Append($"  {i + 1}. {TextUtil.SanitizeFileName(chosen[i].Name)}.cfg  （{chosen[i].Detail}）\n");
        sb.Append("\n二、使用说明\n");
        sb.Append("  1. 每个 .cfg 为可直接粘贴到设备终端的配置命令（已去除注释）；\n");
        sb.Append("  2. 粘贴前请先登录设备并进入配置视图（system-view / configure terminal）；\n");
        sb.Append("  3. 上真机前务必按实际设备型号与软件版本核对命令（不同版本存在差异）；\n");
        sb.Append("  4. 生产设备操作前请先备份现网配置；\n");
        sb.Append("  5. 配置保存命令已含在文件尾部（save / write），完成后请复核 running-config。\n\n");
        sb.Append("三、本次交付配置概要\n");
        foreach (var it in chosen)
            sb.Append($"  · {it.Name}：{it.Content.Split('\n').Length} 行\n");
        return sb.ToString();
    }

    private void BtnPack_Click(object sender, RoutedEventArgs e)
    {
        var chosen = _items.Where(i => i.IsSelected).ToList();
        if (chosen.Count == 0)
        {
            MessageBox.Show("请先在左侧勾选要打包的配置（可点“载入版本库”或“从文件添加”）。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = "导出交付包",
            Filter = "ZIP 压缩包 (*.zip)|*.zip",
            FileName = TextUtil.SanitizeFileName(TxtPackName.Text.Trim().Length == 0 ? "配置交付包" : TxtPackName.Text.Trim())
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            using var zip = ZipFile.Open(dlg.FileName, ZipArchiveMode.Create);
            var seen = new HashSet<string>();
            for (var i = 0; i < chosen.Count; i++)
            {
                var name = TextUtil.SanitizeFileName(chosen[i].Name);
                if (name.Length == 0) name = "配置";
                var baseName = $"{i + 1:D2}_{name}.cfg";
                var entry = baseName;
                for (var k = 1; !seen.Add(entry); k++) entry = $"{i + 1:D2}_{name}_{k}.cfg";
                var e1 = zip.CreateEntry(entry);
                using var sw = new StreamWriter(e1.Open(), new UTF8Encoding(false));
                sw.Write(chosen[i].Content);
            }
            var readme = zip.CreateEntry("交付说明.txt");
            using (var sw = new StreamWriter(readme.Open(), new UTF8Encoding(true)))
                sw.Write(BuildReadme());
        }
        catch (Exception ex)
        {
            MessageBox.Show("打包失败：" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TxtStatus.Text = $"✓ 已生成 {chosen.Count} 项配置的交付包";
        _setStatus($"交付包已导出：{dlg.FileName}");
        MessageBox.Show($"交付包已生成：\n{dlg.FileName}\n\n包内含 {chosen.Count} 份配置 + 交付说明.txt，可直接发送给客户。",
            "完成", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
