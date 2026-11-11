using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using NetConfigPro.Generators;
using NetConfigPro.Models;
using NetConfigPro.Services;

namespace NetConfigPro.Views;

/// <summary>一键巡检：生成厂商×设备类型适用的特权视图只读诊断命令集。</summary>
public partial class InspectionView : UserControl
{
    private readonly Store _store;
    private readonly Action<string> _setStatus;

    private Vendor _vendor = Vendor.Huawei;
    private DeviceType _deviceType = DeviceType.Router;

    /// <summary>用户勾选的巡检项 key（跨厂商/设备切换保持）。</summary>
    private readonly HashSet<string> _checked = [];

    /// <summary>XAML 解析期默认勾选的 chip 会提前触发事件，此期间命名控件尚未创建，用闸门跳过。</summary>
    private bool _loading = true;

    /// <summary>批量勾选期间挂起自动生成，避免逐项重建输出文档。</summary>
    private bool _suspendGen;

    private string _current = "";

    public InspectionView(Store store, Action<string> setStatus)
    {
        InitializeComponent();
        _store = store;
        _setStatus = setStatus;
        _loading = false;
        RebuildItems();
        GenerateNow();

        // [诊断用] NCP_INS_VENDOR=Huawei/H3C/Cisco/Ruijie、NCP_INS_DT=Router/Firewall/Switch/AccessPoint 自动切换并生成
        if (Environment.GetEnvironmentVariable("NCP_INS_VENDOR") is { } iv &&
            Enum.TryParse<Vendor>(iv, out var nv))
            SetVendor(nv);
        if (Environment.GetEnvironmentVariable("NCP_INS_DT") is { } id &&
            Enum.TryParse<DeviceType>(id, out var nd))
            SetDevice(nd);
    }

    private void SetVendor(Vendor v)
    {
        var chip = v switch
        {
            Vendor.Huawei => VndHuawei,
            Vendor.H3C => VndH3C,
            Vendor.Cisco => VndCisco,
            _ => VndRuijie
        };
        if (!chip.IsChecked!.Value) chip.IsChecked = true;
    }

    private void SetDevice(DeviceType d)
    {
        var chip = d switch
        {
            DeviceType.Router => DevRouter,
            DeviceType.Firewall => DevFirewall,
            DeviceType.Switch => DevSwitch,
            _ => DevAp
        };
        if (!chip.IsChecked!.Value) chip.IsChecked = true;
    }

    // ==================== 从设备清单载入 ====================
    private void BtnLoadDevice_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new DevicePickDialog(_store.Devices,
            "选择后自动带入厂商/设备类型，并把设备名称填入巡检输出头部。")
        { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true || dlg.Selected is not { } dev) return;

        SetVendor(dev.Vendor);
        SetDevice(dev.DeviceType);
        if (!string.IsNullOrWhiteSpace(dev.Name)) TxtHost.Text = dev.Name.Trim();
        _setStatus($"已载入设备「{dev.Name}」（{dev.VendorText} {dev.DeviceText}），巡检命令已按该平台生成");
    }

    // ------------------------------ 厂商 / 设备切换 ------------------------------
    private void Vendor_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is not RadioButton { Tag: string tag }) return;
        _vendor = Enum.Parse<Vendor>(tag);
        GenerateNow();
    }

    private void Device_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is not RadioButton { Tag: string tag }) return;
        _deviceType = Enum.Parse<DeviceType>(tag);
        RebuildItems();
        GenerateNow();
    }

    // ------------------------------ 巡检项目勾选列表 ------------------------------
    /// <summary>按当前设备类型重建巡检项目勾选列表（仅显示适用的项，新出现项默认勾选）。</summary>
    private void RebuildItems()
    {
        GroupHost.Children.Clear();

        var titleStyle = (Style)FindResource("SubTitle");
        foreach (var g in Inspector.Groups)
        {
            var applicable = g.Items.Where(it => Inspector.AppliesTo(it, _deviceType)).ToList();
            if (applicable.Count == 0) continue;

            var gTitle = new TextBlock
            {
                Text = $"{g.Name}",
                Style = titleStyle,
                Margin = new Thickness(0, 4, 0, 6)
            };
            GroupHost.Children.Add(gTitle);

            foreach (var it in applicable)
            {
                var cb = new CheckBox
                {
                    Content = it.Name,
                    Tag = it.Key,
                    ToolTip = it.Tip,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                // 适用项一律默认勾选，并保证集合与 UI 同步（不能用 _checked.Add 的返回值当勾选态：
                // 集合中已存在的项会因此变“未勾选”却仍被生成，造成预览与勾选不一致）
                cb.IsChecked = true;
                _checked.Add(it.Key);
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
        var count = GroupHost.Children.OfType<CheckBox>().Count(cb => cb.IsChecked == true);
        TxtSummary.Text = $"{EnumText.VendorName(_vendor)} · {EnumText.DeviceName(_deviceType)} · 已选 {count} 项巡检";
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e) => SetAll(true);
    private void BtnClearAll_Click(object sender, RoutedEventArgs e) => SetAll(false);

    private void SetAll(bool check)
    {
        // 赋值触发的事件被 _suspendGen 屏蔽，必须按 UI 实际状态重建集合，否则预览与勾选不同步
        _suspendGen = true;
        foreach (var cb in GroupHost.Children.OfType<CheckBox>()) cb.IsChecked = check;
        _checked.Clear();
        foreach (var cb in GroupHost.Children.OfType<CheckBox>())
            if (cb.IsChecked == true && cb.Tag is string key) _checked.Add(key);
        _suspendGen = false;
        UpdateSummary();
        GenerateNow();
    }

    // ------------------------------ 生成 / 复制 / 导出 ------------------------------
    private void TxtHost_TextChanged(object sender, TextChangedEventArgs e) => GenerateNow();

    private void BtnGenerate_Click(object sender, RoutedEventArgs e) => GenerateNow();

    private void GenerateNow()
    {
        if (OutputBox == null || GroupHost == null) return;

        var picked = new List<Inspector.InsItem>();
        foreach (var g in Inspector.Groups)
        foreach (var it in g.Items)
        {
            if (Inspector.AppliesTo(it, _deviceType) && _checked.Contains(it.Key)) picked.Add(it);
        }

        if (picked.Count == 0)
        {
            OutputBox.Document = Highlighter.Render("（请先勾选至少一个巡检项目）");
            TxtStat.Text = "0 条命令";
            _current = "";
            return;
        }

        var text = Inspector.Generate(_vendor, _deviceType, TxtHost.Text, picked);
        _current = text;
        OutputBox.Document = Highlighter.Render(text);

        var annotatedLines = text.Split('\n').Count(l => l.Trim().Length > 0);
        var plainLines = Inspector.StripComments(text, _vendor).Split('\n').Length;
        TxtStat.Text = $"{picked.Count} 条命令 ｜ 纯命令 {plainLines} 行 ｜ 含注释共 {annotatedLines} 行";
        _setStatus($"已生成 {EnumText.VendorName(_vendor)} · {EnumText.DeviceName(_deviceType)} 巡检命令 {picked.Count} 条");
    }

    /// <summary>复制可直接粘贴执行的纯命令（去掉注释/分隔线/空行）。</summary>
    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_current.Length == 0)
        {
            _setStatus("请先勾选巡检项目并生成命令");
            return;
        }
        var plain = Inspector.StripComments(_current, _vendor);
        try
        {
            Clipboard.SetText(plain);
            _setStatus($"已复制 {plain.Split('\n').Length} 条纯命令到剪贴板（可直接粘贴到设备终端）");
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
            _setStatus("请先勾选巡检项目并生成命令");
            return;
        }
        var plain = Inspector.StripComments(_current, _vendor);
        var host = string.IsNullOrWhiteSpace(TxtHost.Text)
            ? ""
            : "_" + TextUtil.SanitizeFileName(TxtHost.Text.Trim());
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出巡检命令",
            Filter = "文本文件 (*.txt)|*.txt",
            FileName = $"巡检_{EnumText.VendorShort(_vendor)}_{EnumText.DeviceName(_deviceType)}{host}.txt"
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                System.IO.File.WriteAllText(dlg.FileName, plain, new System.Text.UTF8Encoding(false));
                _setStatus($"已导出 {plain.Split('\n').Length} 条纯命令：{dlg.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("导出失败：" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
