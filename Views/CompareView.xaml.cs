using System.Windows;
using System.Windows.Controls;
using NetConfigPro.Models;
using NetConfigPro.Services;

namespace NetConfigPro.Views;

public partial class CompareView : UserControl, IRefreshable
{
    private readonly Store _store;
    private readonly Action<string> _setStatus;
    private List<DiffLine> _diff = new();

    public CompareView(Store store, Action<string> setStatus)
    {
        InitializeComponent();
        _store = store;
        _setStatus = setStatus;
        Refresh();
    }

    public void Refresh()
    {
        CmbLeft.ItemsSource = null;
        CmbLeft.ItemsSource = _store.Versions;
        CmbRight.ItemsSource = null;
        CmbRight.ItemsSource = _store.Versions;

        // 承接版本页「加载到对比」传入的内容
        if (AppState.CompareLeft is not null)
        {
            TxtLeft.Text = AppState.CompareLeft;
            AppState.CompareLeft = null;
        }
        if (AppState.CompareRight is not null)
        {
            TxtRight.Text = AppState.CompareRight;
            AppState.CompareRight = null;
        }
    }

    private void CmbLeft_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbLeft.SelectedItem is ConfigVersion v) TxtLeft.Text = v.Content;
    }

    private void CmbRight_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbRight.SelectedItem is ConfigVersion v) TxtRight.Text = v.Content;
    }

    private void BtnCompare_Click(object sender, RoutedEventArgs e)
    {
        if (TxtLeft.Text.Trim().Length == 0 && TxtRight.Text.Trim().Length == 0)
        {
            _setStatus("请先载入两侧配置内容");
            return;
        }

        _diff = DiffService.Compare(TxtLeft.Text, TxtRight.Text);
        var (added, removed, same) = DiffService.Stats(_diff);
        TxtStat.Text = $"差异统计：新增 {added} 行 · 删除 {removed} 行 · 相同 {same} 行";

        ApplyFilter();
        _setStatus($"对比完成：新增 {added} 行，删除 {removed} 行");
    }

    private void ChkOnlyDiff_Changed(object sender, RoutedEventArgs e) => ApplyFilter();

    private void ApplyFilter() =>
        ListDiff.ItemsSource = ChkOnlyDiff.IsChecked == true
            ? _diff.Where(x => x.Kind != DiffKind.Same).ToList()
            : _diff;

    private void BtnSwap_Click(object sender, RoutedEventArgs e)
    {
        (TxtLeft.Text, TxtRight.Text) = (TxtRight.Text, TxtLeft.Text);
        if (_diff.Count > 0) BtnCompare_Click(sender, e);
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        TxtLeft.Clear();
        TxtRight.Clear();
        _diff.Clear();
        ListDiff.ItemsSource = null;
        TxtStat.Text = "差异统计：—";
        CmbLeft.SelectedIndex = -1;
        CmbRight.SelectedIndex = -1;
    }
}
