using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NetConfigPro.Generators;
using NetConfigPro.Models;
using NetConfigPro.Services;

namespace NetConfigPro.Views;

public partial class DeployView : UserControl, IRefreshable
{
    private readonly Store _store;
    private readonly Action<string> _setStatus;
    private readonly ObservableCollection<DeploymentLog> _logs = new();

    public DeployView(Store store, Action<string> setStatus)
    {
        InitializeComponent();
        _store = store;
        _setStatus = setStatus;

        GridDevices.ItemsSource = _store.Devices;
        GridLogs.ItemsSource = _logs;
        Refresh();
    }

    public void Refresh()
    {
        CmbVersion.ItemsSource = null;
        CmbVersion.ItemsSource = _store.Versions;
        UpdateSelectedCount();

        // 通道状态提示：附带 plink（密码直连）优先，其次系统 ssh（需密钥）
        if (TxtChannel == null) return;
        var plink = SshDeploy.PlinkPath();
        var ssh = SshDeploy.SshPath();
        TxtChannel.Text = plink is not null
            ? "通道：plink（账号密码直连）"
            : ssh is not null
                ? "通道：系统 ssh（需本机已配密钥免密）"
                : "⚠ 未找到 plink / ssh，真实下发不可用";
    }

    private void UpdateSelectedCount() =>
        TxtSelected.Text = $"已选 {_store.Devices.Count(d => d.IsSelected)} 台设备 / 共 {_store.Devices.Count} 台";

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var d in _store.Devices) d.IsSelected = true;
        GridDevices.Items.Refresh();
        UpdateSelectedCount();
    }

    private void BtnInvert_Click(object sender, RoutedEventArgs e)
    {
        foreach (var d in _store.Devices) d.IsSelected = !d.IsSelected;
        GridDevices.Items.Refresh();
        UpdateSelectedCount();
    }

    private void BtnClearSel_Click(object sender, RoutedEventArgs e)
    {
        foreach (var d in _store.Devices) d.IsSelected = false;
        GridDevices.Items.Refresh();
        UpdateSelectedCount();
    }

    private void CmbVersion_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbVersion.SelectedItem is not ConfigVersion ver) return;
        TxtConfig.Text = ver.Content;
        _setStatus($"已载入 {ver.DeviceName} v{ver.VersionNo} 的配置");
    }

    private void BtnLoadFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "配置文件 (*.cfg;*.txt)|*.cfg;*.txt|所有文件 (*.*)|*.*" };
        if (dlg.ShowDialog() != true) return;

        TxtConfig.Text = File.ReadAllText(dlg.FileName, Encoding.UTF8);
        _setStatus($"已载入文件：{Path.GetFileName(dlg.FileName)}");
    }

    private void BtnClearConfig_Click(object sender, RoutedEventArgs e)
    {
        TxtConfig.Clear();
        CmbVersion.SelectedIndex = -1;
    }

    private void TxtConfig_TextChanged(object sender, TextChangedEventArgs e) =>
        TxtConfigStat.Text = $"{TxtConfig.LineCount} 行 · {TxtConfig.Text.Length} 字符";

    private async void BtnExecute_Click(object sender, RoutedEventArgs e)
    {
        var targets = _store.Devices.Where(d => d.IsSelected).ToList();
        if (targets.Count == 0)
        {
            _setStatus("请先勾选目标设备");
            MessageBox.Show("请在左侧勾选至少一台目标设备。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (TxtConfig.Text.Trim().Length == 0)
        {
            _setStatus("请先载入配置内容");
            MessageBox.Show("请载入或粘贴要下发的配置内容。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var config = TxtConfig.Text;
        var detected = VendorDetect.Detect(config);
        var lineCount = config.Split('\n').Length;

        var task = new DeploymentTask
        {
            Name = $"部署任务 {DateTime.Now:MM-dd HH:mm}",
            ConfigContent = config,
            Status = "执行中"
        };

        var btn = (Button)sender;
        btn.IsEnabled = false;
        Progress.Visibility = Visibility.Visible;
        _logs.Clear();

        AddLog("系统", "-", "开始", $"共 {targets.Count} 台设备，配置 {lineCount} 行" +
            (detected is null ? "（未识别到厂商特征）" : $"（识别为 {EnumText.VendorShort(detected.Value)} 语法）"));

        try
        {
            // 后台只做耗时模拟与纯数据计算，返回结果列表后在 UI 线程统一写日志，
            // 避免跨线程访问 ObservableCollection。
            var results = await Task.Run(async () =>
            {
                var list = new List<DeploymentLog>();
                foreach (var d in targets)
                {
                    await Task.Delay(320);
                    var mismatch = detected is not null && detected.Value != d.Vendor;
                    list.Add(new DeploymentLog
                    {
                        DeviceName = d.Name,
                        DeviceIp = d.Ip,
                        Status = mismatch ? "警告" : "模拟成功",
                        Message = mismatch
                            ? $"配置语法（{EnumText.VendorShort(detected!.Value)}）与设备厂商（{d.VendorText}）不一致，请确认后再执行"
                            : $"已校验 {lineCount} 行配置，未发现厂商语法冲突（模拟下发，未真实连接设备）"
                    });
                }
                return list;
            });

            foreach (var log in results)
            {
                _logs.Add(log);
                task.Logs.Add(log);
            }

            AddLog("系统", "-", "完成", "模拟执行结束。本工具未内置 SSH 下发通道，请复制配置到终端工具手工刷入，或接入自动化通道后批量执行。");
            task.Status = "已完成";
            SaveTask(task);
            _setStatus($"部署任务已完成：{targets.Count} 台设备");
        }
        catch (Exception ex)
        {
            AddLog("系统", "-", "失败", "执行过程异常：" + ex.Message);
            task.Status = "失败";
            task.Logs.Add(new DeploymentLog { DeviceName = "系统", DeviceIp = "-", Status = "失败", Message = ex.Message });
            SaveTask(task);
            _setStatus("部署任务执行异常：" + ex.Message);
        }
        finally
        {
            btn.IsEnabled = true;
            Progress.Visibility = Visibility.Collapsed;
        }
    }

    // ==================== 真实 SSH 下发 ====================
    private async void BtnRealDeploy_Click(object sender, RoutedEventArgs e)
    {
        var targets = _store.Devices.Where(d => d.IsSelected).ToList();
        if (targets.Count == 0)
        {
            _setStatus("请先勾选目标设备");
            MessageBox.Show("请在左侧勾选至少一台目标设备。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (TxtConfig.Text.Trim().Length == 0)
        {
            _setStatus("请先载入配置内容");
            MessageBox.Show("请载入或粘贴要下发的配置内容。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var plink = SshDeploy.PlinkPath();
        var ssh = SshDeploy.SshPath();
        if (plink is null && ssh is null)
        {
            MessageBox.Show("未找到可用的 SSH 客户端。\n\n• plink（密码直连）：随程序发布的 plink.exe 缺失，请从 PuTTY 官网下载后放到本程序同目录\n• ssh（密钥免密）：Windows 10/11 可在“可选功能”中安装 OpenSSH 客户端",
                "无法真实下发", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var usePlink = plink is not null;

        var config = TxtConfig.Text;
        var detected = VendorDetect.Detect(config);
        if (detected is { } dv)
        {
            var mismatch = targets.Where(t => t.Vendor != dv).ToList();
            if (mismatch.Count > 0)
            {
                var ask = MessageBox.Show(
                    $"配置语法识别为「{EnumText.VendorName(dv)}」，但以下设备厂商不同：\n" +
                    string.Join("\n", mismatch.Select(m => $"  · {m.Name}（{m.VendorText}）")) +
                    "\n\n继续下发可能导致设备报错，是否仍要执行？",
                    "厂商不一致提醒", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (ask != MessageBoxResult.Yes) { _setStatus("已取消——请核对配置与目标设备厂商"); return; }
            }
        }

        var enablePwd = TxtEnablePwd.Password;
        // 思科/锐捷设备需要 enable 密码进入特权模式：缺省时可能卡在密码等待，给提示并缩短超时
        var needEnable = targets.Any(t => t.Vendor is Vendor.Cisco or Vendor.Ruijie);
        var shortTimeout = false;
        if (needEnable && enablePwd.Length == 0)
        {
            var ask = MessageBox.Show(
                "目标中包含思科/锐捷设备，但未填写 enable 密码。\n\n" +
                "若设备登录后处于用户模式且需要 enable 密码，命令将等待输入直到 25 秒超时；" +
                "若设备 SSH 直入特权模式则无需填写。\n\n是否仍要继续？",
                "enable 密码未填", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ask != MessageBoxResult.Yes) { _setStatus("已取消——请填写 enable 密码后重试"); return; }
            shortTimeout = true;
        }
        var timeoutMs = shortTimeout ? 25_000 : 120_000;
        var task = new DeploymentTask
        {
            Name = $"真实下发 {DateTime.Now:MM-dd HH:mm}",
            ConfigContent = config,
            Status = "执行中"
        };
        var btn = (Button)sender;
        btn.IsEnabled = false;
        Progress.Visibility = Visibility.Visible;
        _logs.Clear();
        AddLog("系统", "-", "开始", $"共 {targets.Count} 台设备，通道={usePlink}，账号取自设备清单");

        try
        {
            var results = await Task.Run(() =>
            {
                var list = new List<DeploymentLog>();
                foreach (var d in targets)
                {
                    if (usePlink)
                    {
                        var pwd = string.IsNullOrWhiteSpace(d.EncryptedPassword)
                            ? "" : AppStorage.Unprotect(d.EncryptedPassword);
                        if (pwd.Length == 0)
                        {
                            list.Add(new DeploymentLog { DeviceName = d.Name, DeviceIp = d.Ip, Status = "失败",
                                Message = "未保存 SSH 密码——请在「设备管理」中为该设备补录密码" });
                            continue;
                        }
                        var r = SshDeploy.RunPlink(plink!, d, config, enablePwd, pwd, timeoutMs);
                        list.Add(MakeLog(d, r));
                    }
                    else
                    {
                        var r = SshDeploy.RunSsh(ssh!, d, config, enablePwd, timeoutMs);
                        list.Add(MakeLog(d, r));
                    }
                }
                return list;
            });

            foreach (var log in results)
            {
                _logs.Add(log);
                task.Logs.Add(log);
            }
            var failed = results.Count(r => r.Status != "成功");
            AddLog("系统", "-", "完成", failed == 0
                ? "全部下发完成。请到设备上复核 running-config 与保存结果。"
                : $"{results.Count - failed} 台成功 / {failed} 台失败，见上表明细。");
            task.Status = failed == 0 ? "已完成" : "部分失败";
            SaveTask(task);
            _setStatus(failed == 0 ? $"真实下发完成：{results.Count} 台设备" : $"真实下发完成：{failed} 台失败");
        }
        catch (Exception ex)
        {
            AddLog("系统", "-", "失败", "执行过程异常：" + ex.Message);
            task.Status = "失败";
            task.Logs.Add(new DeploymentLog { DeviceName = "系统", DeviceIp = "-", Status = "失败", Message = ex.Message });
            SaveTask(task);
            _setStatus("真实下发异常：" + ex.Message);
        }
        finally
        {
            btn.IsEnabled = true;
            Progress.Visibility = Visibility.Collapsed;
            TxtEnablePwd.Clear();
        }
    }

    private static DeploymentLog MakeLog(DeviceEntry d, SshResult r)
    {
        var ok = !r.Verdict.Contains("认证失败") && !r.Verdict.Contains("无法连接")
                 && !r.Verdict.Contains("超时") && !r.Verdict.Contains("不存在") && !r.Verdict.Contains("异常")
                 && !r.Verdict.Contains("进程启动失败");
        var output = (r.Output ?? "").Trim();
        if (output.Length > 600) output = "…" + output[^600..];
        return new DeploymentLog
        {
            DeviceName = d.Name,
            DeviceIp = d.Ip,
            Status = ok ? "成功" : "失败",
            Message = r.Verdict + (output.Length == 0 ? "" : "\n" + output)
        };
    }

    private void AddLog(string name, string ip, string status, string message) =>
        _logs.Add(new DeploymentLog { DeviceName = name, DeviceIp = ip, Status = status, Message = message });

    /// <summary>保存部署任务历史（最多保留 200 条，防止数据目录无限膨胀）。</summary>
    private void SaveTask(DeploymentTask task)
    {
        _store.Tasks.Insert(0, task);
        while (_store.Tasks.Count > 200) _store.Tasks.RemoveAt(_store.Tasks.Count - 1);
        _store.SaveTasks();
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e) => _logs.Clear();

    private void BtnExportLog_Click(object sender, RoutedEventArgs e)
    {
        if (_logs.Count == 0)
        {
            _setStatus("暂无日志可导出");
            return;
        }

        var dlg = new SaveFileDialog
        {
            FileName = $"deploy_log_{DateTime.Now:yyyyMMdd_HHmm}.csv",
            DefaultExt = ".csv",
            Filter = "CSV 文件 (*.csv)|*.csv"
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder("时间,设备,管理IP,状态,说明\n");
        foreach (var l in _logs)
            sb.AppendLine(string.Join(',',
                TextUtil.EscapeCsv(l.Time.ToString("HH:mm:ss")), TextUtil.EscapeCsv(l.DeviceName),
                TextUtil.EscapeCsv(l.DeviceIp), TextUtil.EscapeCsv(l.Status), TextUtil.EscapeCsv(l.Message)));

        // 带 BOM 的 UTF-8：Excel 直接打开中文不乱码
        File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
        _setStatus($"日志已导出：{dlg.FileName}");
    }
}
