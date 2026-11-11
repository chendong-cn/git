using NetConfigPro.Models;

namespace NetConfigPro.Generators;

/// <summary>
/// 一键巡检：按厂商 × 设备类型输出「特权模式只读诊断命令集」。
/// 与配置生成（进 system-view/configure terminal）不同，巡检命令全部在特权视图直接执行，
/// 用于快速判断设备健康状态（版本/CPU/内存/硬件、接口链路、转发表、安全会话、日志与配置）。
/// </summary>
public static class Inspector
{
    public sealed record InsItem(string Key, string Name, string Tip, DeviceType[]? Scope);

    public sealed record InsGroup(string Name, InsItem[] Items);

    private static readonly DeviceType[] L3 = [DeviceType.Router, DeviceType.Firewall, DeviceType.Switch];
    private static readonly DeviceType[] SW = [DeviceType.Switch];
    private static readonly DeviceType[] FW = [DeviceType.Firewall];

    /// <summary>巡检项目目录（Scope=null 表示全部设备适用）。</summary>
    public static readonly InsGroup[] Groups =
    [
        new("一、系统健康",
        [
            new("ver", "设备版本与运行时长", "型号、系统版本、补丁与 uptime（判断是否异常重启）", null),
            new("cpu", "CPU 利用率", "CPU 占用与峰值，排查高负载", L3),
            new("mem", "内存利用率", "内存占用比例，超过 80% 需关注", L3),
            new("device", "单板/电源/风扇/温度", "硬件模块与温度告警（命令随型号有差异）", L3),
            new("optics", "光模块收发功率", "光衰、收发功率告警（接口名为示例需替换）", L3),
        ]),
        new("二、接口与链路",
        [
            new("ifb", "接口状态摘要", "所有接口 Up/Down、速率与 IP", null),
            new("ifd", "接口详情与错误计数", "CRC 错误、丢包、流量计数", null),
            new("lldp", "LLDP 邻居发现", "链路邻居与线缆连接核对", [DeviceType.Router, DeviceType.Switch]),
        ]),
        new("三、转发表项",
        [
            new("route", "路由表", "静态/动态路由是否完整", L3),
            new("arp", "ARP 表", "IP-MAC 对应与欺骗排查", L3),
            new("vlan", "VLAN 表", "VLAN 与端口归属核对", SW),
            new("mac", "MAC 地址表", "终端 MAC 学习情况（攻击排查）", SW),
            new("stp", "生成树状态", "端口角色与阻塞状态", SW),
        ]),
        new("四、安全与会话",
        [
            new("acl", "ACL 命中计数", "规则命中统计，验证策略是否生效", L3),
            new("sess", "并发会话表", "防火墙活动连接数与来源统计", FW),
            new("users", "在线用户", "当前登录会话（判断是否有非授权接入）", null),
        ]),
        new("五、时间/日志/配置",
        [
            new("clock", "系统时间与 NTP", "时间偏差会影响日志与证书判断", null),
            new("log", "日志缓冲", "近期告警与事件（stp 震荡、接口抖动）", L3),
            new("run", "运行配置回显", "巡检留档与配置核对/备份", null),
            new("diag", "故障信息采集(输出大)", "一键汇总全部诊断信息，供故障报修", L3),
        ]),
    ];

    /// <summary>某巡检项是否适用于指定设备类型。</summary>
    public static bool AppliesTo(InsItem item, DeviceType dt) => item.Scope is null || item.Scope.Contains(dt);

    // ------------------------------------------------------------------
    // 命令表：key → 命令行（{IF} 占位将被替换为例接口名；支持多行）
    // ------------------------------------------------------------------
    private static readonly Dictionary<string, string[]> Huawei = new()
    {
        ["ver"] = ["display version"],
        ["cpu"] = ["display cpu-usage"],
        ["mem"] = ["display memory-usage"],
        ["device"] = ["display device"],
        ["optics"] = ["display transceiver interface {IF} verbose"],
        ["ifb"] = ["display interface brief"],
        ["ifd"] = ["display interface"],
        ["lldp"] = ["display lldp neighbor brief"],
        ["route"] = ["display ip routing-table"],
        ["arp"] = ["display arp"],
        ["vlan"] = ["display vlan"],
        ["mac"] = ["display mac-address"],
        ["stp"] = ["display stp brief"],
        ["acl"] = ["display acl all"],
        ["sess"] = ["display firewall session table"],
        ["users"] = ["display users"],
        ["clock"] = ["display clock", "display ntp-service status"],
        ["log"] = ["display logbuffer"],
        ["run"] = ["display current-configuration"],
        ["diag"] = ["display diagnostic-information"],
    };

    private static readonly Dictionary<string, string[]> H3C = new()
    {
        ["ver"] = ["display version"],
        ["cpu"] = ["display cpu-usage"],
        ["mem"] = ["display memory"],
        ["device"] = ["display device"],
        ["optics"] = ["display transceiver interface {IF} verbose"],
        ["ifb"] = ["display interface brief"],
        ["ifd"] = ["display interface"],
        ["lldp"] = ["display lldp neighbor brief"],
        ["route"] = ["display ip routing-table"],
        ["arp"] = ["display arp"],
        ["vlan"] = ["display vlan"],
        ["mac"] = ["display mac-address"],
        ["stp"] = ["display stp brief"],
        ["acl"] = ["display acl all"],
        ["sess"] = ["display session table ipv4"],
        ["users"] = ["display users"],
        ["clock"] = ["display clock", "display ntp-service status"],
        ["log"] = ["display logbuffer"],
        ["run"] = ["display current-configuration"],
        ["diag"] = ["display diagnostic-information"],
    };

    private static readonly Dictionary<string, string[]> CiscoIos = new()
    {
        ["ver"] = ["show version"],
        ["cpu"] = ["show processes cpu"],
        ["mem"] = ["show memory statistics"],
        ["device"] = ["show environment all"],
        ["optics"] = ["show interface {IF} transceiver"],
        ["ifb"] = ["show ip interface brief", "show interfaces status"],
        ["ifd"] = ["show interfaces"],
        ["lldp"] = ["show lldp neighbors"],
        ["route"] = ["show ip route"],
        ["arp"] = ["show arp"],
        ["vlan"] = ["show vlan brief"],
        ["mac"] = ["show mac address-table"],
        ["stp"] = ["show spanning-tree"],
        ["acl"] = ["show access-lists"],
        ["sess"] = [], // IOS 路由器/交换机无连接会话表（会话项仅防火墙触发，走 ASA 分支）
        ["users"] = ["show users"],
        ["clock"] = ["show clock", "show ntp status"],
        ["log"] = ["show log"],
        ["run"] = ["show running-config"],
        ["diag"] = ["show tech-support"],
    };

    private static readonly Dictionary<string, string[]> CiscoAsa = new()
    {
        ["ver"] = ["show version"],
        ["cpu"] = ["show cpu usage"],
        ["mem"] = ["show memory"],
        ["device"] = ["show environment"],
        ["optics"] = ["show interface {IF}"],
        ["ifb"] = ["show interface ip brief"],
        ["ifd"] = ["show interface"],
        ["lldp"] = [], // ASA 无 LLDP
        ["route"] = ["show route"],
        ["arp"] = ["show arp"],
        ["vlan"] = [], // ASA 无交换式 VLAN
        ["mac"] = [],
        ["stp"] = [],
        ["acl"] = ["show access-list"],
        ["sess"] = ["show conn count", "show xlate count"],
        ["users"] = ["show who"],
        ["clock"] = ["show clock", "show ntp status"],
        ["log"] = ["show logging"],
        ["run"] = ["show running-config"],
        ["diag"] = ["show tech-support"],
    };

    private static readonly Dictionary<string, string[]> Ruijie = new()
    {
        ["ver"] = ["show version"],
        ["cpu"] = ["show cpu"],
        ["mem"] = ["show memory"],
        ["device"] = ["show environment"],
        ["optics"] = ["show interface {IF} transceiver"],
        ["ifb"] = ["show ip interface brief", "show interface status"],
        ["ifd"] = ["show interfaces"],
        ["lldp"] = ["show lldp neighbors"],
        ["route"] = ["show ip route"],
        ["arp"] = ["show arp"],
        ["vlan"] = ["show vlan brief"],
        ["mac"] = ["show mac-address-table"],
        ["stp"] = ["show spanning-tree"],
        ["acl"] = ["show access-lists"],
        ["sess"] = [], // 锐捷出口设备的会话命令随型号差异大
        ["users"] = ["show users"],
        ["clock"] = ["show clock"],
        ["log"] = ["show log"],
        ["run"] = ["show running-config"],
        ["diag"] = ["show tech-support"],
    };

    /// <summary>取某厂商 + 设备类型下某巡检项的全部命令行（已替换接口占位）。空数组 = 该平台无对应查看命令。</summary>
    public static string[] Commands(Vendor v, DeviceType dt, string key)
    {
        var map = v switch
        {
            Vendor.Huawei => Huawei,
            Vendor.H3C => H3C,
            Vendor.Cisco => dt == DeviceType.Firewall ? CiscoAsa : CiscoIos,
            _ => Ruijie,
        };
        if (!map.TryGetValue(key, out var rows)) return [];
        var iface = GeneratorFactory.Get(v).ExampleInterface(dt);
        return rows.Select(r => r.Replace("{IF}", iface)).ToArray();
    }

    /// <summary>生成巡检输出时的系统标识（思科防火墙标注 ASA）。</summary>
    private static string OsLabel(Vendor v, DeviceType dt) =>
        v == Vendor.Cisco && dt == DeviceType.Firewall ? "ASA" : EnumText.OsName(v);

    /// <summary>
    /// 生成带注释说明的巡检命令文本（含时间戳头部）。items 为已勾选项目（按目录顺序）。
    /// 注意：注释行用于阅读，粘贴到设备前请使用 <see cref="StripComments"/> 得到纯命令。
    /// </summary>
    public static string Generate(Vendor v, DeviceType dt, string hostname, IEnumerable<InsItem> items)
    {
        var comment = v == Vendor.Cisco ? "!" : "#";
        var b = new CfgBuilder(comment);
        var bar = new string(comment[0], 62);

        var picked = items.ToList();
        b.Line(bar);
        b.Line($"{comment} NetConfigPro 一键巡检命令  ·  {EnumText.VendorName(v)} ({OsLabel(v, dt)})  ·  {EnumText.DeviceName(dt)}");
        if (!string.IsNullOrWhiteSpace(hostname)) b.Line($"{comment} 设备名称 : {hostname.Trim()}");
        b.Line($"{comment} 生成时间 : {DateTime.Now:yyyy-MM-dd HH:mm}");
        b.Note("请在特权模式（用户视图/特权视图）逐条执行，无需进入配置视图；执行过程中按 Ctrl+C 可中断。");
        b.Line(bar);

        var pickedKeys = picked.Select(it => it.Key).ToHashSet();
        var seq = 0;
        foreach (var g in Groups)
        {
            // 只取本组内且当前设备适用的勾选项（按目录顺序）
            var gItems = g.Items.Where(it => pickedKeys.Contains(it.Key) && AppliesTo(it, dt)).ToList();
            if (gItems.Count == 0) continue;

            b.Section($"{g.Name}（{gItems.Count} 项）");
            foreach (var it in gItems)
            {
                var cmds = Commands(v, dt, it.Key);
                seq++;
                b.Blank();
                b.Line($"{comment} [{seq}] {it.Name} —— {it.Tip}");
                if (cmds.Length == 0)
                {
                    b.Note("该平台无对应查看命令（或随型号差异大），请按实际设备型号核对后手工执行。");
                    continue;
                }
                foreach (var cmd in cmds) b.Line(cmd);
            }
        }
        b.Blank();
        b.Line(bar);
        b.Note($"共 {seq} 项巡检命令。检查要点：CPU/内存 < 80%、接口无 Error/CRC、路由/ARP/MAC 无异常、日志无反复告警。");
        b.Line(bar);
        return b.ToString();
    }

    /// <summary>剥离注释行/分隔线/空行，得到可直接粘贴到设备终端执行的纯命令文本。</summary>
    public static string StripComments(string annotated, Vendor v)
    {
        var token = v == Vendor.Cisco ? "!" : "#";
        var lines = annotated.Split('\n');
        var sb = new System.Text.StringBuilder();
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            var t = line.TrimStart();
            if (t.Length == 0) continue;
            if (t.StartsWith(token)) continue;   // 注释/分隔线
            sb.AppendLine(line);                  // 保留原缩进（无缩进也保留空格无害）
        }
        return sb.ToString().TrimEnd('\n', '\r');
    }
}
