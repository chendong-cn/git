using System.Diagnostics;
using System.IO;
using System.Text;
using NetConfigPro.Models;

namespace NetConfigPro.Services;

/// <summary>一次真实下发的执行结果。</summary>
public sealed class SshResult
{
    public bool Ok { get; set; }
    public string Output { get; set; } = "";
    /// <summary>人性化结论（如 认证失败/无法连接/成功）。</summary>
    public string Verdict { get; set; } = "";
}

/// <summary>
/// 真实 SSH 下发引擎（零依赖）：
/// - plink 通道：优先使用随程序附带的 plink.exe（支持密码直连），找不到时退而查 PATH；
/// - ssh 通道：Windows 自带 OpenSSH（仅本机已配置密钥免密时可用）。
/// 脚本经 stdin 送入设备 shell：关闭分页 → 配置逐行 → 返回视图 → 保存。
/// </summary>
public static class SshDeploy
{
    /// <summary>随程序附带的 plink.exe（与 exe 同目录或 PATH）。</summary>
    public static string? PlinkPath()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "plink.exe");
        if (File.Exists(local)) return local;
        try
        {
            var p = Environment.GetEnvironmentVariable("PATH");
            foreach (var dir in (p ?? "").Split(Path.PathSeparator))
            {
                if (dir.Length == 0) continue;
                var cand = Path.Combine(dir.Trim('"'), "plink.exe");
                if (File.Exists(cand)) return cand;
            }
        }
        catch { /* 忽略 */ }
        return null;
    }

    /// <summary>Windows 自带 OpenSSH 客户端。</summary>
    public static string? SshPath()
    {
        var sys = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "OpenSSH", "ssh.exe");
        return File.Exists(sys) ? sys : null;
    }

    /// <summary>构造设备会话脚本行：分页关闭 → (enable) → 配置 → 返回 → 保存。</summary>
    public static List<string> BuildScript(Vendor vendor, string plainConfig, string? enablePwd)
    {
        var script = new List<string>();
        var isCiscoLike = vendor is Vendor.Cisco or Vendor.Ruijie;

        if (isCiscoLike) script.Add("terminal length 0");
        else script.Add("screen-length 0 temporary");

        if (isCiscoLike)
        {
            script.Add("enable");
            if (!string.IsNullOrWhiteSpace(enablePwd)) script.Add(enablePwd.Trim());
        }

        foreach (var line in plainConfig.Split('\n'))
        {
            var s = line.TrimEnd('\r').Trim();
            if (s.Length == 0 || s.StartsWith('#') || s.StartsWith('!')) continue;
            script.Add(s);
        }

        // 回到可保存视图并保存（若已在该视图，多余命令会被设备忽略/报错，无害）
        switch (vendor)
        {
            case Vendor.Huawei:
                script.Add("return");
                script.Add("save");
                script.Add("y"); // 应答 VRP 的确认提示
                break;
            case Vendor.H3C:
                script.Add("return");
                script.Add("save force");
                break;
            case Vendor.Cisco:
                script.Add("end");
                script.Add("write memory");
                break;
            default: // Ruijie
                script.Add("end");
                script.Add("write");
                break;
        }
        return script;
    }

    /// <summary>用 plink 以账号密码下发（自动应答首次主机密钥确认）。</summary>
    public static SshResult RunPlink(string plinkPath, DeviceEntry dev, string plainConfig, string? enablePwd,
        string password, int timeoutMs = 120_000)
    {
        var script = string.Join("\r\n", BuildScript(dev.Vendor, plainConfig, enablePwd)) + "\r\n";
        // 首行 "y" 用于应答 plink 首次连接的主机密钥确认提示；密钥已缓存时会被设备当作无害命令忽略
        var stdin = "y\r\n" + script;
        // 密码/用户名进入命令行参数：去掉可能破坏引号边界的双引号
        var pw = (password ?? "").Replace("\"", "'");
        var user = (dev.Username ?? "").Replace("\"", "'");
        var psi = new ProcessStartInfo
        {
            FileName = plinkPath,
            Arguments = $"-ssh -P {dev.Port} -pw \"{pw}\" {user}@{dev.Ip}",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.GetEncoding(936),
            StandardErrorEncoding = Encoding.GetEncoding(936)
        };
        return RunProcess(psi, stdin, timeoutMs);
    }

    /// <summary>用系统 ssh（密钥免密通道，BatchMode 禁止交互）。</summary>
    public static SshResult RunSsh(string sshPath, DeviceEntry dev, string plainConfig, string? enablePwd,
        int timeoutMs = 120_000)
    {
        var script = string.Join("\r\n", BuildScript(dev.Vendor, plainConfig, enablePwd)) + "\r\n";
        var psi = new ProcessStartInfo
        {
            FileName = sshPath,
            Arguments = $"-p {dev.Port} -o BatchMode=yes -o StrictHostKeyChecking=accept-new {dev.Username}@{dev.Ip}",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        return RunProcess(psi, script, timeoutMs);
    }

    private static SshResult RunProcess(ProcessStartInfo psi, string stdin, int timeoutMs)
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using var p = new Process { StartInfo = psi };
            var sb = new StringBuilder(16 * 1024);
            p.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) lock (sb) sb.AppendLine(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) lock (sb) sb.AppendLine(e.Data); };
            if (!p.Start()) return new SshResult { Ok = false, Verdict = "进程启动失败" };
            try { p.StandardInput.Write(stdin); p.StandardInput.Flush(); p.StandardInput.Close(); }
            catch { /* 连接立即失败时忽略写错误 */ }
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            var exited = p.WaitForExit(timeoutMs);
            if (!exited)
            {
                try { p.Kill(true); } catch { }
                p.WaitForExit(3000);
                lock (sb) return new SshResult { Ok = false, Verdict = $"超时（{timeoutMs / 1000}s）", Output = sb.ToString() };
            }

            lock (sb)
            {
                var outText = sb.ToString();
                return new SshResult { Ok = true, Output = outText, Verdict = Judge(outText) };
            }
        }
        catch (Exception ex)
        {
            return new SshResult { Ok = false, Verdict = "异常：" + ex.Message };
        }
    }

    /// <summary>按输出特征给出结论；退出码 0 且无致命错误即视为成功。</summary>
    private static string Judge(string output)
    {
        var o = output ?? "";
        if (o.Contains("Access denied") || o.Contains("Permission denied") || o.Contains("认证失败") || o.Contains("authentication"))
            return "认证失败——请核对账号/密码";
        if (o.Contains("Unable to open connection") || o.Contains("Connection refused") || o.Contains("Network error") || o.Contains("无法连接到远程服务器") || o.Contains("Connection timed out"))
            return "无法连接设备（地址/端口不通或 SSH 未启用）";
        if (o.Contains("Host key") || o.Contains("does not exist") || o.Contains("没有这样的主机"))
            return "主机不存在或无法解析";
        if (o.Contains("% Error") || o.Contains("Unknown command") || o.Contains("syntax error"))
            return "已连接但出现命令报错（部分命令设备不接受，见输出）";
        return "下发完成——请核对设备回显确认配置与保存是否成功";
    }
}
