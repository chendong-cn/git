using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace NetConfigPro.Views;

public partial class TextViewDialog : Window
{
    public TextViewDialog(string title, string content)
    {
        InitializeComponent();
        TxtTitle.Text = title;
        TxtContent.Text = content;
        TxtStat.Text = $"{content.Split('\n').Length} 行 · {content.Length} 字符";
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(TxtContent.Text);
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            FileName = $"config_{DateTime.Now:yyyyMMdd_HHmm}.cfg",
            DefaultExt = ".cfg",
            Filter = "配置文件 (*.cfg)|*.cfg|文本文件 (*.txt)|*.txt"
        };
        if (dlg.ShowDialog() != true) return;

        File.WriteAllText(dlg.FileName, TxtContent.Text, Encoding.UTF8);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
