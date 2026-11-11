using System.Windows;
using System.Windows.Controls;
using NetConfigPro.Models;

namespace NetConfigPro.Views;

/// <summary>从设备清单选一台设备，供生成器 / 巡检页自动带入厂商、设备类型与基础信息。</summary>
public partial class DevicePickDialog : Window
{
    public DeviceEntry? Selected { get; private set; }

    public DevicePickDialog(IEnumerable<DeviceEntry> devices, string tip)
    {
        InitializeComponent();
        TxtTip.Text = tip;
        var list = devices.OrderByDescending(d => d.CreatedAt).ToList();
        if (list.Count == 0)
        {
            LstDevices.IsEnabled = false;
            TxtTip.Text = "设备清单为空，请先到「设备管理」添加设备，再回到这里载入。";
        }
        LstDevices.ItemsSource = list;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (LstDevices.SelectedItem is not DeviceEntry dev)
        {
            MessageBox.Show("请先选择一台设备。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Selected = dev;
        DialogResult = true;
    }

    private void LstDevices_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (LstDevices.SelectedItem is DeviceEntry) BtnOk_Click(sender, e);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
