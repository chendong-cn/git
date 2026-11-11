using System.Diagnostics;
using System.Text;
using System.Threading;

namespace NetConfigPro.Generators;

/// <summary>单条 Windows 网络诊断命令模板。</summary>
public sealed class DiagCmd
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public string Tip { get; init; } = "";

    /// <summary>命令模板：{T}=目标主机，{P}=端口。</summary>
    public string Tmpl { get; init; } = "";

    /// <summary>该命令必须填写目标主机。</summary>
    public bool NeedsTarget { get; init; }

    /// <summary>执行耗时较长（本工具执行时给更长超时）。</summary>
    public bool Slow { get; init; }

    /// <summary>交互式命令（无法自动退出）。</summary>
    public bool Interactive { get; init; }

    /// <summary>本工具执行时实际使用的命令（把持续/交互式换成有界变体）；null 表示只可复制不可执行。</summary>
    public string? RunTmpl { get; init; }
}

/// <summary>一组网络诊断命令。</summary>
public sealed class DiagGroup
{
    public string Name { get; init; } = "";
    public List<DiagCmd> Items { get; init; } = [];
}

/// <summary>网络命令模块：Windows 本机常用网络诊断命令目录与渲染。</summary>
public static class DiagCmds
{
    public static readonly List<DiagGroup> Groups =
    [
        new DiagGroup
        {
            Name = "连通性测试",
            Items =
            [
                new DiagCmd { Key = "ping4", Name = "ping 连通测试", NeedsTarget = true,
                    Tip = "向目标发送 4 个 ICMP 报文，检查连通性与往返延迟（最常用）",
                    Tmpl = "ping {T} -n 4" },
                new DiagCmd { Key = "pingt", Name = "ping 持续测试", NeedsTarget = true, Interactive = true,
                    RunTmpl = "ping {T} -n 4",
                    Tip = "不间断 ping（Ctrl+C 停止）；本工具内执行自动改为有限次数以免一直运行",
                    Tmpl = "ping {T} -t" },
                new DiagCmd { Key = "pingmtu", Name = "ping 大包（MTU 探测）", NeedsTarget = true,
                    Tip = "发送 1400 字节不分片大包；若提示“需要拆分数据包”说明路径 MTU 不足",
                    Tmpl = "ping {T} -f -l 1400" },
                new DiagCmd { Key = "tracert", Name = "tracert 路由追踪", NeedsTarget = true, Slow = true,
                    Tip = "显示到目标所经过的每一跳路由与延迟，定位断点在哪一跳（-d 不做反向域名解析，更快）",
                    Tmpl = "tracert -d {T}" },
                new DiagCmd { Key = "pathping", Name = "pathping 丢包分析", NeedsTarget = true, Slow = true,
                    Tip = "逐跳统计丢包与时延定位链路质量问题（耗时较长，本工具执行 60 秒未完成会终止）",
                    Tmpl = "pathping {T}" },
                new DiagCmd { Key = "tcpnc", Name = "TCP 端口连通测试", NeedsTarget = true,
                    Tip = "PowerShell 内置：测试到目标指定端口的 TCP 连通性（无需启用 telnet 客户端）",
                    Tmpl = "powershell -NoProfile -Command Test-NetConnection -ComputerName {T} -Port {P} -InformationLevel Detailed" },
                new DiagCmd { Key = "telnet", Name = "telnet 端口测试", NeedsTarget = true, Interactive = true,
                    Tip = "传统 telnet 端口探测（需在系统功能里启用 Telnet 客户端；交互式，本工具内跳过执行）",
                    Tmpl = "telnet {T} {P}" }
            ]
        },
        new DiagGroup
        {
            Name = "DNS 域名解析",
            Items =
            [
                new DiagCmd { Key = "nslk", Name = "nslookup 域名解析", NeedsTarget = true,
                    Tip = "用系统默认 DNS 解析域名，核对解析结果",
                    Tmpl = "nslookup {T}" },
                new DiagCmd { Key = "nslk2", Name = "nslookup 指定 DNS", NeedsTarget = true,
                    Tip = "用阿里公共 DNS 223.5.5.5 解析，用于对比本地 DNS 是否被污染/异常",
                    Tmpl = "nslookup {T} 223.5.5.5" },
                new DiagCmd { Key = "nslka", Name = "nslookup A 记录", NeedsTarget = true,
                    Tip = "查询域名 A 记录（IPv4 地址），type 可换 AAAA/NS/TXT/CNAME",
                    Tmpl = "nslookup -type=A {T}" },
                new DiagCmd { Key = "nslkmx", Name = "nslookup MX 记录", NeedsTarget = true,
                    Tip = "查询域名邮件交换记录（也可把 type 换成 A / NS / TXT）",
                    Tmpl = "nslookup -type=MX {T}" },
                new DiagCmd { Key = "nslkrev", Name = "nslookup 反向解析", NeedsTarget = true,
                    Tip = "反向解析：由 IP 查域名（PTR 记录），需目标网段配置了反向区",
                    Tmpl = "nslookup -type=PTR {T}" },
                new DiagCmd { Key = "dnsview", Name = "查看 DNS 缓存",
                    Tip = "列出本机 DNS 缓存条目，排查解析异常",
                    Tmpl = "ipconfig /displaydns" },
                new DiagCmd { Key = "dnsflush", Name = "刷新 DNS 缓存",
                    Tip = "清空本地 DNS 缓存（需管理员终端）；改过 DNS 不生效时先刷它",
                    Tmpl = "ipconfig /flushdns" }
            ]
        },
        new DiagGroup
        {
            Name = "本机 IP · ARP · MAC",
            Items =
            [
                new DiagCmd { Key = "ipcfg", Name = "ipconfig 简览",
                    Tip = "查看本机 IPv4 地址/子网/网关/DNS 配置",
                    Tmpl = "ipconfig" },
                new DiagCmd { Key = "ipall", Name = "ipconfig /all 全量",
                    Tip = "本机网络全量信息：各网卡 MAC、DHCP 状态、DNS 后缀等",
                    Tmpl = "ipconfig /all" },
                new DiagCmd { Key = "netshif", Name = "netsh 接口配置明细",
                    Tip = "各网卡的 IP/掩码/网关/DNS 的逐接口明细（比 ipconfig 更结构化）",
                    Tmpl = "netsh interface ip show config" },
                new DiagCmd { Key = "hostname", Name = "hostname 本机名",
                    Tip = "查看本机主机名（配合 net view 判断同网段主机）",
                    Tmpl = "hostname" },
                new DiagCmd { Key = "renew", Name = "DHCP 重新获取地址",
                    Tip = "强制向 DHCP 重新续租 IP（会短暂断网；先 ipconfig /release 可强制换地址，需管理员）",
                    Tmpl = "ipconfig /renew", Slow = true },
                new DiagCmd { Key = "arp", Name = "arp -a 邻居表",
                    Tip = "查看 ARP 缓存（IP↔MAC 映射），排查 IP 冲突、ARP 欺骗、网关丢包",
                    Tmpl = "arp -a" },
                new DiagCmd { Key = "arpdel", Name = "清空 ARP 缓存",
                    Tip = "删除全部 ARP 缓存条目（需管理员）；网络异常重绑定网关时使用",
                    Tmpl = "netsh interface ip delete arpcache" },
                new DiagCmd { Key = "mac", Name = "getmac 网卡 MAC",
                    Tip = "列出本机各网卡 MAC 地址",
                    Tmpl = "getmac /v /fo list" }
            ]
        },
        new DiagGroup
        {
            Name = "连接 · 端口 · 路由",
            Items =
            [
                new DiagCmd { Key = "netall", Name = "netstat 全部连接",
                    Tip = "列出所有 TCP/UDP 连接与占用进程 PID（配合任务管理器定位进程）",
                    Tmpl = "netstat -ano" },
                new DiagCmd { Key = "netls", Name = "netstat 监听端口",
                    Tip = "只看本机正在监听的端口（对外提供的服务一览）",
                    Tmpl = "netstat -ano | findstr LISTENING" },
                new DiagCmd { Key = "netp", Name = "netstat 指定端口",
                    Tip = "查指定端口被哪个 PID 占用（端口冲突排查）",
                    Tmpl = "netstat -ano | findstr :{P}" },
                new DiagCmd { Key = "netb", Name = "netstat 端口对程序（管理员）",
                    Tip = "直接显示占用端口的程序名（netstat -b 需管理员权限）",
                    Tmpl = "netstat -ano -b" },
                new DiagCmd { Key = "nets", Name = "netstat 协议统计",
                    Tip = "TCP/UDP/ICMP 各协议的收发与错误统计，判断丢包集中在哪层",
                    Tmpl = "netstat -s" },
                new DiagCmd { Key = "route", Name = "route print 路由表",
                    Tip = "查看完整路由表：默认网关与网段走向（多网卡/多网关必备）",
                    Tmpl = "route print" },
                new DiagCmd { Key = "neteth", Name = "netstat -e 收发统计",
                    Tip = "网卡收发报文/错误/丢弃计数，粗略判断链路质量",
                    Tmpl = "netstat -e" }
            ]
        },
        new DiagGroup
        {
            Name = "HTTP / 服务探测",
            Items =
            [
                new DiagCmd { Key = "httphead", Name = "curl HTTP 头探测", NeedsTarget = true,
                    Tip = "探测 Web 服务响应头（目标需带 http:// 或 https:// 前缀）",
                    Tmpl = "curl -I -m 10 {T}" },
                new DiagCmd { Key = "httpscert", Name = "curl HTTPS 证书握手", NeedsTarget = true, Slow = true,
                    Tip = "详细输出 TLS 握手与证书链（-k 忽略证书错误仅测链路）；目标需带 https:// 前缀",
                    Tmpl = "curl -vk -m 10 {T}" },
                new DiagCmd { Key = "tcpprobe", Name = "curl TCP 端口探测", NeedsTarget = true,
                    Tip = "用 curl 的 telnet 协议探测任意 TCP 端口连通（无需 telnet 客户端，5 秒超时）",
                    Tmpl = "curl -v -m 5 telnet://{T}:{P}" }
            ]
        },
        new DiagGroup
        {
            Name = "网络共享 / 局域网",
            Items =
            [
                new DiagCmd { Key = "netview", Name = "net view 工作组浏览",
                    Tip = "列出本机工作组/域中可见的主机（判断局域网连通与发现是否正常）",
                    Tmpl = "net view" },
                new DiagCmd { Key = "netviewt", Name = "net view 目标共享", NeedsTarget = true,
                    Tip = "列出目标主机共享的文件夹（即 SMB/445 可达性 + 权限探测；\\目标\\形式）",
                    Tmpl = "net view \\\\{T}" },
                new DiagCmd { Key = "netuse", Name = "net use 已映射",
                    Tip = "查看本机已映射的网络驱动器与连接状态",
                    Tmpl = "net use" },
                new DiagCmd { Key = "netshare", Name = "net share 本机共享",
                    Tip = "查看本机对外共享的文件夹（排查共享泄露）",
                    Tmpl = "net share" },
                new DiagCmd { Key = "netstatn", Name = "SMB 445 连接检查",
                    Tip = "看本机到 445 端口的 ESTABLISHED 会话（配合 net session 查谁连着我）",
                    Tmpl = "netstat -ano | findstr :445" }
            ]
        },
        new DiagGroup
        {
            Name = "无线网络",
            Items =
            [
                new DiagCmd { Key = "wlani", Name = "netsh 无线状态",
                    Tip = "当前 Wi-Fi 连接信息：SSID / 信号强度 / 速率 / 信道",
                    Tmpl = "netsh wlan show interfaces" },
                new DiagCmd { Key = "wlans", Name = "netsh 扫描 Wi-Fi", Slow = true,
                    Tip = "扫描周边无线网络（含 BSSID / 信道 / 信号，约数秒）",
                    Tmpl = "netsh wlan show networks mode=bssid" }
            ]
        }
    ];

    /// <summary>把模板中的 {T}/{P} 占位符替换为目标与端口。</summary>
    public static string Fill(DiagCmd c, string? target, string? port)
    {
        var t = (target ?? "").Trim();
        var p = (port ?? "").Trim();
        if (p.Length == 0) p = "80";
        return c.Tmpl
            .Replace("{T}", t.Length == 0 ? "目标主机" : t)
            .Replace("{P}", p);
    }

    /// <summary>交互式且无执行变体的命令只可复制、不可在本工具内执行。</summary>
    public static bool CanRun(DiagCmd c) => !c.Interactive || c.RunTmpl != null;

    /// <summary>本工具执行时实际使用的命令（交互式命令走有界变体）。</summary>
    public static string ExecCommand(DiagCmd c, string? target, string? port)
    {
        var t = (target ?? "").Trim();
        var p = (port ?? "").Trim();
        if (p.Length == 0) p = "80";
        var line = (c.RunTmpl ?? c.Tmpl)
            .Replace("{T}", t.Length == 0 ? "目标主机" : t)
            .Replace("{P}", p);
        return line;
    }

    /// <summary>生成带中文注释的预览文本（供阅读；复制/导出请用 <see cref="Plain"/>）。</summary>
    public static string Render(IEnumerable<DiagCmd> picked, string? target, string? port)
    {
        var sb = new StringBuilder();
        var bar = new string('#', 62);
        var targetText = (target ?? "").Trim();
        sb.AppendLine(bar);
        sb.AppendLine("# NetConfigPro 网络诊断命令 · 运行环境：Windows 本机（cmd / PowerShell）");
        sb.AppendLine("# 目标主机：" + (targetText.Length == 0 ? "（未填——依赖目标的命令未列出，请先在左栏填写）" : targetText));
        sb.AppendLine("# 用法：复制纯命令粘贴到命令行执行；标注“需管理员”的命令请在管理员终端中运行");
        sb.AppendLine(bar);

        var pickedList = picked.ToList();
        var hasTarget = targetText.Length > 0;
        foreach (var g in Groups)
        {
            // 目标为空时跳过依赖目标的命令（防御：无论调用方是否已过滤）
            var chosen = g.Items
                .Where(i => pickedList.Contains(i) && (!i.NeedsTarget || hasTarget))
                .ToList();
            if (chosen.Count == 0) continue;
            sb.AppendLine();
            sb.AppendLine($"# —— {g.Name} ——");
            foreach (var c in chosen)
            {
                sb.AppendLine($"# [{c.Name}] {c.Tip}");
                sb.AppendLine(Fill(c, target, port));
            }
        }
        return sb.ToString();
    }

    /// <summary>剥离 # 注释与空行，得到可直接粘贴执行的纯命令。</summary>
    public static string Plain(string annotated)
    {
        var sb = new StringBuilder();
        foreach (var line in annotated.Split('\n'))
        {
            var s = line.TrimEnd('\r');
            if (s.TrimStart().StartsWith('#')) continue;
            if (s.Trim().Length == 0) continue;
            sb.AppendLine(s);
        }
        return sb.ToString();
    }

    /// <summary>cmd 子进程输出编码（中文 Windows 为 GBK/936）。CodePages 程序集在共享框架内，无需额外包。</summary>
    private static readonly Encoding ConsoleEnc = CreateConsoleEncoding();

    private static Encoding CreateConsoleEncoding()
    {
        try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); } catch { /* 忽略 */ }
        try { return Encoding.GetEncoding(936); } catch { return Encoding.UTF8; }
    }

    /// <summary>在 Windows 命令解释器中执行单条命令并捕获输出；超过 timeoutMs 或取消请求时自动杀进程返回。</summary>
    public static string Execute(string cmdLine, int timeoutMs, CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/d /c \"" + cmdLine + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = ConsoleEnc,
                StandardErrorEncoding = ConsoleEnc
            };
            using var p = new Process { StartInfo = psi };
            var sb = new StringBuilder(4096);
            p.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) lock (sb) sb.AppendLine(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) lock (sb) sb.AppendLine(e.Data); };
            if (!p.Start()) return "（进程启动失败）";
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            var deadline = Environment.TickCount64 + timeoutMs;
            var reason = "";
            while (!p.WaitForExit(200))
            {
                if (ct.IsCancellationRequested) { reason = "（已停止）"; break; }
                if (Environment.TickCount64 >= deadline) { reason = $"（执行超过 {timeoutMs / 1000} 秒未结束已自动终止——请在终端中单独运行）"; break; }
            }
            if (reason.Length > 0)
            {
                try { p.Kill(true); } catch { /* 进程可能已退出 */ }
                p.WaitForExit(3000);
                lock (sb) return sb.ToString().TrimEnd('\r', '\n') + "\n" + reason;
            }
            lock (sb) return sb.ToString().TrimEnd('\r', '\n');
        }
        catch (Exception ex)
        {
            return "（执行异常：" + ex.Message + "）";
        }
    }
}
