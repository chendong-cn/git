using System.Windows;

namespace NetConfigPro.Views;

public partial class PromptDialog : Window
{
    public string InputText => TxtInput.Text.Trim();

    public PromptDialog(string title, string prompt, string defaultValue = "")
    {
        InitializeComponent();
        TxtTitle.Text = title;
        TxtPrompt.Text = prompt;
        TxtInput.Text = defaultValue;
        Loaded += (_, _) =>
        {
            TxtInput.Focus();
            TxtInput.SelectAll();
        };
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
