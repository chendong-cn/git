using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using NetConfigPro.Services;

namespace NetConfigPro.Views;

/// <summary>命令手册弹窗：主题树 + 语法说明。</summary>
public partial class HelpDialog : Window
{
    public HelpDialog()
    {
        InitializeComponent();
        foreach (var t in HelpDb.Topics)
        {
            var item = new ListBoxItem { Content = t.Title, Tag = t };
            TopicList.Items.Add(item);
        }
        if (TopicList.Items.Count > 0) TopicList.SelectedIndex = 0;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void TopicList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TopicList.SelectedItem is not ListBoxItem { Tag: HelpTopic topic }) return;
        ContentScroll.Content = BuildTopic(topic);
        ContentScroll.ScrollToTop();
    }

    private static UIElement BuildTopic(HelpTopic topic)
    {
        var stack = new StackPanel();
        var accent = Application.Current.FindResource("AccentCyanBrush") as Brush ?? Brushes.SteelBlue;
        var dim = Application.Current.FindResource("TextDimBrush") as Brush ?? Brushes.Gray;
        var primary = Application.Current.FindResource("TextPrimaryBrush") as Brush ?? Brushes.Black;
        var mono = (FontFamily)Application.Current.FindResource("MonoFont");

        var head = new TextBlock
        {
            Text = topic.Title,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = accent,
            Margin = new Thickness(0, 0, 0, 12)
        };
        stack.Children.Add(head);

        foreach (var entry in topic.Entries)
        {
            var name = new TextBlock
            {
                Text = entry.Name,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = primary,
                Margin = new Thickness(0, 10, 0, 5)
            };
            stack.Children.Add(name);

            // 命令块：可选中复制的只读文本框
            var cmdBox = new TextBox
            {
                Text = entry.Cmd,
                IsReadOnly = true,
                FontFamily = mono,
                FontSize = 11.5,
                Foreground = primary,
                Background = (Brush)(Application.Current.FindResource("OutputBgBrush") ?? Brushes.White),
                BorderBrush = (Brush)(Application.Current.FindResource("BorderBrush") ?? Brushes.Gray),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stack.Children.Add(cmdBox);

            var note = new TextBlock
            {
                Text = entry.Note,
                FontSize = 11,
                Foreground = dim,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 0, 0, 2)
            };
            stack.Children.Add(note);
        }
        return stack;
    }
}
