using System.Windows;
using System.Windows.Controls;
using NetConfigPro.Services;

namespace NetConfigPro.Views;

public partial class MainWindow : Window
{
    private readonly Store _store = new();
    private readonly Dictionary<string, UserControl> _pages = new();

    /// <summary>XAML 解析阶段默认勾选的导航按钮会提前触发事件，此时 PageHost 尚未创建，用闸门跳过。</summary>
    private bool _loading = true;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            UpdateStats();
            // 数据文件损坏恢复提示（有则显示，避免用户误以为数据凭空丢失）
            if (AppStorage.RecoveryNotes.Count > 0)
                StatusText.Text = "⚠ " + AppStorage.RecoveryNotes[0];
            // [诊断用] NCP_PAGE=generator/devices/deploy/inspection/diag/compare/templates/versions/pack/scan 自动跳页
            if (Environment.GetEnvironmentVariable("NCP_PAGE") is { } page &&
                page is "generator" or "devices" or "deploy" or "inspection" or "diag" or "compare" or "templates" or "versions" or "pack" or "scan")
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                    () => NavigateTo(page));
            }
            // [诊断用] NCP_SKIN=0..31 模拟"点选皮肤 → 即时换肤重建"完整路径
            if (Environment.GetEnvironmentVariable("NCP_SKIN") is { } skin &&
                int.TryParse(skin, out var skinIdx) && skinIdx >= 0 && skinIdx < 32)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                    () => ((App)System.Windows.Application.Current).ApplyThemeAndRestart(skinIdx));
            }
        };

        _loading = false;
        Nav_Checked(NavGenerator, new RoutedEventArgs()); // 显式载入默认页（配置生成器）
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is not RadioButton rb || rb.Tag is not string key) return;

        if (!_pages.TryGetValue(key, out var page))
        {
            page = key switch
            {
                "generator" => new GeneratorView(_store, SetStatus),
                "devices" => new DevicesView(_store, SetStatus),
                "deploy" => new DeployView(_store, SetStatus),
                "inspection" => new InspectionView(_store, SetStatus),
                "diag" => new DiagView(_store, SetStatus),
                "compare" => new CompareView(_store, SetStatus),
                "templates" => new TemplateView(_store, SetStatus),
                "versions" => new VersionView(_store, SetStatus),
                "pack" => new PackView(_store, SetStatus),
                "scan" => new ScanView(_store, SetStatus),
                _ => throw new InvalidOperationException($"未知页面：{key}")
            };
            _pages[key] = page;
        }

        PageHost.Content = page;

        if (page is IRefreshable refreshable) refreshable.Refresh();

        SetStatus(key switch
        {
            "generator" => "配置生成器：选择厂商与设备类型，勾选功能模块后生成配置",
            "devices" => "设备管理：维护可批量操作的设备资产清单",
            "deploy" => "批量部署：把配置批量下发到目标设备",
            "inspection" => "一键巡检：按厂商×设备生成特权视图只读巡检命令集",
            "diag" => "网络命令：Windows 本机诊断命令（ping/tracert/arp/netstat/nslookup…），可生成也可直接执行",
            "compare" => "配置对比：对比两份配置的差异",
            "templates" => "模板库：保存与复用参数集合",
            "versions" => "版本管理：保存与回滚历史配置",
            "pack" => "一键交付包：勾选配置打包 ZIP 并自动附带交付说明，发给客户零手工整理",
            "scan" => "IP-MAC 扫描：扫网段列出在线主机 IP↔MAC 并识别厂商，排查 IP 冲突与未知设备",
            _ => "就绪"
        });
        UpdateStats();
    }

    public void SetStatus(string text) => StatusText.Text = text;

    /// <summary>供其他页面跳转到指定功能页。</summary>
    public void NavigateTo(string key)
    {
        var target = key switch
        {
            "generator" => NavGenerator,
            "devices" => NavDevices,
            "deploy" => NavDeploy,
            "inspection" => NavInspection,
            "diag" => NavDiag,
            "compare" => NavCompare,
            "templates" => NavTemplates,
            "versions" => NavVersions,
            "pack" => NavPack,
            "scan" => NavScan,
            _ => null
        };
        if (target != null) target.IsChecked = true;
    }

    private void UpdateStats()
    {
        StatText.Text = $"设备 {_store.Devices.Count} 台  |  模板 {_store.Templates.Count} 个  |  版本 {_store.Versions.Count} 条  |  数据目录 {AppStorage.DataDir}";
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            RootBorder.CornerRadius = WindowState == WindowState.Maximized ? new CornerRadius(0) : new CornerRadius(10);
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        RootBorder.CornerRadius = WindowState == WindowState.Maximized ? new CornerRadius(0) : new CornerRadius(10);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>打开 32 色皮肤选择器；选中不同主色后即时应用（重建窗口）。</summary>
    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SkinDialog(ThemeManager.CurrentIndex) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.SelectedIndex != ThemeManager.CurrentIndex)
        {
            ((App)System.Windows.Application.Current).ApplyThemeAndRestart(dlg.SelectedIndex);
        }
    }
}

/// <summary>页面切换时用于刷新数据的接口。</summary>
public interface IRefreshable
{
    void Refresh();
}
