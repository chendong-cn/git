using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NetConfigPro.Models;
using NetConfigPro.Services;

namespace NetConfigPro.Views;

public partial class ScanView : UserControl
{
    private readonly Action<string> _setStatus;
    private CancellationTokenSource? _cts;
    private bool _scanning;
    private readonly List<ArpEntry> _result = new();

    public ScanView(Store _, Action<string> setStatus)
    {
        InitializeComponent();
        _setStatus = setStatus;
        TxtCidr.Text = DetectLocalSubnet();

        // [诊断用] NCP_SCAN_RUN=1 + NCP_SCAN_TARGET=… 自动扫描（UI 冒烟）
        if (Environment.GetEnvironmentVariable("NCP_SCAN_RUN") == "1")
        {
            var t = Environment.GetEnvironmentVariable("NCP_SCAN_TARGET");
            if (!string.IsNullOrWhiteSpace(t)) TxtCidr.Text = t;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                () => BtnScan_Click(null!, new RoutedEventArgs()));
        }
    }

    /// <summary>自动带出本机第一块活动网卡所在网段（形如 192.168.1.5/24）。</summary>
    private static string DetectLocalSubnet()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                    var ip = ua.Address.ToString();
                    var mask = ua.IPv4Mask?.ToString();
                    if (string.IsNullOrEmpty(mask) || mask == "255.255.255.255") continue;
                    if (ip.StartsWith("169.254")) continue;
                    var bits = mask.Split('.').Sum(s => Convert.ToString(int.Parse(s), 2).Count(c => c == '1'));
                    if (bits == 0) continue;
                    return $"{ip}/{bits}";
                }
            }
        }
        catch { /* 检测失败走默认值 */ }
        return "192.168.1.0/24";
    }

    private async void BtnScan_Click(object sender, RoutedEventArgs e)
    {
        if (_scanning) return;
        if (!IpMacScanner.TryExpandTarget(TxtCidr.Text, out var ips, out var error))
        {
            _setStatus(error);
            MessageBox.Show(error, "扫描范围无效", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _scanning = true;
        _cts = new CancellationTokenSource();
        BtnScan.IsEnabled = false;
        BtnStop.IsEnabled = true;
        GridHosts.ItemsSource = null;
        _result.Clear();
        TxtStat.Text = $"扫描 {ips.Count} 个地址…（请稍候，约 2~6 秒）";
        _setStatus($"开始扫描 {ips.Count} 个地址，请稍候…");

        try
        {
            var progress = new Progress<int>(p => TxtProgress.Text = $"探测进度 {p}%（{ips.Count} 个地址）");
            await IpMacScanner.ProbeAsync(ips, progress, _cts.Token);

            // 探测完成后读 ARP 表，只保留本次扫描范围内的条目
            var (locals, all) = IpMacScanner.ParseArpText(IpMacScanner.RunArp());
            var targetSet = ips.ToHashSet();
            var found = all.Where(x => targetSet.Contains(x.Ip)).ToList();
            // 本机通常不在自身 ARP 表 → 用网卡信息补齐“本机行”（已在表内则不重复）
            foreach (var self in IpMacScanner.LocalSelf(targetSet).Where(s => !found.Any(x => x.Ip == s.Ip)))
                found.Add(self);
            found.Sort((a, b) => string.CompareOrdinal(a.Ip, b.Ip));
            for (var i = 0; i < found.Count; i++) found[i].Ordinal = i + 1;
            IpMacScanner.Annotate(found);
            _result.AddRange(found);
            GridHosts.ItemsSource = _result;

            var localCount = found.Count(x => x.IsLocal);
            var vendorCount = found.Count(x => x.Vendor.Length > 0);
            TxtStat.Text = found.Count == 0
                ? $"扫描完成：{ips.Count} 个地址中未发现在线主机（离线主机不会出现在 ARP 表）"
                : $"扫描完成：发现 {found.Count} 台在线主机（本机 {localCount}，识别厂商 {vendorCount}/{found.Count}）";
            _setStatus($"扫描完成：{found.Count} 台在线");
        }
        catch (OperationCanceledException)
        {
            TxtStat.Text = "扫描已停止";
            _setStatus("扫描已停止");
        }
        catch (Exception ex)
        {
            TxtStat.Text = "扫描异常：" + ex.Message;
            _setStatus("扫描异常：" + ex.Message);
        }
        finally
        {
            _scanning = false;
            _cts?.Dispose();
            _cts = null;
            BtnScan.IsEnabled = true;
            BtnStop.IsEnabled = false;
            TxtProgress.Text = "";
        }
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        try { _cts?.Cancel(); } catch { /* 忽略 */ }
        BtnStop.IsEnabled = false;
        TxtProgress.Text = "正在停止…";
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_result.Count == 0) { _setStatus("没有可复制的数据——请先扫描"); return; }
        var sb = new StringBuilder();
        sb.AppendLine("IP\tMAC\t厂商\t类型\t备注");
        foreach (var r in _result)
            sb.AppendLine($"{r.Ip}\t{r.Mac}\t{r.Vendor}\t{r.Type}\t{r.Note}");
        try
        {
            Clipboard.SetText(sb.ToString());
            _setStatus($"已复制 {_result.Count} 台主机列表（制表符分隔，可直接粘贴到 Excel）");
        }
        catch (Exception ex) { _setStatus("复制失败：" + ex.Message); }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (_result.Count == 0) { _setStatus("没有可导出的数据——请先扫描"); return; }
        var dlg = new SaveFileDialog
        {
            Title = "导出 IP-MAC 扫描结果",
            FileName = $"ipmac_scan_{DateTime.Now:yyyyMMdd_HHmm}.csv",
            Filter = "CSV 文件 (*.csv)|*.csv"
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("IP地址,MAC地址,厂商,类型,备注");
        foreach (var r in _result)
            sb.AppendLine($"{r.Ip},{r.Mac},{r.Vendor},{r.Type},{r.Note}");
        File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true)); // BOM：Excel 直开不乱码
        _setStatus($"已导出 {_result.Count} 台主机到 {dlg.FileName}");
    }
}
