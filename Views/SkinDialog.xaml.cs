using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NetConfigPro.Services;

namespace NetConfigPro.Views;

/// <summary>32 色皮肤选择弹窗：点击色块即选中并关闭，由主窗口触发换肤重建。</summary>
public partial class SkinDialog : Window
{
    private readonly int _startIndex;

    public int SelectedIndex { get; private set; }

    public SkinDialog(int currentIndex)
    {
        InitializeComponent();
        _startIndex = currentIndex;
        SelectedIndex = currentIndex;
        BuildSwatches();
        UpdateLabel(currentIndex);
    }

    private void BuildSwatches()
    {
        for (var i = 0; i < ThemeManager.Options.Count; i++)
        {
            var opt = ThemeManager.Options[i];
            var ring = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(22),
                BorderThickness = new Thickness(i == _startIndex ? 2 : 0),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x1C, 0x2B, 0x46)),
                Background = Brushes.Transparent,
                Tag = i,
                Cursor = Cursors.Hand
            };
            var dot = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(17),
                Background = new SolidColorBrush(opt.Main),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ring.Child = dot;
            ring.MouseLeftButtonUp += Swatch_MouseLeftButtonUp;
            var idx = i; // for 循环变量为共享引用，hover 回调必须捕获副本（否则循环结束后 i=Count 越界）
            ring.MouseEnter += (_, _) => UpdateLabel(idx);
            ring.ToolTip = $"{opt.Name}\n{ThemeManager.Hex(opt.Main)}";
            SwatchGrid.Children.Add(ring);
        }
    }

    private void Swatch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: int idx }) return;
        SelectedIndex = idx;
        DialogResult = true; // 关闭弹窗，交由主窗口执行换肤重建
    }

    private void UpdateLabel(int index)
    {
        // 防御：hover 事件与构建可能交错，越界时忽略
        if (index < 0 || index >= ThemeManager.Options.Count) return;
        var opt = ThemeManager.Options[index];
        SkinName.Text = $"{opt.Name} · {ThemeManager.Hex(opt.Main)}";
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1) DragMove();
    }
}
