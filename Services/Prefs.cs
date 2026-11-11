using System.Linq;
using System.Text.Json;
using NetConfigPro.Models;

namespace NetConfigPro.Services;

/// <summary>一条“厂商×设备类型”的模块勾选偏好（自动记忆，重启后该组合自动恢复）。</summary>
public sealed class VendorPref
{
    public string Vendor { get; set; } = "";
    public string DeviceType { get; set; } = "";
    /// <summary>模块 key 列表（逗号分隔）。</summary>
    public string KeysCsv { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public string Display => $"{Vendor} · {DeviceType}  {UpdatedAt:MM-dd HH:mm}";
}

/// <summary>每厂商×设备类型的勾选偏好存取（prefs.json，本地 JSON）。</summary>
public static class Prefs
{
    private static readonly object Gate = new();
    private static List<VendorPref>? _cache;

    private static List<VendorPref> Load()
    {
        if (_cache is null)
        {
            lock (Gate)
            {
                _cache ??= AppStorage.Load<VendorPref>("prefs.json");
            }
        }
        return _cache!;
    }

    private static void Persist()
    {
        lock (Gate)
        {
            AppStorage.Save("prefs.json", Load().Take(64)); // 防无限增长
        }
    }

    /// <summary>取某组合偏好（无则 null）。</summary>
    public static VendorPref? Get(string vendor, string deviceType)
    {
        var key = (vendor ?? "").Trim();
        var dt = (deviceType ?? "").Trim();
        return Load().FirstOrDefault(p => p.Vendor == key && p.DeviceType == dt);
    }

    /// <summary>保存/更新某组合偏好。</summary>
    public static void Set(string vendor, string deviceType, IEnumerable<string> keys)
    {
        var list = Load();
        var keyList = keys.Distinct().ToList();
        var item = Get(vendor, deviceType);
        if (item is null)
        {
            list.Insert(0, new VendorPref
            {
                Vendor = vendor,
                DeviceType = deviceType,
                KeysCsv = string.Join(',', keyList),
                UpdatedAt = DateTime.Now
            });
        }
        else
        {
            item.KeysCsv = string.Join(',', keyList);
            item.UpdatedAt = DateTime.Now;
        }
        Persist();
    }

    /// <summary>清除某组合偏好。</summary>
    public static void Clear(string vendor, string deviceType)
    {
        Load().RemoveAll(p => p.Vendor == vendor && p.DeviceType == deviceType);
        Persist();
    }

    public static HashSet<string> KeysOf(VendorPref? pref) =>
        pref is null || string.IsNullOrWhiteSpace(pref.KeysCsv)
            ? []
            : pref.KeysCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
}
