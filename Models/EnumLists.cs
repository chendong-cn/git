namespace NetConfigPro.Models;

/// <summary>供 XAML 下拉列表静态绑定的取值集合。</summary>
public static class EnumLists
{
    public static InterfaceMode[] InterfaceModes => [InterfaceMode.Access, InterfaceMode.Trunk, InterfaceMode.Routed];
    public static AclKind[] AclKinds => [AclKind.Standard, AclKind.Extended];
    public static RoutingProtocol[] RoutingProtocols => [RoutingProtocol.Static, RoutingProtocol.Ospf, RoutingProtocol.Bgp, RoutingProtocol.Rip, RoutingProtocol.Eigrp, RoutingProtocol.None];
    public static StpMode[] StpModes => [StpMode.Rstp, StpMode.Mstp, StpMode.Stp, StpMode.Pvst, StpMode.RapidPvst];
    public static WpaMode[] WpaModes => [WpaMode.Wpa2Psk, WpaMode.Wpa2Wpa3, WpaMode.Wpa3Sae];
    public static string[] PermitDeny => ["permit", "deny"];
    public static string[] Protocols => ["ip", "tcp", "udp", "icmp"];
    public static string[] Services => ["any", "http", "https", "ssh", "telnet", "ftp", "dns", "icmp", "rdp"];
    public static string[] AreaTypes => ["Normal", "Stub", "NSSA"];
    public static string[] SnmpVersions => ["v1", "v2c", "v3"];
    public static string[] RadioBands => ["2.4G", "5G"];
    public static string[] SshVersions => ["1", "2", "1.99"];
    public static string[] WanModes => ["固定 IP", "DHCP 自动获取", "PPPoE 拨号"];
    public static string[] NatKinds => ["Easy IP（PAT 出网）", "静态端口映射"];
    public static string[] ZonePresets => ["Trust", "Untrust", "DMZ", "Management", "Local"];

    // 扩展类别取值
    public static string[] AggModes => ["lacp", "static"];
    public static string[] MirrorDirections => ["both", "inbound", "outbound"];
    public static string[] ViolationModes => ["restrict", "protect", "shutdown"];
    public static string[] PortControls => ["auto", "force-authorized"];
    public static string[] LinkModes => ["access", "trunk"];
}
