using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NetConfigPro.Generators;
using NetConfigPro.Models;
using NetConfigPro.Services;

namespace NetConfigPro.Views;

public partial class GeneratorView : UserControl
{
    private readonly Store _store;
    private readonly Action<string> _setStatus;
    private readonly ObservableCollection<ModuleItem> _modules = new();
    private ConfigSpec _spec = new();

    private Vendor _vendor = Vendor.Huawei;
    private DeviceType _deviceType = DeviceType.Switch;
    private string _currentConfig = "";
    private List<RecentProfile> _recents = [];

    /// <summary>XAML 解析阶段勾选的 RadioButton 会提前触发事件，此时命名控件尚未全部创建，
    /// 用该闸门跳过初始化期间的事件处理，默认状态由构造函数末尾显式调用统一建立。</summary>
    private bool _loading = true;

    public GeneratorView(Store store, Action<string> setStatus)
    {
        InitializeComponent();
        _store = store;
        _setStatus = setStatus;

        ModuleList.ItemsSource = _modules;
        Rebind();
        ReloadModules();
        ApplyProfile();
        _loading = false;
        LoadRecents();
        TryRestoreModulePref(); // 启动后自动恢复上次该厂商×设备的常用模块勾选

        // [诊断用] 通过环境变量自动切换页签以复现崩溃：NCP_TAB=页签索引
        if (Environment.GetEnvironmentVariable("NCP_TAB") is { } tabIdx &&
            int.TryParse(tabIdx, out var idx) && idx >= 0 && idx < ParamTabs.Items.Count)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () =>
                ParamTabs.SelectedIndex = idx);
        }

        // [诊断用] NCP_GEN=1：全选所有模块并自动生成一次配置（回归测试用，不弹对话框）
        if (Environment.GetEnvironmentVariable("NCP_GEN") == "1")
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () =>
            {
                // 可选的设备类型覆盖：NCP_DT=Router/Firewall/Switch/AccessPoint
                if (Environment.GetEnvironmentVariable("NCP_DT") is { } dtTag &&
                    Enum.TryParse<DeviceType>(dtTag, out var dt))
                {
                    _deviceType = dt;
                    ReloadModules();
                    ApplyProfile();
                }
                BtnSelectAll_Click(this, new RoutedEventArgs());
                BtnGenerate_Click(this, new RoutedEventArgs());
            });
        }
    }

    /// <summary>把当前参数集合重新绑定到表单与列表（切换模板后调用）。</summary>
    private void Rebind()
    {
        BasicPanel.DataContext = _spec.Basic;
        StpPanel.DataContext = _spec.Stp;
        RoutingPanel.DataContext = _spec.Routing;
        NatPanel.DataContext = _spec.Nat;
        WirelessPanel.DataContext = _spec.Wireless;
        SnmpPanel.DataContext = _spec.Snmp;
        SshPanel.DataContext = _spec.Ssh;
        SecurityPanel.DataContext = _spec.Security;

        GridVlan.ItemsSource = _spec.Vlans;
        GridInterface.ItemsSource = _spec.Interfaces;
        GridStaticRoute.ItemsSource = _spec.StaticRoutes;
        GridOspfNet.ItemsSource = _spec.OspfNetworks;
        GridBgpPeer.ItemsSource = _spec.BgpNeighbors;
        GridAcl.ItemsSource = _spec.AclRules;
        GridZone.ItemsSource = _spec.Zones;
        GridPolicy.ItemsSource = _spec.Policies;
        GridDhcp.ItemsSource = _spec.DhcpPools;

        // v2 扩展类别
        GridLag.ItemsSource = _spec.LinkAggs;
        GridVrrp.ItemsSource = _spec.Vrrps;
        DhcpSnoopPanel.DataContext = _spec.DhcpSnoop;
        GridMirror.ItemsSource = _spec.Mirrors;
        GridPortSec.ItemsSource = _spec.PortSecs;
        Dot1xPanel.DataContext = _spec.Dot1x;
        LldpPanel.DataContext = _spec.Lldp;

        // v3 扩展类别（远程管理接入）
        GridLocalUser.ItemsSource = _spec.LocalUsers;
        TelnetPanel.DataContext = _spec.Telnet;
        WebAdminPanel.DataContext = _spec.WebAdmin;

        // v6 功能补齐
        GridPortMap.ItemsSource = _spec.PortMaps;
        GridStaticNat.ItemsSource = _spec.StaticNats;
        GridDhcpRelay.ItemsSource = _spec.DhcpRelays;
        GridDhcpBind.ItemsSource = _spec.DhcpBinds;
        GridArpBind.ItemsSource = _spec.ArpBinds;
        GridStorm.ItemsSource = _spec.Storms;
        IgmpPanel.DataContext = _spec.Igmp;
        GridPbr.ItemsSource = _spec.Pbrs;
        GridQos.ItemsSource = _spec.Qos;
        GridTimeRange.ItemsSource = _spec.TimeRanges;
        GridBfd.ItemsSource = _spec.Bfds;
        GridPoe.ItemsSource = _spec.PoEs;

        // v13 扩展类别（IPv6 / GRE / 语音 VLAN / 端口隔离）
        Ipv6Panel.DataContext = _spec.Ipv6;
        GridGre.ItemsSource = _spec.Gres;
        GridVoice.ItemsSource = _spec.Voices;
        IsolatePanel.DataContext = _spec.Isolate;

        SyncFormVisibility();
    }

    /// <summary>同步由枚举取值驱动的面板显隐。</summary>
    private void SyncFormVisibility()
    {
        WanStaticPanel.Visibility = _spec.Basic.WanMode == WanMode.StaticIp ? Visibility.Visible : Visibility.Collapsed;
        WanPppoePanel.Visibility = _spec.Basic.WanMode == WanMode.Pppoe ? Visibility.Visible : Visibility.Collapsed;
        EasyIpPanel.Visibility = _spec.Nat.Kind == NatKind.EasyIp ? Visibility.Visible : Visibility.Collapsed;
        StaticNatPanel.Visibility = _spec.Nat.Kind == NatKind.StaticNat ? Visibility.Visible : Visibility.Collapsed;
        SnmpV3Panel.Visibility = _spec.Snmp.Version.Equals("v3", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible : Visibility.Collapsed;

        switch (_spec.Basic.WanMode)
        {
            case WanMode.Dhcp: RbWanDhcp.IsChecked = true; break;
            case WanMode.Pppoe: RbWanPppoe.IsChecked = true; break;
            default: RbWanStatic.IsChecked = true; break;
        }

        if (_spec.Nat.Kind == NatKind.StaticNat) RbNatStatic.IsChecked = true;
        else RbNatEasy.IsChecked = true;
    }

    /// <summary>载入模板库传入的参数集合。</summary>
    public void Refresh()
    {
        if (AppState.PendingSpec is not { } spec) return;
        AppState.PendingSpec = null;

        _spec = spec;
        _vendor = AppState.PendingVendor;
        _deviceType = AppState.PendingDeviceType;

        (_vendor switch
        {
            Vendor.Huawei => VndHuawei,
            Vendor.H3C => VndH3C,
            Vendor.Cisco => VndCisco,
            _ => VndRuijie
        }).IsChecked = true;

        (_deviceType switch
        {
            DeviceType.Router => DevRouter,
            DeviceType.Firewall => DevFirewall,
            DeviceType.AccessPoint => DevAp,
            _ => DevSwitch
        }).IsChecked = true;

        Rebind();
        ApplyProfile();

        foreach (var m in _modules) m.Enabled = _spec.EnabledModules.Contains(m.Key);
        TxtModuleCount.Text = $"已选 {_spec.EnabledModules.Count} 个模块";
        _setStatus($"已载入模板参数：{EnumText.VendorShort(_vendor)} {EnumText.DeviceName(_deviceType)}，请点击「生成配置」");
    }

    // ==================== 从设备清单载入 ====================
    private void BtnLoadDevice_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new DevicePickDialog(_store.Devices,
            "选择后自动带入厂商/设备类型，并预填主机名与管理 IP；已勾选模块会保留。")
        { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true || dlg.Selected is not { } dev) return;

        // 1) 厂商 / 设备类型 chip（IsChecked 变化会触发对应 Checked 事件完成模块与页签联动）
        (dev.Vendor switch
        {
            Vendor.Huawei => VndHuawei,
            Vendor.H3C => VndH3C,
            Vendor.Cisco => VndCisco,
            _ => VndRuijie
        }).IsChecked = true;
        (dev.DeviceType switch
        {
            DeviceType.Router => DevRouter,
            DeviceType.Firewall => DevFirewall,
            DeviceType.AccessPoint => DevAp,
            _ => DevSwitch
        }).IsChecked = true;

        // 2) 预填基础信息（型号可能为空时保留）
        if (!string.IsNullOrWhiteSpace(dev.Name)) _spec.Basic.Hostname = dev.Name.Trim();
        if (NetHelper.IsValidIp(dev.Ip)) _spec.Basic.MgmtIp = dev.Ip.Trim();
        Rebind(); // 刷新表单绑定
        ApplyProfile();
        _setStatus($"已载入设备「{dev.Name}」（{dev.VendorText} {dev.DeviceText}{(dev.Model.Length > 0 ? " " + dev.Model : "")}），主机名/管理 IP 已预填，请完善参数后生成");
    }

    // ==================== 最近方案（B3）====================
    private void LoadRecents()
    {
        _recents = AppStorage.Load<RecentProfile>("recents.json");
        RefreshRecentList(true);
    }

    private void RefreshRecentList(bool keepSelection = false)
    {
        if (CmbRecent == null) return;
        var sel = keepSelection && CmbRecent.SelectedItem is RecentProfile cur ? cur.Display : null;
        CmbRecent.ItemsSource = null;
        CmbRecent.ItemsSource = _recents;
        CmbRecent.DisplayMemberPath = "Display";
        if (sel is null && _recents.Count > 0) CmbRecent.SelectedIndex = 0;
        else if (sel is not null)
        {
            var idx = _recents.FindIndex(r => r.Display == sel);
            CmbRecent.SelectedIndex = idx >= 0 ? idx : (_recents.Count > 0 ? 0 : -1);
        }
    }

    private void SaveRecents() => AppStorage.Save("recents.json", _recents.Take(10));

    /// <summary>成功生成后自动记忆（最多 10 条；同厂商×设备仅保留最新一条自动记录）。</summary>
    private void AutoRememberRecent()
    {
        try
        {
            if (_spec.EnabledModules.Count == 0) return;
            var name = $"{EnumText.VendorShort(_vendor)}·{EnumText.DeviceName(_deviceType)} {DateTime.Now:MM-dd HH:mm}";
            var json = AppStorage.Serialize(_spec);
            _recents.RemoveAll(r => r.Vendor == _vendor.ToString() && r.DeviceType == _deviceType.ToString() && r.Name.StartsWith("自动"));
            _recents.Insert(0, new RecentProfile
            {
                Name = name,
                Vendor = _vendor.ToString(),
                DeviceType = _deviceType.ToString(),
                SpecJson = json,
                UpdatedAt = DateTime.Now
            });
            while (_recents.Count > 10) _recents.RemoveAt(_recents.Count - 1);
            SaveRecents();
            RefreshRecentList();
            // 同步更新该厂商×设备类型的模块勾选偏好（下次打开自动恢复）
            Prefs.Set(_vendor.ToString(), _deviceType.ToString(), _spec.EnabledModules);
        }
        catch { /* 记忆失败不影响生成 */ }
    }

    private void BtnRecentLoad_Click(object sender, RoutedEventArgs e)
    {
        if (CmbRecent.SelectedItem is not RecentProfile rp)
        {
            _setStatus("请先在列表选择一条方案");
            return;
        }
        var spec = AppStorage.Deserialize<ConfigSpec>(rp.SpecJson);
        if (spec is null)
        {
            MessageBox.Show("方案数据无法解析（可能已损坏），请删除后重建。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _spec = spec;
        if (Enum.TryParse<Vendor>(rp.Vendor, out var v)) _vendor = v;
        if (Enum.TryParse<DeviceType>(rp.DeviceType, out var dt)) _deviceType = dt;

        // 同步厂商/设备 chips（同值时 IsChecked 赋值不触发事件，故再显式重建）
        (v switch { Vendor.Huawei => VndHuawei, Vendor.H3C => VndH3C, Vendor.Cisco => VndCisco, _ => VndRuijie }).IsChecked = true;
        (dt switch { DeviceType.Router => DevRouter, DeviceType.Firewall => DevFirewall, DeviceType.AccessPoint => DevAp, _ => DevSwitch }).IsChecked = true;

        ReloadModules();
        Rebind();
        ApplyProfile();
        _setStatus($"已载入方案「{rp.Name}」（{rp.Vendor} {rp.DeviceType}），参数已全部恢复");
    }

    private void BtnRecentSave_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new PromptDialog("保存当前参数为方案", "方案名称：",
            $"{EnumText.VendorShort(_vendor)}·{EnumText.DeviceName(_deviceType)} 标准配置")
        { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;
        var name = dlg.InputText;
        if (name.Length == 0) name = $"{EnumText.VendorShort(_vendor)}·{EnumText.DeviceName(_deviceType)} {DateTime.Now:MM-dd HH:mm}";

        _recents.RemoveAll(r => r.Name == name);
        _recents.Insert(0, new RecentProfile
        {
            Name = name,
            Vendor = _vendor.ToString(),
            DeviceType = _deviceType.ToString(),
            SpecJson = AppStorage.Serialize(_spec),
            UpdatedAt = DateTime.Now
        });
        while (_recents.Count > 10) _recents.RemoveAt(_recents.Count - 1);
        SaveRecents();
        RefreshRecentList();
        _setStatus($"方案「{name}」已保存（最多保留 10 条）");
    }

    private void BtnRecentDelete_Click(object sender, RoutedEventArgs e)
    {
        if (CmbRecent.SelectedItem is not RecentProfile rp) return;
        _recents.Remove(rp);
        SaveRecents();
        RefreshRecentList(true);
        _setStatus($"已删除方案「{rp.Name}」");
    }

    // ==================== 导入现网配置（A2）====================
    private void BtnImportCfg_Click(object sender, RoutedEventArgs e)
    {
        var work = AppStorage.Clone(_spec); // 预览副本：解析先作用于副本，点“应用”才替换当前
        var dlg = new ImportDialog(work) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;

        _spec = dlg.Result; // 应用：用被解析回填过的工作副本替换当前参数
        var dv = dlg.DetectedVendor;
        if (dv is not null) _vendor = dv.Value;
        (dv switch
        {
            Vendor.Huawei => VndHuawei,
            Vendor.H3C => VndH3C,
            Vendor.Cisco => VndCisco,
            Vendor.Ruijie => VndRuijie,
            _ => VndHuawei
        }).IsChecked = true;

        ReloadModules();
        Rebind();
        ApplyProfile();
        _setStatus(dv is null
            ? "配置未能识别厂商，未回填——请确认粘贴的是 running-config 导出全文"
            : $"已导入现网配置（{EnumText.VendorName(dv.Value)}）并回填核心参数——请在各页签核对细节后生成");
    }

    // ==================== 命令手册（B4）====================
    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        new HelpDialog { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    // ==================== 厂商 / 设备类型 ====================
    private void Vendor_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is not RadioButton rb || rb.Tag is not string tag) return;
        _vendor = Enum.Parse<Vendor>(tag);
        ApplyProfile();
        TryRestoreModulePref(); // 无勾选且该厂商×设备存在偏好时自动恢复
    }

    private void Device_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is not RadioButton rb || rb.Tag is not string tag) return;
        _deviceType = Enum.Parse<DeviceType>(tag);
        ReloadModules();
        ApplyProfile();
        TryRestoreModulePref();
    }

    /// <summary>模块偏好恢复：仅在“当前没有任何勾选”时，按厂商×设备类型恢复上次的常用模块组合。</summary>
    private void TryRestoreModulePref()
    {
        if (_spec.EnabledModules.Count > 0 || _modules.Any(m => m.Enabled)) return;
        var pref = Prefs.Get(_vendor.ToString(), _deviceType.ToString());
        if (pref is null) return;
        var supported = Mod.For(_deviceType).Select(m => m.Key).ToHashSet();
        var keys = Prefs.KeysOf(pref).Where(supported.Contains).ToList();
        if (keys.Count == 0) return;

        _spec.EnabledModules.UnionWith(keys);
        foreach (var m in _modules) m.Enabled = keys.Contains(m.Key);
        RefreshModuleList();
        TxtModuleCount.Text = $"已选 {_spec.EnabledModules.Count} 个模块";
        _setStatus($"已自动恢复 {EnumText.VendorName(_vendor)}{EnumText.DeviceName(_deviceType)} 的常用模块勾选（{keys.Count} 个），可按需调整");
    }

    private void ReloadModules()
    {
        var previous = new HashSet<string>(_modules.Where(m => m.Enabled).Select(m => m.Key)
            .Concat(_spec.EnabledModules));

        _modules.Clear();
        _spec.EnabledModules.Clear();

        foreach (var (key, name, tip, _) in Mod.For(_deviceType))
        {
            var item = new ModuleItem { Key = key, Name = name, Tip = tip };
            // 首次进入时给出常用默认勾选
            var shouldEnable = previous.Count == 0
                ? key is Mod.Basic or Mod.Vlan or Mod.Interface
                : previous.Contains(key);
            item.Enabled = shouldEnable;
            if (shouldEnable) _spec.EnabledModules.Add(key);
            _modules.Add(item);
        }
        RefreshModuleList();
    }

    private void ApplyProfile()
    {
        var gen = GeneratorFactory.Get(_vendor);
        TxtProfile.Text = $"{EnumText.VendorShort(_vendor)} {EnumText.OsName(_vendor)} · {EnumText.DeviceName(_deviceType)}";
        TxtIfHint.Text = $"接口名示例：{gen.ExampleInterface(_deviceType)}（不同型号接口编号略有差异，请按实际调整）";
        TxtModuleCount.Text = $"已选 {_spec.EnabledModules.Count} 个模块";

        var supported = Mod.For(_deviceType).Select(m => m.Key).ToHashSet();

        // 按设备类型显隐：模块目录 Scope 与页签一一对应（AP 无路由/VLAN/ACL/NAT/DHCP 等）
        TabVlan.Visibility = supported.Contains(Mod.Vlan) ? Visibility.Visible : Visibility.Collapsed;
        TabInterface.Visibility = supported.Contains(Mod.Interface) ? Visibility.Visible : Visibility.Collapsed;
        TabAcl.Visibility = supported.Contains(Mod.Acl) ? Visibility.Visible : Visibility.Collapsed;
        TabDhcp.Visibility = supported.Contains(Mod.Dhcp) ? Visibility.Visible : Visibility.Collapsed;
        var hasRoute = supported.Contains(Mod.StatRoute) || supported.Contains(Mod.OspfR)
                       || supported.Contains(Mod.BgpR) || supported.Contains(Mod.DynR);
        TabRouting.Visibility = hasRoute ? Visibility.Visible : Visibility.Collapsed;

        TabStp.Visibility = supported.Contains(Mod.Stp) ? Visibility.Visible : Visibility.Collapsed;
        TabFirewall.Visibility = supported.Contains(Mod.Firewall) ? Visibility.Visible : Visibility.Collapsed;
        TabWireless.Visibility = supported.Contains(Mod.Wireless) ? Visibility.Visible : Visibility.Collapsed;
        TabNat.Visibility = supported.Contains(Mod.Nat) ? Visibility.Visible : Visibility.Collapsed;
        WanPanel.Visibility = _deviceType is DeviceType.Router or DeviceType.Firewall
            ? Visibility.Visible : Visibility.Collapsed;

        // v2 扩展类别按设备类型显隐
        TabLag.Visibility = supported.Contains(Mod.Lag) ? Visibility.Visible : Visibility.Collapsed;
        TabVrrp.Visibility = supported.Contains(Mod.Vrrp) ? Visibility.Visible : Visibility.Collapsed;
        TabDhcpSnoop.Visibility = supported.Contains(Mod.DhcpSnoop) ? Visibility.Visible : Visibility.Collapsed;
        TabMirror.Visibility = supported.Contains(Mod.Mirror) ? Visibility.Visible : Visibility.Collapsed;
        TabPortSec.Visibility = supported.Contains(Mod.PortSec) ? Visibility.Visible : Visibility.Collapsed;
        TabDot1x.Visibility = supported.Contains(Mod.Dot1x) ? Visibility.Visible : Visibility.Collapsed;
        TabLldp.Visibility = supported.Contains(Mod.Lldp) ? Visibility.Visible : Visibility.Collapsed;

        // v6 功能补齐页签按设备类型显隐
        TabPortMap.Visibility = supported.Contains(Mod.PortMap) ? Visibility.Visible : Visibility.Collapsed;
        TabStaticNat.Visibility = supported.Contains(Mod.StaticNat) ? Visibility.Visible : Visibility.Collapsed;
        TabDhcpRelay.Visibility = supported.Contains(Mod.DhcpRelay) ? Visibility.Visible : Visibility.Collapsed;
        TabDhcpBind.Visibility = supported.Contains(Mod.DhcpBind) ? Visibility.Visible : Visibility.Collapsed;
        TabArpBind.Visibility = supported.Contains(Mod.ArpBind) ? Visibility.Visible : Visibility.Collapsed;
        TabStorm.Visibility = supported.Contains(Mod.Storm) ? Visibility.Visible : Visibility.Collapsed;
        TabIgmp.Visibility = supported.Contains(Mod.IgmpSnoop) ? Visibility.Visible : Visibility.Collapsed;
        TabPbr.Visibility = supported.Contains(Mod.Pbr) ? Visibility.Visible : Visibility.Collapsed;
        TabQos.Visibility = supported.Contains(Mod.Qos) ? Visibility.Visible : Visibility.Collapsed;
        TabTimeRange.Visibility = supported.Contains(Mod.TimeRange) ? Visibility.Visible : Visibility.Collapsed;
        TabBfd.Visibility = supported.Contains(Mod.Bfd) ? Visibility.Visible : Visibility.Collapsed;
        TabPoe.Visibility = supported.Contains(Mod.Poe) ? Visibility.Visible : Visibility.Collapsed;
        TabIpv6.Visibility = supported.Contains(Mod.Ipv6) ? Visibility.Visible : Visibility.Collapsed;
        TabGre.Visibility = supported.Contains(Mod.Gre) ? Visibility.Visible : Visibility.Collapsed;
        TabVoice.Visibility = supported.Contains(Mod.Voice) ? Visibility.Visible : Visibility.Collapsed;
        TabIsolate.Visibility = supported.Contains(Mod.Isolate) ? Visibility.Visible : Visibility.Collapsed;

        // 若当前选中的页签因设备类型切换而隐藏，则回到第一个可见页签
        if (ParamTabs.SelectedItem is TabItem cur && cur.Visibility == Visibility.Collapsed)
            ParamTabs.SelectedIndex = 0;
    }

    // ==================== 模块搜索 / 预设 / 预检 / 批量（v12 交互优化） ====================
    private void TxtModuleSearch_TextChanged(object sender, TextChangedEventArgs e) => RefreshModuleList();

    private void RefreshModuleList()
    {
        if (ModuleList == null || TxtModuleNone == null) return;
        var q = TxtModuleSearch.Text?.Trim() ?? "";
        if (q.Length == 0)
        {
            ModuleList.ItemsSource = _modules;
            TxtModuleNone.Visibility = Visibility.Collapsed;
            return;
        }
        var hit = _modules.Where(m => m.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                                      || m.Key.Contains(q, StringComparison.OrdinalIgnoreCase)
                                      || m.Tip.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        ModuleList.ItemsSource = hit;
        TxtModuleNone.Visibility = hit.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static readonly (string Name, DeviceType[] Dev, string[] Keys)[] Presets =
    [
        ("小型办公出口（路由/防火墙）", [DeviceType.Router, DeviceType.Firewall],
            [Mod.Basic, Mod.Interface, Mod.StatRoute, Mod.Acl, Mod.Nat, Mod.PortMap, Mod.Dhcp,
             Mod.Ssh, Mod.LocalUser, Mod.Security, Mod.Qos]),
        ("出口双线分流（路由/防火墙）", [DeviceType.Router, DeviceType.Firewall],
            [Mod.Basic, Mod.Interface, Mod.StatRoute, Mod.Pbr, Mod.OspfR, Mod.Acl, Mod.Nat,
             Mod.PortMap, Mod.StaticNat, Mod.Ssh, Mod.Security]),
        ("标准园区接入（交换机）", [DeviceType.Switch],
            [Mod.Basic, Mod.Vlan, Mod.Interface, Mod.Stp, Mod.Lag, Mod.Lldp, Mod.DhcpSnoop,
             Mod.PortSec, Mod.Storm, Mod.IgmpSnoop, Mod.Voice, Mod.Isolate]),
        ("三层汇聚（交换机）", [DeviceType.Switch],
            [Mod.Basic, Mod.Vlan, Mod.Interface, Mod.Stp, Mod.StatRoute, Mod.OspfR, Mod.Lag,
             Mod.Vrrp, Mod.DhcpSnoop, Mod.Snmp, Mod.Ssh, Mod.Qos]),
        ("无线热点（AP）", [DeviceType.AccessPoint],
            [Mod.Basic, Mod.Wireless, Mod.Snmp, Mod.Ssh, Mod.LocalUser, Mod.Security]),
        ("运维全开（三层设备）", [DeviceType.Router, DeviceType.Firewall, DeviceType.Switch],
            [Mod.Basic, Mod.Vlan, Mod.Interface, Mod.Snmp, Mod.Ssh, Mod.LocalUser, Mod.WebAdmin,
             Mod.Lldp, Mod.Bfd, Mod.TimeRange])
    ];

    private void BtnPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var menu = new ContextMenu();
        var any = false;
        foreach (var p in Presets)
        {
            if (!p.Dev.Contains(_deviceType)) continue;
            any = true;
            var item = new MenuItem { Header = p.Name };
            item.Click += (_, _) => ApplyPreset(p.Keys, p.Name);
            menu.Items.Add(item);
        }
        if (!any) menu.Items.Add(new MenuItem { Header = "（当前设备类型无内置预设）", IsEnabled = false });

        // 厂商×设备勾选偏好记忆
        menu.Items.Add(new Separator());
        var savePref = new MenuItem { Header = $"记住当前勾选（{EnumText.VendorShort(_vendor)}{EnumText.DeviceName(_deviceType)}）" };
        savePref.Click += (_, _) =>
        {
            if (_spec.EnabledModules.Count == 0)
            {
                _setStatus("当前没有勾选任何模块，无需记忆");
                return;
            }
            Prefs.Set(_vendor.ToString(), _deviceType.ToString(), _spec.EnabledModules);
            _setStatus($"已记住 {EnumText.VendorName(_vendor)}{EnumText.DeviceName(_deviceType)} 的 {_spec.EnabledModules.Count} 个常用模块，下次打开自动恢复");
        };
        menu.Items.Add(savePref);
        var clearPref = new MenuItem { Header = $"清除该厂商·设备的偏好记忆" };
        clearPref.Click += (_, _) =>
        {
            Prefs.Clear(_vendor.ToString(), _deviceType.ToString());
            _setStatus($"已清除 {EnumText.VendorName(_vendor)}{EnumText.DeviceName(_deviceType)} 的偏好记忆");
        };
        menu.Items.Add(clearPref);

        menu.PlacementTarget = btn;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void ApplyPreset(IReadOnlyList<string> keys, string presetName)
    {
        var supported = Mod.For(_deviceType).Select(m => m.Key).ToHashSet();
        _spec.EnabledModules.Clear();
        foreach (var m in _modules) m.Enabled = false;
        foreach (var k in keys)
        {
            if (supported.Contains(k)) _spec.EnabledModules.Add(k);
        }
        foreach (var m in _modules) m.Enabled = _spec.EnabledModules.Contains(m.Key);
        RefreshModuleList();
        TxtModuleCount.Text = $"已选 {_spec.EnabledModules.Count} 个模块";
        _setStatus($"已套用预设「{presetName}」，勾选 {_spec.EnabledModules.Count} 个模块（不适用于当前设备的已忽略）");
    }

    // -------------------- 生成前合法性预检 --------------------
    private List<string> ValidateWarnings()
    {
        var warns = new List<string>();
        if (string.IsNullOrWhiteSpace(_spec.Basic.Hostname)) warns.Add("主机名未填写");
        if (!NetHelper.IsValidIp(_spec.Basic.MgmtIp)) warns.Add($"管理 IP 格式不正确：{_spec.Basic.MgmtIp}");

        var vlans = _spec.Vlans.Where(v => v.Id > 0).Select(v => v.Id).ToList();
        var dups = vlans.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).OrderBy(x => x).ToList();
        if (dups.Count > 0) warns.Add("VLAN 编号重复：" + string.Join(", ", dups));
        var badVlans = _spec.Vlans.Where(v => v.Id is < 1 or > 4094).Select(v => v.Id).OrderBy(x => x).ToList();
        if (badVlans.Count > 0) warns.Add("VLAN 编号越界（1-4094）：" + string.Join(", ", badVlans));

        foreach (var rl in _spec.AclRules.Where(r => !string.IsNullOrWhiteSpace(r.Number)))
        {
            if (!int.TryParse(rl.Number.Trim(), out var no) || !IsAclNumberOk(no, rl.Kind))
                warns.Add($"ACL {rl.Number.Trim()} 的编号段对 {EnumText.VendorShort(_vendor)} {rl.Kind} ACL 无效（请参考各平台编号范围）");
        }
        foreach (var sr in _spec.StaticRoutes.Where(r => !string.IsNullOrWhiteSpace(r.Prefix)))
        {
            if (!NetHelper.IsValidIp(sr.Prefix)) warns.Add($"静态路由目的网段非法：{sr.Prefix}");
            if (!string.IsNullOrWhiteSpace(sr.NextHop) && !NetHelper.IsValidIp(sr.NextHop))
                warns.Add($"静态路由 {sr.Prefix} 的下一跳非法：{sr.NextHop}");
        }
        foreach (var p in _spec.PortMaps)
        {
            if (string.IsNullOrWhiteSpace(p.ExternalPort))
                warns.Add("端口映射有行未填外部端口，该行不会生成（请补 ExternalPort 或删除该行）");
            else if (!p.ExternalPort.Trim().All(char.IsAsciiDigit))
                warns.Add($"端口映射外部端口须为数字：{p.ExternalPort.Trim()}（该行不会生成）");
            if (!NetHelper.IsValidIp(p.InsideIp))
                warns.Add($"端口映射 {p.ExternalPort.Trim()} 的内网服务器 IP 非法/为空，该行不会生成：{p.InsideIp}");
        }
        foreach (var n in _spec.StaticNats)
        {
            if (!NetHelper.IsValidIp(n.InsideIp) || !NetHelper.IsValidIp(n.GlobalIp))
                warns.Add($"NAT 静态一对一需同时填写合法的公网 IP 与内网 IP（当前：{n.GlobalIp} → {n.InsideIp}）");
        }
        if (_spec.Nat.Kind == NatKind.EasyIp &&
            !_spec.AclRules.Any(r => r.Number.Trim() == _spec.Nat.AclNumber.Trim()))
            warns.Add($"Easy IP 出网引用的 ACL {_spec.Nat.AclNumber.Trim()} 未在「ACL 策略」中定义——请在 ACL 页签补建（否则内网无法上网）");

        // ---- 跨模块引用一致性（v12.4）：引用了"不存在定义"的对象时给出提示 ----
        if (_vendor is Vendor.Cisco or Vendor.Ruijie)
            foreach (var rl in _spec.AclRules.Where(r => !string.IsNullOrWhiteSpace(r.TimeRange) && r.Kind == AclKind.Standard))
                warns.Add($"ACL {rl.Number.Trim()} 是标准 ACL，不支持 time-range（思科/锐捷仅扩展 ACL 支持）——该规则的时间段不会生效，请改用扩展 ACL");
        foreach (var rl in _spec.AclRules.Where(r => !string.IsNullOrWhiteSpace(r.TimeRange)))
            if (!_spec.TimeRanges.Any(t => t.Name.Trim() == rl.TimeRange.Trim()))
                warns.Add($"ACL {rl.Number.Trim()} 引用的时间段「{rl.TimeRange.Trim()}」未在「ACL 时间段」页定义——该规则不会按时段生效");
        foreach (var p in _spec.Pbrs.Where(p => !string.IsNullOrWhiteSpace(p.MatchAcl)))
            if (!_spec.AclRules.Any(r => r.Number.Trim() == p.MatchAcl.Trim()))
                warns.Add($"策略路由 {p.Name.Trim()} 引用的 ACL {p.MatchAcl.Trim()} 未在「ACL 策略」中定义——策略不会匹配到流量");
        foreach (var p in _spec.DhcpPools.Where(p => p.VlanId > 0))
            if (_spec.Vlans.Count > 0 && !_spec.Vlans.Any(v => v.Id == p.VlanId))
                warns.Add($"DHCP 地址池 {p.Name.Trim()} 绑定 VLAN {p.VlanId}，但「VLAN」页未定义该 VLAN——交换机的 Vlanif 无法生效");
        foreach (var bfd in _spec.Bfds.Where(x => NetHelper.IsValidIp(x.PeerIp)))
        {
            if (string.IsNullOrWhiteSpace(bfd.Interface))
                warns.Add($"BFD 会话 {bfd.PeerIp} 未填本端接口，将被跳过");
        }

        // ---- 其余表格式模块：关键字段缺失 = 该行静默跳过，必须给出提示（v12.3 补齐）----
        foreach (var x in _spec.Interfaces.Where(x => string.IsNullOrWhiteSpace(x.Name)))
            warns.Add("接口配置有行未填接口名，该行不会生成");
        foreach (var x in _spec.DhcpPools.Where(x => !NetHelper.IsValidIp(x.Network)))
            warns.Add($"DHCP 地址池有行网络号非法/为空：{x.Network}（该池不会生成）");
        foreach (var x in _spec.DhcpPools.Where(x => x.VlanId < 1))
            warns.Add($"DHCP 地址池 {x.Name} 未绑定 VLAN——请确认网关三层口已手工绑定（否则客户端拿不到地址）");
        foreach (var x in _spec.Vrrps.Where(x => x.GroupId < 1 || !NetHelper.IsValidIp(x.VirtualIp)))
            warns.Add($"VRRP 有行未填组号或虚拟网关 IP 非法（组 {x.GroupId} / {x.VirtualIp}），该行不会生成");
        foreach (var x in _spec.LinkAggs.Where(x => x.Id < 1 || string.IsNullOrWhiteSpace(x.Members)))
            warns.Add($"链路聚合有行未填聚合组号或成员口（组 {x.Id}），该行不会生成");
        foreach (var x in _spec.Mirrors.Where(x => x.SessionId < 1 || string.IsNullOrWhiteSpace(x.DestPort)))
            warns.Add($"端口镜像有行未填会话号或目的端口（会话 {x.SessionId}），该行不会生成");
        foreach (var x in _spec.DhcpRelays.Where(x => string.IsNullOrWhiteSpace(x.Interface) || !NetHelper.IsValidIp(x.ServerIp)))
            warns.Add($"DHCP 中继有行接口为空或服务器 IP 非法（{x.Interface} → {x.ServerIp}），该行不会生成");
        foreach (var x in _spec.DhcpBinds.Where(x => string.IsNullOrWhiteSpace(x.Ip) || string.IsNullOrWhiteSpace(x.Mac)))
            warns.Add("DHCP 静态绑定有行未填 IP 或 MAC，该行不会生成");
        foreach (var x in _spec.ArpBinds.Where(x => !NetHelper.IsValidIp(x.Ip) || string.IsNullOrWhiteSpace(x.Mac)))
            warns.Add($"IP-MAC 绑定有行 IP 非法或 MAC 为空：{x.Ip}（该行不会生成）");
        foreach (var x in _spec.PortSecs.Where(x => string.IsNullOrWhiteSpace(x.Interface)))
            warns.Add("端口安全有行未填接口，该行不会生成");
        foreach (var x in _spec.Storms.Where(x => string.IsNullOrWhiteSpace(x.Interface)))
            warns.Add("风暴抑制有行未填接口，该行不会生成");
        foreach (var x in _spec.Qos.Where(x => string.IsNullOrWhiteSpace(x.Interface) || !long.TryParse(x.CirKbps, out var cir) || cir <= 0))
            warns.Add($"QoS 限速有行接口为空或速率(CIR)非法：{x.CirKbps}（该行不会生成）");
        foreach (var x in _spec.Pbrs.Where(x => !NetHelper.IsValidIp(x.NextHop) && string.IsNullOrWhiteSpace(x.ExitInterface)))
            warns.Add($"策略路由有行未填下一跳或出接口（{x.Name}），该行不会生成");
        foreach (var x in _spec.TimeRanges.Where(x => string.IsNullOrWhiteSpace(x.Name) ||
                                                      !System.Text.RegularExpressions.Regex.IsMatch(x.StartTime, @"^\d{1,2}:\d{2}$") ||
                                                      !System.Text.RegularExpressions.Regex.IsMatch(x.EndTime, @"^\d{1,2}:\d{2}$")))
            warns.Add($"ACL 时间段有行名称或起止时间格式非法（{x.Name} {x.StartTime}-{x.EndTime}，应为 HH:mm），该行不会生成");
        foreach (var x in _spec.PoEs.Where(x => string.IsNullOrWhiteSpace(x.Interface)))
            warns.Add("PoE 供电有行未填接口，该行不会生成");

        if (_spec.Dot1x.Enabled && !NetHelper.IsValidIp(_spec.Dot1x.ServerIp))
            warns.Add($"802.1X 已启用但 RADIUS 服务器 IP 非法：{_spec.Dot1x.ServerIp}");
        return warns;
    }

    private bool IsAclNumberOk(int no, AclKind kind)
    {
        if (no < 1) return false;
        if (_vendor == Vendor.Cisco)
        {
            var std = (no >= 1 && no <= 99) || (no >= 1300 && no <= 1999);
            var ext = (no >= 100 && no <= 199) || (no >= 2000 && no <= 2699);
            return kind == AclKind.Standard ? std : ext;
        }
        var hwStd = no >= 2000 && no <= 2999;
        var hwExt = no >= 3000 && no <= 3999;
        return kind == AclKind.Standard ? hwStd : hwExt;
    }

    // -------------------- 多设备批量生成 --------------------
    private void BtnBulk_Click(object sender, RoutedEventArgs e)
    {
        CommitGrids();
        var dlg = new BulkDialog(_spec.Basic.Hostname) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var gen = GeneratorFactory.Get(_vendor);
            Directory.CreateDirectory(dlg.OutDir);
            var filePrefix = TextUtil.SanitizeFileName(dlg.Prefix, "dev");
            var written = 0;
            for (var i = 1; i <= dlg.Count; i++)
            {
                var spec = AppStorage.Clone(_spec);
                spec.Basic.Hostname = $"{dlg.Prefix}{i}";
                if (dlg.StartIp.Length > 0)
                {
                    var next = OffsetIp(dlg.StartIp, i - 1);
                    if (NetHelper.IsValidIp(next)) spec.Basic.MgmtIp = next;
                }
                var text = gen.Generate(spec, _deviceType);
                var plain = Inspector.StripComments(text, _vendor);
                File.WriteAllText(Path.Combine(dlg.OutDir, $"{filePrefix}{i}.cfg"), plain,
                    new System.Text.UTF8Encoding(false));
                written++;
            }
            _setStatus($"已批量生成 {written} 份纯命令配置到：{dlg.OutDir}");
            MessageBox.Show($"已生成 {written} 份配置（纯命令，可直接下发）：\n{dlg.OutDir}",
                "批量生成完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _setStatus("批量生成失败：" + ex.Message);
            MessageBox.Show("批量生成失败：\n" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>IPv4 末段 +offset，超 254 进位到第三段（简化）。非法输入原样返回。</summary>
    private static string OffsetIp(string ip, int offset)
    {
        var p = ip.Trim().Split('.');
        if (p.Length != 4 || !p.All(o => byte.TryParse(o, out _))) return ip;
        var a = new[] { int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]) };
        a[3] += offset;
        while (a[3] > 254) { a[3] -= 254; a[2]++; }
        while (a[2] > 254) { a[2] -= 254; a[1]++; }
        if (a[1] > 255) { a[1] = 255; a[2] = Math.Min(a[2], 254); a[3] = Math.Min(a[3], 254); } // 极长递增时饱和，避免生成 256+ 非法段
        return $"{a[0]}.{a[1]}.{a[2]}.{a[3]}";
    }

    private void Module_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.Tag is not string key) return;
        if (cb.IsChecked == true) _spec.EnabledModules.Add(key);
        else _spec.EnabledModules.Remove(key);
        TxtModuleCount.Text = $"已选 {_spec.EnabledModules.Count} 个模块";
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var m in _modules)
        {
            m.Enabled = true;
            _spec.EnabledModules.Add(m.Key);
        }
        TxtModuleCount.Text = $"已选 {_spec.EnabledModules.Count} 个模块";
    }

    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var m in _modules) m.Enabled = false;
        _spec.EnabledModules.Clear();
        TxtModuleCount.Text = $"已选 {_spec.EnabledModules.Count} 个模块";
    }

    // ==================== 表单联动 ====================
    private void WanMode_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is not RadioButton rb || rb.Tag is not string tag) return;
        _spec.Basic.WanMode = Enum.Parse<WanMode>(tag);
        WanStaticPanel.Visibility = _spec.Basic.WanMode == WanMode.StaticIp ? Visibility.Visible : Visibility.Collapsed;
        WanPppoePanel.Visibility = _spec.Basic.WanMode == WanMode.Pppoe ? Visibility.Visible : Visibility.Collapsed;
    }

    private void NatKind_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is not RadioButton rb || rb.Tag is not string tag) return;
        _spec.Nat.Kind = Enum.Parse<NatKind>(tag);
        EasyIpPanel.Visibility = _spec.Nat.Kind == NatKind.EasyIp ? Visibility.Visible : Visibility.Collapsed;
        StaticNatPanel.Visibility = _spec.Nat.Kind == NatKind.StaticNat ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CmbSnmpVer_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_spec == null) return;
        SnmpV3Panel.Visibility = _spec.Snmp.Version.Equals("v3", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    // ==================== 列表增删 ====================
    private void BtnAddVlan_Click(object sender, RoutedEventArgs e) => _spec.Vlans.Add(new VlanItem());
    private void BtnDelVlan_Click(object sender, RoutedEventArgs e) => DeleteSelected(GridVlan, _spec.Vlans);
    private void BtnClearVlan_Click(object sender, RoutedEventArgs e) => _spec.Vlans.Clear();

    private void BtnAddIf_Click(object sender, RoutedEventArgs e) => _spec.Interfaces.Add(new InterfaceItem());
    private void BtnDelIf_Click(object sender, RoutedEventArgs e) => DeleteSelected(GridInterface, _spec.Interfaces);
    private void BtnClearIf_Click(object sender, RoutedEventArgs e) => _spec.Interfaces.Clear();

    private void BtnSampleIf_Click(object sender, RoutedEventArgs e)
    {
        var gen = GeneratorFactory.Get(_vendor);
        _spec.Interfaces.Add(new InterfaceItem
        {
            Name = gen.ExampleInterface(_deviceType),
            Description = "Uplink",
            Mode = _deviceType == DeviceType.Switch ? InterfaceMode.Trunk : InterfaceMode.Routed,
            TrunkVlans = "10,20",
            Enabled = true
        });
    }

    private void BtnAddAcl_Click(object sender, RoutedEventArgs e) => _spec.AclRules.Add(new AclRuleItem());
    private void BtnDelAcl_Click(object sender, RoutedEventArgs e) => DeleteSelected(GridAcl, _spec.AclRules);
    private void BtnClearAcl_Click(object sender, RoutedEventArgs e) => _spec.AclRules.Clear();

    private void BtnAddDhcp_Click(object sender, RoutedEventArgs e) => _spec.DhcpPools.Add(new DhcpPoolItem());
    private void BtnDelDhcp_Click(object sender, RoutedEventArgs e) => DeleteSelected(GridDhcp, _spec.DhcpPools);
    private void BtnClearDhcp_Click(object sender, RoutedEventArgs e) => _spec.DhcpPools.Clear();

    private static void DeleteSelected<T>(DataGrid grid, IList<T> source) where T : class
    {
        var targets = grid.SelectedItems.OfType<T>().ToList();
        foreach (var t in targets) source.Remove(t);
    }

    /// <summary>通用“删除选中行”：由按钮 Tag 引用目标 DataGrid。</summary>
    private void BtnDelRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not DataGrid grid || grid.SelectedItems.Count == 0) return;
        if (grid.ItemsSource is not System.Collections.IList list) return;
        var targets = grid.SelectedItems.Cast<object>().ToList();
        foreach (var t in targets) list.Remove(t);
    }

    // ==================== 生成与输出 ====================
    private void CommitGrids()
    {
        DataGrid[] grids =
        [
            GridVlan, GridInterface, GridStaticRoute, GridOspfNet, GridBgpPeer, GridAcl, GridZone, GridPolicy, GridDhcp,
            GridLag, GridVrrp, GridMirror, GridPortSec, GridLocalUser,
            GridPortMap, GridStaticNat, GridDhcpRelay, GridDhcpBind, GridArpBind, GridStorm,
            GridPbr, GridQos, GridTimeRange, GridBfd, GridPoe,
            GridGre, GridVoice
        ];
        foreach (var g in grids)
        {
            try
            {
                g.CommitEdit(DataGridEditingUnit.Row, true);
                g.CommitEdit(DataGridEditingUnit.Cell, true);
            }
            catch
            {
                // 编辑态提交失败时忽略，按已提交的数据生成
            }
        }
    }

    // -------------------- 自动补勾：有数据但漏勾的模块 --------------------
    /// <summary>页签表格/表单填了数据，若对应模块漏勾则生成时自动补勾（端口映射等最容易漏）。</summary>
    private static readonly (string Key, Func<ConfigSpec, int> Count)[] DataModules =
    [
        (Mod.Vlan, s => s.Vlans.Count), (Mod.Interface, s => s.Interfaces.Count),
        (Mod.StatRoute, s => s.StaticRoutes.Count), (Mod.OspfR, s => s.OspfNetworks.Count),
        (Mod.BgpR, s => s.BgpNeighbors.Count), (Mod.Acl, s => s.AclRules.Count),
        (Mod.Dhcp, s => s.DhcpPools.Count), (Mod.Firewall, s => s.Zones.Count + s.Policies.Count),
        (Mod.Lag, s => s.LinkAggs.Count), (Mod.Vrrp, s => s.Vrrps.Count),
        (Mod.Mirror, s => s.Mirrors.Count), (Mod.PortSec, s => s.PortSecs.Count),
        (Mod.LocalUser, s => s.LocalUsers.Count),
        (Mod.PortMap, s => s.PortMaps.Count), (Mod.StaticNat, s => s.StaticNats.Count),
        (Mod.DhcpRelay, s => s.DhcpRelays.Count), (Mod.DhcpBind, s => s.DhcpBinds.Count),
        (Mod.ArpBind, s => s.ArpBinds.Count), (Mod.Storm, s => s.Storms.Count),
        (Mod.Pbr, s => s.Pbrs.Count), (Mod.Qos, s => s.Qos.Count),
        (Mod.TimeRange, s => s.TimeRanges.Count), (Mod.Bfd, s => s.Bfds.Count),
        (Mod.Poe, s => s.PoEs.Count),
        (Mod.Gre, s => s.Gres.Count), (Mod.Voice, s => s.Voices.Count),
        (Mod.Ipv6, s => s.Ipv6.Enabled ? 1 : 0), (Mod.Isolate, s => s.Isolate.Enabled ? 1 : 0)
    ];

    private void AutoEnableModulesWithData(List<string> addedNames)
    {
        var supported = Mod.For(_deviceType).Select(m => m.Key).ToHashSet();
        foreach (var (key, count) in DataModules)
        {
            if (count(_spec) <= 0 || !supported.Contains(key) || _spec.EnabledModules.Contains(key)) continue;
            _spec.EnabledModules.Add(key);
            var item = _modules.FirstOrDefault(m => m.Key == key);
            if (item != null) item.Enabled = true;
            addedNames.Add(item?.Name ?? key);
        }
        if (addedNames.Count > 0)
            TxtModuleCount.Text = $"已选 {_spec.EnabledModules.Count} 个模块";
    }

    private void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        if (_spec.EnabledModules.Count == 0)
        {
            _setStatus("请至少勾选一个功能模块");
            MessageBox.Show("请先在左侧勾选至少一个功能模块。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        CommitGrids();

        // 自动补勾：页签里填了数据但漏勾模块（如端口映射只填了表格、没勾模块）→ 避免“填了不生成”的静默
        var autoAdded = new List<string>();
        AutoEnableModulesWithData(autoAdded);

        try
        {
            var text = GeneratorFactory.Get(_vendor).Generate(_spec, _deviceType);

            // 生成前预检：把校验提醒注入输出头部（仅提醒，不阻断生成）
            var warns = ValidateWarnings();
            if (warns.Count > 0)
            {
                var token = _vendor == Vendor.Cisco ? "!" : "#";
                var block = new System.Text.StringBuilder();
                block.Append('\n').Append(token).Append(" ⚠ 生成前校验提醒（不影响生成，请逐条确认）：");
                for (var i = 0; i < warns.Count; i++)
                    block.Append('\n').Append(token).Append("  ").Append(i + 1).Append(". ").Append(warns[i]);
                block.Append('\n');
                var nl = text.IndexOf('\n');
                text = nl >= 0 ? text.Insert(nl + 1, block.ToString()) : text + block;
            }

            _currentConfig = text;
            OutputBox.Document = Highlighter.Render(text);
            AutoRememberRecent(); // 成功生成后自动记忆方案（自吞异常）

            var lines = text.Split('\n').Length;
            TxtOutputStat.Text = $"{lines} 行 · {text.Length} 字符";
            var autoNote = autoAdded.Count > 0 ? $"｜已自动勾选漏勾模块：{string.Join("、", autoAdded)}" : "";
            _setStatus(warns.Count == 0
                ? $"已生成 {EnumText.VendorShort(_vendor)}{EnumText.DeviceName(_deviceType)} 配置：{lines} 行（校验无提醒）{autoNote}"
                : $"已生成 {EnumText.VendorShort(_vendor)}{EnumText.DeviceName(_deviceType)} 配置：{lines} 行，含 {warns.Count} 条参数提醒（见输出头部）{autoNote}");
        }
        catch (Exception ex)
        {
            _setStatus("生成失败：" + ex.Message);
            MessageBox.Show("生成配置时出错：\n" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_currentConfig.Length == 0)
        {
            _setStatus("请先生成配置");
            return;
        }
        // 复制剥离 # / ! 注释行后的纯命令：直接粘贴到设备终端执行不会触发 % Unknown command
        var plain = Inspector.StripComments(_currentConfig, _vendor);
        Clipboard.SetText(plain);
        _setStatus($"已复制 {plain.Split('\n').Length} 行纯命令（已去掉注释），可直接粘贴到设备终端");
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (_currentConfig.Length == 0)
        {
            _setStatus("请先生成配置");
            return;
        }

        var dlg = new SaveFileDialog
        {
            FileName = $"{TextUtil.SanitizeFileName(_spec.Basic.Hostname)}_{_vendor}_{_deviceType}.cfg",
            DefaultExt = ".cfg",
            Filter = "配置文件 (*.cfg)|*.cfg|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        File.WriteAllText(dlg.FileName, _currentConfig, System.Text.Encoding.UTF8);
        _setStatus($"已导出：{dlg.FileName}");
    }

    private void BtnSaveVersion_Click(object sender, RoutedEventArgs e)
    {
        if (_currentConfig.Length == 0)
        {
            _setStatus("请先生成配置");
            return;
        }

        var dlg = new PromptDialog("保存配置版本", "请填写本次变更说明（可留空）：");
        if (dlg.ShowDialog() != true) return;

        var ver = new ConfigVersion
        {
            DeviceName = _spec.Basic.Hostname,
            Vendor = _vendor,
            DeviceType = _deviceType,
            VersionNo = _store.NextVersionNo(_spec.Basic.Hostname),
            Note = dlg.InputText,
            Content = _currentConfig
        };
        _store.Versions.Insert(0, ver);
        _store.SaveVersions();
        _setStatus($"已保存版本 v{ver.VersionNo}（{ver.DeviceName}）");
    }

    private void BtnSaveTemplate_Click(object sender, RoutedEventArgs e)
    {
        CommitGrids();
        var dlg = new PromptDialog("保存为模板", "模板名称：", $"{EnumText.VendorShort(_vendor)}{EnumText.DeviceName(_deviceType)}模板");
        if (dlg.ShowDialog() != true || dlg.InputText.Length == 0) return;

        _store.Templates.Insert(0, new ConfigTemplate
        {
            Name = dlg.InputText,
            Vendor = _vendor,
            DeviceType = _deviceType,
            SpecJson = AppStorage.Serialize(_spec)
        });
        _store.SaveTemplates();
        _setStatus($"模板已保存：{dlg.InputText}");
    }

    private sealed class ModuleItem : INotifyPropertyChanged
    {
        private bool _enabled;
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public string Tip { get; set; } = "";

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
