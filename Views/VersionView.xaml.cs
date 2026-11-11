using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NetConfigPro.Models;
using NetConfigPro.Services;

namespace NetConfigPro.Views;

public partial class VersionView : UserControl, IRefreshable
{
    private readonly Store _store;
    private readonly Action<string> _setStatus;

    public VersionView(Store store, Action<string> setStatus)
    {
        InitializeComponent();
        _store = store;
        _setStatus = setStatus;
        GridVersions.ItemsSource = _store.Versions;
    }

    public void Refresh() => GridVersions.Items.Refresh();

    private void BtnView_Click(object sender, RoutedEventArgs e)
    {
        if (GridVersions.SelectedItem is not ConfigVersion ver)
        {
            _setStatus("请先选择一个版本");
            return;
        }
        new TextViewDialog($"{ver.DeviceName} v{ver.VersionNo}", ver.Content) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private void GridVersions_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (GridVersions.SelectedItem is ConfigVersion) BtnView_Click(sender, e);
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (GridVersions.SelectedItem is not ConfigVersion ver)
        {
            _setStatus("请先选择一个版本");
            return;
        }
        Clipboard.SetText(ver.Content);
        _setStatus($"已复制 {ver.DeviceName} v{ver.VersionNo} 的配置");
    }

    private void BtnToLeft_Click(object sender, RoutedEventArgs e) => LoadToCompare(isLeft: true);
    private void BtnToRight_Click(object sender, RoutedEventArgs e) => LoadToCompare(isLeft: false);

    private void LoadToCompare(bool isLeft)
    {
        if (GridVersions.SelectedItem is not ConfigVersion ver)
        {
            _setStatus("请先选择一个版本");
            return;
        }

        if (isLeft) AppState.CompareLeft = ver.Content;
        else AppState.CompareRight = ver.Content;

        if (Window.GetWindow(this) is MainWindow main) main.NavigateTo("compare");
        _setStatus($"已载入对比{(isLeft ? "左" : "右")}侧：{ver.DeviceName} v{ver.VersionNo}");
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (GridVersions.SelectedItem is not ConfigVersion ver)
        {
            _setStatus("请先选择一个版本");
            return;
        }

        var dlg = new SaveFileDialog
        {
            FileName = $"{TextUtil.SanitizeFileName(ver.DeviceName)}_v{ver.VersionNo}.cfg",
            DefaultExt = ".cfg",
            Filter = "配置文件 (*.cfg)|*.cfg|文本文件 (*.txt)|*.txt"
        };
        if (dlg.ShowDialog() != true) return;

        File.WriteAllText(dlg.FileName, ver.Content, Encoding.UTF8);
        _setStatus($"已导出：{dlg.FileName}");
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        var targets = GridVersions.SelectedItems.OfType<ConfigVersion>().ToList();
        if (targets.Count == 0)
        {
            _setStatus("请先选择要删除的版本");
            return;
        }

        if (MessageBox.Show($"确定删除选中的 {targets.Count} 个版本吗？", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        foreach (var v in targets) _store.Versions.Remove(v);
        _store.SaveVersions();
        _setStatus($"已删除 {targets.Count} 个版本");
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        _store.LoadAll();
        GridVersions.ItemsSource = null;
        GridVersions.ItemsSource = _store.Versions;
        _setStatus("版本列表已刷新");
    }
}
