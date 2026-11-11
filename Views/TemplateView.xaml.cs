using System.Windows;
using System.Windows.Controls;
using NetConfigPro.Generators;
using NetConfigPro.Models;
using NetConfigPro.Services;

namespace NetConfigPro.Views;

public partial class TemplateView : UserControl, IRefreshable
{
    private readonly Store _store;
    private readonly Action<string> _setStatus;

    public TemplateView(Store store, Action<string> setStatus)
    {
        InitializeComponent();
        _store = store;
        _setStatus = setStatus;
        GridTemplates.ItemsSource = _store.Templates;
    }

    public void Refresh() => GridTemplates.Items.Refresh();

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        if (GridTemplates.SelectedItem is not ConfigTemplate tpl)
        {
            _setStatus("请先选择一个模板");
            return;
        }

        var spec = AppStorage.Deserialize<ConfigSpec>(tpl.SpecJson);
        if (spec is null)
        {
            _setStatus("模板数据损坏，无法应用");
            return;
        }

        AppState.PendingSpec = spec;
        AppState.PendingVendor = tpl.Vendor;
        AppState.PendingDeviceType = tpl.DeviceType;

        if (Window.GetWindow(this) is MainWindow main) main.NavigateTo("generator");
        _setStatus($"模板「{tpl.Name}」已载入生成器");
    }

    private void BtnPreview_Click(object sender, RoutedEventArgs e)
    {
        if (GridTemplates.SelectedItem is not ConfigTemplate tpl) return;

        var spec = AppStorage.Deserialize<ConfigSpec>(tpl.SpecJson);
        if (spec is null) return;

        var text = GeneratorFactory.Get(tpl.Vendor).Generate(spec, tpl.DeviceType);
        new TextViewDialog($"模板预览 - {tpl.Name}", text) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private void GridTemplates_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (GridTemplates.SelectedItem is ConfigTemplate) BtnApply_Click(sender, e);
    }

    private void BtnRename_Click(object sender, RoutedEventArgs e)
    {
        if (GridTemplates.SelectedItem is not ConfigTemplate tpl)
        {
            _setStatus("请先选择一个模板");
            return;
        }

        var dlg = new PromptDialog("重命名模板", "模板名称：", tpl.Name) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true || dlg.InputText.Length == 0) return;

        tpl.Name = dlg.InputText;
        tpl.UpdatedAt = DateTime.Now;
        _store.SaveTemplates();
        GridTemplates.Items.Refresh();
        _setStatus("模板已重命名");
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        var targets = GridTemplates.SelectedItems.OfType<ConfigTemplate>().ToList();
        if (targets.Count == 0)
        {
            _setStatus("请先选择要删除的模板");
            return;
        }

        if (MessageBox.Show($"确定删除选中的 {targets.Count} 个模板吗？", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        foreach (var t in targets) _store.Templates.Remove(t);
        _store.SaveTemplates();
        _setStatus($"已删除 {targets.Count} 个模板");
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        _store.LoadAll();
        GridTemplates.ItemsSource = null;
        GridTemplates.ItemsSource = _store.Templates;
        _setStatus("模板列表已刷新");
    }
}
