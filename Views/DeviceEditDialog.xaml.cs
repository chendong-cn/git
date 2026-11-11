using System.Windows;
using NetConfigPro.Models;
using NetConfigPro.Services;

namespace NetConfigPro.Views;

public partial class DeviceEditDialog : Window
{
    public DeviceEntry Result { get; }

    public DeviceEditDialog(DeviceEntry? device)
    {
        InitializeComponent();

        CmbVendor.ItemsSource = Enum.GetValues<Vendor>();
        CmbType.ItemsSource = Enum.GetValues<DeviceType>();

        Result = device is null ? new DeviceEntry() : AppStorage.Clone(device);
        DataContext = Result;
        TxtPassword.Password = AppStorage.Unprotect(Result.EncryptedPassword);
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Result.Name))
        {
            MessageBox.Show("请填写设备名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        // 端口与 IP 直接从输入框取（避免 int 绑定转换失败时静默保留旧值）
        var ip = TxtIp.Text.Trim();
        if (ip.Length > 0 && !IsIpV4(ip))
        {
            MessageBox.Show($"管理 IP 格式不正确：{ip}\n示例：192.168.1.1（留空表示暂不填）", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!int.TryParse(TxtPort.Text.Trim(), out var port) || port is < 1 or > 65535)
        {
            MessageBox.Show($"SSH 端口需为 1~65535 的整数：{TxtPort.Text.Trim()}", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Result.Ip = ip;
        Result.Port = port;
        Result.EncryptedPassword = AppStorage.Protect(TxtPassword.Password);
        DialogResult = true;
    }

    /// <summary>轻量 IPv4 校验（避免反向引用生成器命名空间）。</summary>
    private static bool IsIpV4(string s)
    {
        var parts = s.Split('.');
        return parts.Length == 4 && parts.All(p =>
            p.Length is >= 1 and <= 3 && byte.TryParse(p, out var b) && (p.Length == 1 || b.ToString() == p));
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
