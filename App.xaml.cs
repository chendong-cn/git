using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using NetConfigPro.Services;

namespace NetConfigPro;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                var dir = Services.AppStorage.DataDir;
                var log = Path.Combine(dir, "crash-" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
                var sb = new StringBuilder();
                sb.AppendLine("时间: " + DateTime.Now);
                sb.AppendLine("异常: " + args.Exception);
                sb.AppendLine();
                sb.AppendLine("堆栈: " + args.Exception.StackTrace);
                File.WriteAllText(log, sb.ToString(), Encoding.UTF8);
                MessageBox.Show(
                    "程序遇到未预期的错误，已停止以避免数据损坏。\n\n" +
                    "错误已记录到：" + log + "\n\n" +
                    "详情：\n" + args.Exception.Message,
                    "NetConfigPro 错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // 日志写入失败时也要给用户提示
                MessageBox.Show("程序遇到未预期的错误：\n" + args.Exception.Message,
                    "NetConfigPro 错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            args.Handled = true;
            Shutdown(-1);
        };

        // 先注入皮肤覆盖字典（默认科技蓝或用户上次选择），再创建 StartupUri 主窗口，
        // 保证窗口中所有 StaticResource 主色键在创建时即解析到所选皮肤。
        ThemeManager.ApplySavedTheme();

        base.OnStartup(e);
    }

    /// <summary>
    /// 即时换肤：写入存档 → 替换覆盖字典 → 重建主窗口（先显示新窗再关闭旧窗，
    /// 避免 ShutdownMode=OnLastWindowClose 在切换瞬间误退出）。
    /// </summary>
    public void ApplyThemeAndRestart(int themeIndex)
    {
        // [诊断用] 一次性模拟换肤，避免重建后的新窗口再次触发造成死循环
        Environment.SetEnvironmentVariable("NCP_SKIN", null);

        ThemeManager.SaveThemeIndex(themeIndex);
        ThemeManager.Apply(themeIndex);

        var oldWin = MainWindow;
        var fresh = new Views.MainWindow
        {
            WindowStartupLocation = oldWin is { WindowState: WindowState.Normal }
                ? WindowStartupLocation.Manual
                : WindowStartupLocation.CenterScreen
        };
        if (oldWin is { WindowState: WindowState.Normal } && oldWin.WindowState != WindowState.Maximized)
        {
            fresh.Left = oldWin.Left;
            fresh.Top = oldWin.Top;
            fresh.Width = oldWin.ActualWidth;
            fresh.Height = oldWin.ActualHeight;
            fresh.WindowState = oldWin.WindowState;
        }
        else
        {
            fresh.WindowState = oldWin is { WindowState: WindowState.Maximized }
                ? WindowState.Maximized
                : WindowState.Normal;
        }

        fresh.Show();
        oldWin?.Close();
    }
}
