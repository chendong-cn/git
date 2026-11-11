using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NetConfigPro.Models;
using NetConfigPro.Services;

namespace NetConfigPro.Views;

public partial class DevicesView : UserControl, IRefreshable
{
    private readonly Store _store;
    private readonly Action<string> _setStatus;

    public DevicesView(Store store, Action<string> setStatus)
    {
        InitializeComponent();
        _store = store;
        _setStatus = setStatus;
        GridDevices.ItemsSource = _store.Devices;
        Refresh();
    }

    public void Refresh() => TxtCount.Text = $"共 {_store.Devices.Count} 台设备";

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new DeviceEditDialog(null) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;

        _store.Devices.Add(dlg.Result);
        _store.SaveDevices();
        Refresh();
        _setStatus($"已新增设备：{dlg.Result.Name}");
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (GridDevices.SelectedItem is not DeviceEntry item)
        {
            _setStatus("请先选择一台设备");
            return;
        }
        EditDevice(item);
    }

    private void GridDevices_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (GridDevices.SelectedItem is DeviceEntry item) EditDevice(item);
    }

    private void EditDevice(DeviceEntry item)
    {
        var dlg = new DeviceEditDialog(item) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;

        var index = _store.Devices.IndexOf(item);
        if (index >= 0) _store.Devices[index] = dlg.Result;
        _store.SaveDevices();
        _setStatus($"已更新设备：{dlg.Result.Name}");
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        var targets = GridDevices.SelectedItems.OfType<DeviceEntry>().ToList();
        if (targets.Count == 0)
        {
            _setStatus("请先选择要删除的设备");
            return;
        }

        if (MessageBox.Show($"确定删除选中的 {targets.Count} 台设备吗？", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        foreach (var t in targets) _store.Devices.Remove(t);
        _store.SaveDevices();
        Refresh();
        _setStatus($"已删除 {targets.Count} 台设备");
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (_store.Devices.Count == 0)
        {
            _setStatus("设备列表为空，无法导出");
            return;
        }

        var dlg = new SaveFileDialog
        {
            FileName = $"devices_{DateTime.Now:yyyyMMdd}.csv",
            DefaultExt = ".csv",
            Filter = "CSV 文件 (*.csv)|*.csv"
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder("设备名称,厂商,设备类型,型号,管理IP,端口,用户名,备注\n");
        foreach (var d in _store.Devices)
            sb.AppendLine(string.Join(',',
                TextUtil.EscapeCsv(d.Name), TextUtil.EscapeCsv(d.VendorText), TextUtil.EscapeCsv(d.DeviceText),
                TextUtil.EscapeCsv(d.Model), TextUtil.EscapeCsv(d.Ip), TextUtil.EscapeCsv(d.Port.ToString()),
                TextUtil.EscapeCsv(d.Username), TextUtil.EscapeCsv(d.Note)));

        // 带 BOM 的 UTF-8：Excel 直接打开中文不乱码
        File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
        _setStatus($"已导出：{dlg.FileName}");
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        _store.LoadAll();
        GridDevices.ItemsSource = null;
        GridDevices.ItemsSource = _store.Devices;
        Refresh();
        _setStatus("设备列表已刷新");
    }
}
