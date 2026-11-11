using System.Collections.ObjectModel;

namespace NetConfigPro.Models;

/// <summary>一台设备待生成的全部配置参数集合。</summary>
public class ConfigSpec
{
    public BasicConfig Basic { get; set; } = new();
    public ObservableCollection<InterfaceItem> Interfaces { get; set; } = new();
    public ObservableCollection<VlanItem> Vlans { get; set; } = new();
    public ObservableCollection<StaticRouteItem> StaticRoutes { get; set; } = new();
    public RoutingConfig Routing { get; set; } = new();
    public ObservableCollection<OspfNetworkItem> OspfNetworks { get; set; } = new();
    public ObservableCollection<BgpNeighborItem> BgpNeighbors { get; set; } = new();
    public ObservableCollection<AclRuleItem> AclRules { get; set; } = new();
    public NatConfig Nat { get; set; } = new();
    public SecurityConfig Security { get; set; } = new();
    public SnmpConfig Snmp { get; set; } = new();
    public SshConfig Ssh { get; set; } = new();
    public ObservableCollection<DhcpPoolItem> DhcpPools { get; set; } = new();
    public StpConfig Stp { get; set; } = new();
    public WirelessConfig Wireless { get; set; } = new();
    public ObservableCollection<ZoneItem> Zones { get; set; } = new();
    public ObservableCollection<PolicyItem> Policies { get; set; } = new();

    // ---- 扩展配置类别（v2）----
    public ObservableCollection<LinkAggItem> LinkAggs { get; set; } = new();
    public ObservableCollection<VrrpItem> Vrrps { get; set; } = new();
    public DhcpSnoopConfig DhcpSnoop { get; set; } = new();
    public ObservableCollection<MirrorItem> Mirrors { get; set; } = new();
    public ObservableCollection<PortSecItem> PortSecs { get; set; } = new();
    public Dot1xConfig Dot1x { get; set; } = new();
    public LldpConfig Lldp { get; set; } = new();

    // ---- 扩展配置类别（v3：远程管理接入）----
    public ObservableCollection<LocalUserItem> LocalUsers { get; set; } = new();
    public TelnetConfig Telnet { get; set; } = new();
    public WebAdminConfig WebAdmin { get; set; } = new();

    // ---- 扩展配置类别（v6：NAT 深化 + 常用功能补齐）----
    public ObservableCollection<PortMapItem> PortMaps { get; set; } = new();
    public ObservableCollection<StaticNatItem> StaticNats { get; set; } = new();
    public ObservableCollection<DhcpRelayItem> DhcpRelays { get; set; } = new();
    public ObservableCollection<DhcpBindItem> DhcpBinds { get; set; } = new();
    public ObservableCollection<ArpBindItem> ArpBinds { get; set; } = new();
    public ObservableCollection<StormItem> Storms { get; set; } = new();
    public IgmpSnoopConfig Igmp { get; set; } = new();

    // ---- 扩展配置类别（v11：策略路由 + QoS）----
    public ObservableCollection<PbrItem> Pbrs { get; set; } = new();
    public ObservableCollection<QosItem> Qos { get; set; } = new();

    // ---- 扩展配置类别（v12：ACL 时间段 + BFD + PoE）----
    public ObservableCollection<TimeRangeItem> TimeRanges { get; set; } = new();
    public ObservableCollection<BfdItem> Bfds { get; set; } = new();
    public ObservableCollection<PoEItem> PoEs { get; set; } = new();

    // ---- 扩展配置类别（v13：IPv6 / GRE / 语音 VLAN / 端口隔离）----
    public Ipv6Config Ipv6 { get; set; } = new();
    public ObservableCollection<GreItem> Gres { get; set; } = new();
    public ObservableCollection<VoiceItem> Voices { get; set; } = new();
    public IsolateConfig Isolate { get; set; } = new();

    /// <summary>被勾选生成的功能模块。</summary>
    public HashSet<string> EnabledModules { get; set; } = new();
}

public class BasicConfig
{
    public string Hostname { get; set; } = "SW-Core-01";
    public string DomainName { get; set; } = "";
    public int MgmtVlanId { get; set; } = 100;
    public string MgmtIp { get; set; } = "192.168.100.1";
    public string MgmtMask { get; set; } = "255.255.255.0";
    public string DefaultGateway { get; set; } = "192.168.100.254";
    public string DnsServers { get; set; } = "223.5.5.5 114.114.114.114";
    public string NtpServer { get; set; } = "";
    public string SyslogHost { get; set; } = "";
    public string TimeZone { get; set; } = "Beijing";

    // 上网配置（路由器 / 防火墙）
    public WanMode WanMode { get; set; } = WanMode.StaticIp;
    public string WanInterface { get; set; } = "";
    public string WanIp { get; set; } = "1.1.1.2";
    public string WanMask { get; set; } = "255.255.255.252";
    public string WanGateway { get; set; } = "1.1.1.1";
    public string PppoeUser { get; set; } = "user@isp";
    public string PppoePassword { get; set; } = "password";
    public string LanInterface { get; set; } = "";
    public string LanIp { get; set; } = "192.168.1.1";
    public string LanMask { get; set; } = "255.255.255.0";
}

public class InterfaceItem
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public InterfaceMode Mode { get; set; } = InterfaceMode.Access;
    public int AccessVlan { get; set; } = 1;
    public string TrunkVlans { get; set; } = "10,20";
    public string IpAddress { get; set; } = "";
    public string Mask { get; set; } = "255.255.255.0";
    public bool Enabled { get; set; } = true;
}

public class VlanItem
{
    public int Id { get; set; } = 10;
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool L3Enabled { get; set; }
    public string IpAddress { get; set; } = "";
    public string Mask { get; set; } = "255.255.255.0";
}

public class StaticRouteItem
{
    public string Prefix { get; set; } = "0.0.0.0";
    public string Mask { get; set; } = "0.0.0.0";
    public string NextHop { get; set; } = "";
    public string Preference { get; set; } = "";
    public string Description { get; set; } = "";
}

public class RoutingConfig
{
    public RoutingProtocol Protocol { get; set; } = RoutingProtocol.Static;
    public string RouterId { get; set; } = "1.1.1.1";
    public int OspfProcess { get; set; } = 1;
    public int BgpAs { get; set; } = 65001;
    public int EigrpAs { get; set; } = 100;
    public bool RedistributeConnected { get; set; }
    public bool DefaultRouteAdvertise { get; set; }
}

public class OspfNetworkItem
{
    public string Network { get; set; } = "10.0.0.0";
    public string Wildcard { get; set; } = "0.0.0.3";
    public string Area { get; set; } = "0";
    public string AreaType { get; set; } = "Normal";
}

public class BgpNeighborItem
{
    public string PeerIp { get; set; } = "10.0.0.2";
    public int RemoteAs { get; set; } = 65002;
    public string Description { get; set; } = "";
}

public class AclRuleItem
{
    public string Number { get; set; } = "2000";
    public AclKind Kind { get; set; } = AclKind.Standard;
    public string Action { get; set; } = "permit";
    public string Protocol { get; set; } = "ip";
    public string Source { get; set; } = "192.168.1.0";
    public string SrcWildcard { get; set; } = "0.0.0.255";
    public string Destination { get; set; } = "any";
    public string DstWildcard { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>可引用「ACL 时间段」模块定义的 time-range 名称；留空表示全天生效。</summary>
    public string TimeRange { get; set; } = "";
}

public class NatConfig
{
    public NatKind Kind { get; set; } = NatKind.EasyIp;
    public string InsideInterface { get; set; } = "";
    public string OutsideInterface { get; set; } = "";
    public string AclNumber { get; set; } = "2000";
    public string Protocol { get; set; } = "tcp";
    public string GlobalIp { get; set; } = "1.1.1.2";
    public string GlobalPort { get; set; } = "80";
    public string InsideIp { get; set; } = "192.168.1.10";
    public string InsidePort { get; set; } = "80";
}

public class SecurityConfig
{
    public string LocalUser { get; set; } = "admin";
    public string Password { get; set; } = "Admin@123";
    public int Privilege { get; set; } = 15;
    public string EnablePassword { get; set; } = "Enable@123";
    public int IdleTimeout { get; set; } = 10;
    public int LoginRetryLimit { get; set; } = 3;
    public int LockTime { get; set; } = 5;
    public int PasswordMinLength { get; set; } = 8;
    public bool AntiAttack { get; set; } = true;
}

public class SnmpConfig
{
    public string Version { get; set; } = "v2c";
    public string Community { get; set; } = "public";
    public string TrapHost { get; set; } = "";
    public string Location { get; set; } = "Chengdu-IDC";
    public string Contact { get; set; } = "admin@example.com";

    // SNMPv3 凭据（版本为 v3 时生效）
    public string V3User { get; set; } = "netuser";
    public string V3AuthPassword { get; set; } = "Auth@123";
    public string V3PrivPassword { get; set; } = "Priv@123";
}

public class SshConfig
{
    public bool Enabled { get; set; } = true;
    public string Version { get; set; } = "2";
    public int AuthTimeout { get; set; } = 60;
    public int AuthRetries { get; set; } = 3;
    public int Port { get; set; } = 22;
}

public class DhcpPoolItem
{
    public string Name { get; set; } = "VLAN10";
    public string Network { get; set; } = "192.168.10.0";
    public string Mask { get; set; } = "255.255.255.0";
    public string Gateway { get; set; } = "192.168.10.1";
    public string Dns1 { get; set; } = "223.5.5.5";
    public string Dns2 { get; set; } = "114.114.114.114";
    public string ExcludeFrom { get; set; } = "192.168.10.1";
    public string ExcludeTo { get; set; } = "192.168.10.20";
    public int LeaseDays { get; set; } = 1;
    public int VlanId { get; set; } = 10;
}

public class StpConfig
{
    public StpMode Mode { get; set; } = StpMode.Rstp;
    public int Priority { get; set; } = 4096;
    public bool RootPrimary { get; set; } = true;
    public string RegionName { get; set; } = "RG1";
    public string InstanceMap { get; set; } = "1:10-20";
}

public class WirelessConfig
{
    public string Ssid { get; set; } = "Office-WiFi";
    public string Password { get; set; } = "WiFi@2026";
    public WpaMode Wpa { get; set; } = WpaMode.Wpa2Psk;
    public int VlanId { get; set; } = 10;
    public string RadioBand { get; set; } = "2.4G";
    public string Channel { get; set; } = "auto";
    public int Power { get; set; } = 100;
    public int MaxUsers { get; set; } = 32;
    public bool HideSsid { get; set; }
    public string CountryCode { get; set; } = "CN";
}

public class ZoneItem
{
    public string Name { get; set; } = "Trust";
    public string Interfaces { get; set; } = "";
    public int Priority { get; set; } = 85;
}

public class PolicyItem
{
    public string Name { get; set; } = "permit-web";
    public string Action { get; set; } = "permit";
    public string SourceZone { get; set; } = "Trust";
    public string DestZone { get; set; } = "Untrust";
    public string Source { get; set; } = "192.168.1.0/24";
    public string Destination { get; set; } = "any";
    public string Service { get; set; } = "http";
    public string Description { get; set; } = "";
}

// ==================== 扩展类别模型 ====================

/// <summary>链路聚合（华为 Eth-Trunk / 华三 Bridge-Aggregation / 思科与锐捷 Port-Channel）。</summary>
public class LinkAggItem
{
    public int Id { get; set; } = 1;
    public string Description { get; set; } = "Uplink 聚合";
    /// <summary>lacp=动态 LACP，static=手工静态。</summary>
    public string Mode { get; set; } = "lacp";
    /// <summary>成员接口，空格/逗号分隔，支持 "A to B" 区间写法。</summary>
    public string Members { get; set; } = "";
}

/// <summary>VRRP 网关冗余（对接接口需先有三层地址）。</summary>
public class VrrpItem
{
    public string Interface { get; set; } = "Vlanif10";
    public int GroupId { get; set; } = 1;
    public string VirtualIp { get; set; } = "192.168.10.254";
    public int Priority { get; set; } = 100;
    public bool Preempt { get; set; } = true;
    public string Description { get; set; } = "";
}

/// <summary>DHCP Snooping（仅交换机）。</summary>
public class DhcpSnoopConfig
{
    public string VlanIds { get; set; } = "10,20";
    /// <summary>信任接口（通常为上行），空格/逗号分隔。</summary>
    public string TrustPorts { get; set; } = "";
    public bool Option82 { get; set; }
}

/// <summary>端口镜像（仅交换机）。Direction: both/inbound/outbound。</summary>
public class MirrorItem
{
    public int SessionId { get; set; } = 1;
    public string SourcePorts { get; set; } = "";
    public string DestPort { get; set; } = "";
    public string Direction { get; set; } = "both";
}

/// <summary>端口安全 MAC 限制（仅交换机）。</summary>
public class PortSecItem
{
    public string Interface { get; set; } = "";
    public int MaxMac { get; set; } = 5;
    public bool Sticky { get; set; } = true;
    /// <summary>restrict / protect / shutdown。</summary>
    public string Violation { get; set; } = "restrict";
}

/// <summary>802.1X / RADIUS 接入认证（仅交换机）。</summary>
public class Dot1xConfig
{
    public bool Enabled { get; set; } = true;
    public string ServerIp { get; set; } = "";
    public string SharedKey { get; set; } = "";
    public string AuthPort { get; set; } = "1812";
    public string AcctPort { get; set; } = "1813";
    /// <summary>应用到指定接入接口，留空仅启用全局认证。</summary>
    public string Interface { get; set; } = "";
    public string PortControl { get; set; } = "auto";
}

/// <summary>LLDP 链路层发现协议。</summary>
public class LldpConfig
{
    public bool Enabled { get; set; } = true;
}

// ==================== v3 远程管理模型 ====================

/// <summary>本地用户账号（接入 SSH/Telnet/Web/控制台）。</summary>
public class LocalUserItem
{
    public string Name { get; set; } = "admin";
    public string Password { get; set; } = "Admin@123";
    public int Privilege { get; set; } = 15;
    /// <summary>允许的接入方式：ssh/telnet/http/https/terminal，空格分隔。</summary>
    public string Services { get; set; } = "ssh terminal";
    public bool Active { get; set; } = true;
}

/// <summary>Telnet 接入管理。</summary>
public class TelnetConfig
{
    public bool Enabled { get; set; }
    /// <summary>可选的来源限制 ACL 编号（交换机/路由器侧）。</summary>
    public string AclId { get; set; } = "";
}

/// <summary>Web 管理（HTTP/HTTPS 图形化管理）。</summary>
public class WebAdminConfig
{
    public bool HttpEnabled { get; set; } = true;
    public bool HttpsEnabled { get; set; }
    public string Port { get; set; } = "";
    /// <summary>来源限制：留空=任意，或填 IP/网段说明。</summary>
    public string AllowSource { get; set; } = "";
}

// ==================== v6 功能补齐模型 ====================

/// <summary>端口映射（NAT Server，把公网端口发布到内网服务器）。</summary>
public class PortMapItem
{
    public string Name { get; set; } = "";
    public string Protocol { get; set; } = "tcp";
    /// <summary>公网 IP：留空表示使用接口地址。</summary>
    public string ExternalIp { get; set; } = "";
    public string ExternalPort { get; set; } = "80";
    public string InsideIp { get; set; } = "";
    public string InsidePort { get; set; } = "80";
    public string Description { get; set; } = "";
}

/// <summary>静态 NAT 一对一（把公网 IP 对映内网服务器）。</summary>
public class StaticNatItem
{
    public string GlobalIp { get; set; } = "";
    public string InsideIp { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>DHCP 中继：把 VLAN 的 DHCP 请求转发给远端服务器。</summary>
public class DhcpRelayItem
{
    public string Interface { get; set; } = "Vlanif10";
    public string ServerIp { get; set; } = "";
}

/// <summary>DHCP 静态绑定（按 MAC 固定分配 IP）。</summary>
public class DhcpBindItem
{
    public string Pool { get; set; } = "VLAN10";
    public string Mac { get; set; } = "";
    public string Ip { get; set; } = "";
    public string Mask { get; set; } = "255.255.255.0";
    public string Description { get; set; } = "";
}

/// <summary>IP-MAC 静态绑定（ARP 防欺骗）。</summary>
public class ArpBindItem
{
    public string Ip { get; set; } = "";
    public string Mac { get; set; } = "";
    /// <summary>思科/锐捷在接口视图生效，可留空用全局近似。</summary>
    public string Interface { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>风暴抑制（广播/组播/未知单播限速）。</summary>
public class StormItem
{
    public string Interface { get; set; } = "";
    /// <summary>广播抑制百分比（留空=不限制）。</summary>
    public string BroadcastPct { get; set; } = "";
    public string MulticastPct { get; set; } = "";
    public string UnknownUcPct { get; set; } = "";
}

/// <summary>IGMP Snooping（组播侦听，仅交换机）。</summary>
public class IgmpSnoopConfig
{
    public bool Enabled { get; set; } = true;
    /// <summary>需要开启 snooping 的 VLAN，逗号分隔，支持区间。</summary>
    public string VlanIds { get; set; } = "";
}

/// <summary>策略路由（PBR）：按 ACL 匹配流量重定向到指定下一跳/出接口。</summary>
public class PbrItem
{
    public string Name { get; set; } = "pbr-to-mpls";
    /// <summary>匹配流量的 ACL 编号（需先在 ACL 模块中定义）。</summary>
    public string MatchAcl { get; set; } = "3000";
    /// <summary>动作：next-hop / interface（二选一，优先 next-hop）。</summary>
    public string Mode { get; set; } = "next-hop";
    public string NextHop { get; set; } = "10.1.1.254";
    public string ExitInterface { get; set; } = "";
    /// <summary>应用策略的入接口。</summary>
    public string ApplyOn { get; set; } = "GigabitEthernet0/0/1";
    public string Description { get; set; } = "";
}

/// <summary>QoS 接口限速（承诺速率，单位 kbps）。</summary>
public class QosItem
{
    public string Interface { get; set; } = "GigabitEthernet0/0/1";
    /// <summary>限制方向：inbound / outbound / both。</summary>
    public string Direction { get; set; } = "outbound";
    /// <summary>承诺速率（kbps）。</summary>
    public string CirKbps { get; set; } = "2048";
    public string Description { get; set; } = "";
}

/// <summary>ACL 生效时间段：time-range 定义，供 ACL 规则引用实现定时放行/阻断。</summary>
public class TimeRangeItem
{
    public string Name { get; set; } = "office-hours";
    /// <summary>生效日期：daily / working-day / off-day。</summary>
    public string Days { get; set; } = "working-day";
    public string StartTime { get; set; } = "08:00";
    public string EndTime { get; set; } = "18:00";
    public string Description { get; set; } = "";
}

/// <summary>BFD 双向转发检测会话（毫秒级故障检测，配合路由使用）。</summary>
public class BfdItem
{
    public string PeerIp { get; set; } = "10.0.0.2";
    public string Interface { get; set; } = "";
    /// <summary>发送/接收间隔（毫秒，思科/锐捷按此输出 min_rx）。</summary>
    public string MinTxMs { get; set; } = "100";
    public string Multiplier { get; set; } = "3";
    public string Description { get; set; } = "";
}

/// <summary>PoE 供电（交换机下联 AP/摄像头/面板的供电管理）。</summary>
public class PoEItem
{
    public string Interface { get; set; } = "GigabitEthernet0/0/1";
    public bool Enabled { get; set; } = true;
    /// <summary>供电优先级：critical / high / low。</summary>
    public string Priority { get; set; } = "critical";
    public string Description { get; set; } = "";
}

// ==================== v13 模型：IPv6 / GRE / 语音 VLAN / 端口隔离 ====================

/// <summary>IPv6 基础配置：单个三层接口的 IPv6 地址与前缀，可下发 ND 路由通告。</summary>
public class Ipv6Config
{
    public bool Enabled { get; set; } = true;
    /// <summary>三层接口（Vlanif10 / GigabitEthernet0/0/1 等）。</summary>
    public string Interface { get; set; } = "Vlanif10";
    /// <summary>IPv6 地址含前缀长度，如 2001:db8:10::1/64。</summary>
    public string Address { get; set; } = "2001:db8:10::1/64";
    /// <summary>是否允许发送 RA 路由通告（终端自动获取地址/网关）。</summary>
    public bool NdRa { get; set; } = true;
    public string Description { get; set; } = "";
}

/// <summary>GRE 隧道：两站点内网经公网轻量互通（不加密，敏感流量请另配 IPSec）。</summary>
public class GreItem
{
    /// <summary>隧道接口名（华为/华三 Tunnel0/0/1，思科/锐捷 Tunnel0）。</summary>
    public string Name { get; set; } = "Tunnel0/0/1";
    /// <summary>本端公网地址（tunnel source）。</summary>
    public string Source { get; set; } = "";
    /// <summary>对端公网地址（tunnel destination）。</summary>
    public string Destination { get; set; } = "";
    /// <summary>隧道内网地址（私网互联地址，掩码默认 30 位）。</summary>
    public string Address { get; set; } = "172.16.0.1";
    public string Mask { get; set; } = "255.255.255.252";
    public string Description { get; set; } = "";
}

/// <summary>语音 VLAN：接 IP 话机的口数据走 PVID、语音 untagged 进语音 VLAN。</summary>
public class VoiceItem
{
    public int DataVlan { get; set; } = 10;
    public int VoiceVlan { get; set; } = 100;
    /// <summary>话机端口，空格/逗号分隔，支持 "A to B" 区间（同 ExpandMembers 规则）。</summary>
    public string Ports { get; set; } = "";
    /// <summary>auto=按 CDP/LLDP 识别话机（思科），manual=强制打语音 VLAN 标签（华为/华三）。</summary>
    public string Mode { get; set; } = "auto";
    public string Description { get; set; } = "";
}

/// <summary>端口隔离：同隔离组内端口二层互不通、均可访问上联口（仅交换机）。</summary>
public class IsolateConfig
{
    public bool Enabled { get; set; } = true;
    /// <summary>隔离组号（华为/华三 1-64；思科/锐捷为全局 protected，忽略组号）。</summary>
    public int GroupId { get; set; } = 1;
    /// <summary>加入隔离组的端口，支持区间写法。</summary>
    public string Ports { get; set; } = "";
    /// <summary>免隔离的上联口（可留空，上联口本就不在组内）。</summary>
    public string Uplink { get; set; } = "";
}
