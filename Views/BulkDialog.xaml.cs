using System.Windows;
using System.Windows.Controls;
using NetConfigPro.Services;

namespace NetConfigPro.Views;

/// <summary>批量生成参数：份数 / 名称前缀 / 起始管理 IP / 输出目录。</summary>
public partial class BulkDialog : Window
{
    public int Count { get; private set; }
    public string Prefix { get; private set; } = "";
    public string StartIp { get; private set; } = "";
    public string OutDir { get; private set; } = "";

    public BulkDialog(string currentHostname)
    {
        InitializeComponent();
        TxtPrefix.Text = currentHostname.Replace("-", "").Trim() + "-"; // 赋值即触发 TextChanged 刷新提示
        TxtDir.Text = System.IO.Path.Combine(AppStorage.DataDir, "batch");
    }

    private void TxtPrefix_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TxtTip == null) return;
        var prefix = string.IsNullOrWhiteSpace(TxtPrefix.Text) ? "dev" : TxtPrefix.Text.Trim();
        if (int.TryParse(TxtCount.Text, out var n) && n >= 1 && n <= 99)
            TxtTip.Text = $"输出文件名：{prefix}1.cfg … {prefix}{n}.cfg；每份自动替换主机名与递增管理 IP。文件为纯命令（可直接下发）。";
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "选择批量输出目录" };
        if (dlg.ShowDialog(this) == true) TxtDir.Text = dlg.FolderName;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TxtCount.Text, out var n) || n < 1 || n > 99)
        {
            MessageBox.Show("生成份数需为 1~99 之间的整数。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(TxtPrefix.Text))
        {
            MessageBox.Show("请填写名称前缀。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(TxtDir.Text))
        {
            MessageBox.Show("请选择输出目录。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Count = n;
        Prefix = TxtPrefix.Text.Trim();
        StartIp = TxtStartIp.Text.Trim();
        OutDir = TxtDir.Text.Trim();
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
