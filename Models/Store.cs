using System.Collections.ObjectModel;

namespace NetConfigPro.Models;

/// <summary>配置模板：保存一套完整的参数集合，便于同类设备快速套用。</summary>
public class ConfigTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Category { get; set; } = "通用";
    public string Description { get; set; } = "";
    public Vendor Vendor { get; set; }
    public DeviceType DeviceType { get; set; }
    public string SpecJson { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [System.Text.Json.Serialization.JsonIgnore] public string VendorText => EnumText.VendorShort(Vendor);
    [System.Text.Json.Serialization.JsonIgnore] public string DeviceTypeText => EnumText.DeviceName(DeviceType);
}

/// <summary>配置版本：每次保存生成一条历史记录，可回滚与对比。</summary>
public class ConfigVersion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DeviceName { get; set; } = "";
    public Vendor Vendor { get; set; }
    public DeviceType DeviceType { get; set; }
    public int VersionNo { get; set; } = 1;
    public string Note { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [System.Text.Json.Serialization.JsonIgnore] public string VendorText => EnumText.VendorShort(Vendor);
    [System.Text.Json.Serialization.JsonIgnore] public string DeviceTypeText => EnumText.DeviceName(DeviceType);
    [System.Text.Json.Serialization.JsonIgnore] public string Display => $"v{VersionNo} · {DeviceName} · {CreatedAt:MM-dd HH:mm}";
}

/// <summary>设备资产条目，用于批量部署。</summary>
public class DeviceEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public Vendor Vendor { get; set; }
    public DeviceType DeviceType { get; set; }
    public string Model { get; set; } = "";
    public string Ip { get; set; } = "";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "admin";
    public string EncryptedPassword { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [System.Text.Json.Serialization.JsonIgnore] public string VendorText => EnumText.VendorShort(Vendor);
    [System.Text.Json.Serialization.JsonIgnore] public string DeviceText => EnumText.DeviceName(DeviceType);
    [System.Text.Json.Serialization.JsonIgnore] public bool IsSelected { get; set; }
}

/// <summary>页面之间传递的临时数据（模板套用、版本对比）。</summary>
public static class AppState
{
    public static ConfigSpec? PendingSpec { get; set; }
    public static Vendor PendingVendor { get; set; }
    public static DeviceType PendingDeviceType { get; set; }
    public static string? CompareLeft { get; set; }
    public static string? CompareRight { get; set; }
}

public class DeploymentLog
{
    public string DeviceName { get; set; } = "";
    public string DeviceIp { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime Time { get; set; } = DateTime.Now;
}

public class DeploymentTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string ConfigContent { get; set; } = "";
    public string Status { get; set; } = "待执行";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public ObservableCollection<DeploymentLog> Logs { get; set; } = new();
}

/// <summary>最近方案：保存厂商×设备×参数集的快照，便于一键恢复。</summary>
public class RecentProfile
{
    public string Name { get; set; } = "";
    public string Vendor { get; set; } = "";
    public string DeviceType { get; set; } = "";
    public string SpecJson { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public string Display => $"{Name}  ·  {Vendor} {DeviceType}  ·  {UpdatedAt:MM-dd HH:mm}";
}
