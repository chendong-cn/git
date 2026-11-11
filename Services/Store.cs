using System.Collections.ObjectModel;
using NetConfigPro.Models;

namespace NetConfigPro.Services;

/// <summary>统一的本地数据仓库：模板、版本、设备、部署任务。</summary>
public sealed class Store
{
    private const string TemplateFile = "templates.json";
    private const string VersionFile = "versions.json";
    private const string DeviceFile = "devices.json";
    private const string TaskFile = "tasks.json";

    public ObservableCollection<ConfigTemplate> Templates { get; } = new();
    public ObservableCollection<ConfigVersion> Versions { get; } = new();
    public ObservableCollection<DeviceEntry> Devices { get; } = new();
    public ObservableCollection<DeploymentTask> Tasks { get; } = new();

    public Store() => LoadAll();

    public void LoadAll()
    {
        Templates.Clear();
        foreach (var t in AppStorage.Load<ConfigTemplate>(TemplateFile).OrderByDescending(x => x.UpdatedAt))
            Templates.Add(t);

        Versions.Clear();
        foreach (var v in AppStorage.Load<ConfigVersion>(VersionFile).OrderByDescending(x => x.CreatedAt))
            Versions.Add(v);

        Devices.Clear();
        foreach (var d in AppStorage.Load<DeviceEntry>(DeviceFile).OrderBy(x => x.Name))
            Devices.Add(d);

        Tasks.Clear();
        foreach (var t in AppStorage.Load<DeploymentTask>(TaskFile).OrderByDescending(x => x.CreatedAt))
            Tasks.Add(t);
    }

    public void SaveTemplates() => AppStorage.Save(TemplateFile, Templates);
    public void SaveVersions() => AppStorage.Save(VersionFile, Versions);
    public void SaveDevices() => AppStorage.Save(DeviceFile, Devices);
    public void SaveTasks() => AppStorage.Save(TaskFile, Tasks);

    public int NextVersionNo(string deviceName)
    {
        var max = Versions.Where(v => v.DeviceName == deviceName).Select(v => v.VersionNo).DefaultIfEmpty(0).Max();
        return max + 1;
    }
}
