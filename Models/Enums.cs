namespace NetConfigPro.Models;

public enum Vendor
{
    Huawei,
    H3C,
    Cisco,
    Ruijie
}

public enum DeviceType
{
    Router,
    Firewall,
    Switch,
    AccessPoint
}

public enum WanMode
{
    StaticIp,
    Dhcp,
    Pppoe
}

public enum InterfaceMode
{
    Access,
    Trunk,
    Routed
}

public enum RoutingProtocol
{
    None,
    Static,
    Ospf,
    Bgp,
    Eigrp,
    Rip
}

public enum AclKind
{
    Standard,
    Extended
}

public enum NatKind
{
    EasyIp,
    StaticNat
}

public enum StpMode
{
    Stp,
    Rstp,
    Mstp,
    Pvst,
    RapidPvst
}

public enum WpaMode
{
    Wpa2Psk,
    Wpa3Sae,
    Wpa2Wpa3
}

public static class EnumText
{
    public static string VendorName(Vendor v) => v switch
    {
        Vendor.Huawei => "华为 Huawei",
        Vendor.H3C => "华三 H3C",
        Vendor.Cisco => "思科 Cisco",
        Vendor.Ruijie => "锐捷 Ruijie",
        _ => v.ToString()
    };

    public static string VendorShort(Vendor v) => v switch
    {
        Vendor.Huawei => "华为",
        Vendor.H3C => "华三",
        Vendor.Cisco => "思科",
        Vendor.Ruijie => "锐捷",
        _ => v.ToString()
    };

    public static string DeviceName(DeviceType d) => d switch
    {
        DeviceType.Router => "路由器",
        DeviceType.Firewall => "防火墙",
        DeviceType.Switch => "交换机",
        DeviceType.AccessPoint => "无线 AP",
        _ => d.ToString()
    };

    public static string OsName(Vendor v) => v switch
    {
        Vendor.Huawei => "VRP",
        Vendor.H3C => "Comware V7",
        Vendor.Cisco => "IOS / IOS-XE",
        Vendor.Ruijie => "RGOS",
        _ => ""
    };
}
