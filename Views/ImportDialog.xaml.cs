using System.Windows;
using NetConfigPro.Models;
using NetConfigPro.Services;

namespace NetConfigPro.Views;

/// <summary>导入现网配置：粘贴 running-config → 识别厂商 → 预览回填项 → 应用。</summary>
public partial class ImportDialog : Window
{
    /// <summary>应用后被修改的参数集合（由调用方替换当前 spec）。</summary>
    public ConfigSpec Result { get; }

    /// <summary>识别到的厂商（可空）。</summary>
    public Vendor? DetectedVendor { get; private set; }

    private ParseOutcome _outcome = new();

    public ImportDialog(ConfigSpec workingSpec)
    {
        InitializeComponent();
        Result = workingSpec;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void BtnPaste_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Clipboard.ContainsText()) TxtRaw.Text = Clipboard.GetText();
        }
        catch (Exception ex)
        {
            MessageBox.Show("读取剪贴板失败：" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnSample_Click(object sender, RoutedEventArgs e)
    {
        TxtRaw.Text = @"sysname SW-Core-01
vlan batch 10 20 30
vlan 30
 description Guest
interface Vlanif10
 ip address 192.168.10.1 255.255.255.0
interface Vlanif20
 ip address 192.168.20.1 255.255.255.0
interface GigabitEthernet0/0/1
 port link-type trunk
 port trunk allow-pass vlan 10 to 30
ip route-static 0.0.0.0 0.0.0.0 192.168.10.254
ip route-static 10.0.0.0 255.0.0.0 192.168.20.254
dns server 223.5.5.5
acl number 2000
 rule 5 permit source 192.168.10.0 0.0.0.255
acl number 3000
 rule 10 permit ip
snmp-agent community read cipher public-ro
nat server protocol tcp global 202.96.1.10 8080 inside 192.168.10.5 80
local-user admin password cipher %^%#abc
local-user admin service-type ssh
";
    }

    private void BtnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        var text = TxtRaw.Text ?? "";
        if (text.Trim().Length == 0)
        {
            MessageBox.Show("请先粘贴配置内容。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DetectedVendor = VendorDetect.Detect(text);
        _outcome = ConfigParser.Parse(text, Result);

        ListNotes.Items.Clear();
        foreach (var n in _outcome.Notes) ListNotes.Items.Add(n);
        BtnApply.IsEnabled = _outcome.Applied;

        if (DetectedVendor is { } v)
        {
            TxtVendor.Text = "识别厂商：" + EnumText.VendorName(v);
            TxtHint.Text = _outcome.AppliedCount > 0
                ? $"将回填 {_outcome.AppliedCount} 项（VLAN/静态路由为追加，其余覆盖）"
                : "未能回填——可能为空配置或无法识别，请核对文本";
        }
        else
        {
            TxtVendor.Text = "未能识别厂商";
            TxtVendor.Foreground = (System.Windows.Media.Brush)FindResource("AccentRedBrush");
        }
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        // Result 已在 Analyze 时被就地修改（预览即工作副本），这里直接收尾
        DialogResult = true;
    }
}
