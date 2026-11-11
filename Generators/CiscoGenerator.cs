using NetConfigPro.Models;
using static NetConfigPro.Generators.NetHelper;

namespace NetConfigPro.Generators;

/// <summary>思科配置生成器：交换机 / 路由器使用 IOS-XE 语法，防火墙使用 ASA 语法，AP 使用 Autonomous IOS 语法。</summary>
public sealed class CiscoGenerator : VendorGenerator
{
    public override Vendor Vendor => Vendor.Cisco;
    protected override string Comment => "!";
    protected override string[] EnterCommands => ["configure terminal"];
    protected override string ExitCommand => "end";
    protected override string SaveCommand => "write memory";

    public override string ExampleInterface(DeviceType dt) => dt switch
    {
        DeviceType.Switch => "GigabitEthernet1/0/1",
        DeviceType.Router => "GigabitEthernet0/0/0",
        DeviceType.Firewall => "GigabitEthernet0/0",
        DeviceType.AccessPoint => "GigabitEthernet0",
        _ => "GigabitEthernet0/0/0"
    };

    private bool IsAsa(DeviceType dt) => dt == DeviceType.Firewall;

    /// <summary>防火墙（ASA）在头部单独标注，避免误导为 IOS。</summary>
    protected override string OsNameFor(DeviceType dt) =>
        IsAsa(dt) ? "ASA" : "IOS / IOS-XE";

    // =============================== 基础配置 ===============================
    protected override void GenBasic(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var c = s.Basic;
        b.Section("基础配置");
        using var _ = b.Indent();

        b.Line($"hostname {c.Hostname}");

        var dns = (c.DnsServers ?? "").Split(new[] { ' ', ',', '，' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Where(IsValidIp).Select(x => x.Trim()).ToList();

        if (IsAsa(dt))
        {
            // ASA 使用 dns server-group 而不是 ip name-server
            b.Line("dns server-group DefaultDNS");
            using (b.Indent())
            {
                if (dns.Count > 0) b.Line($"name-server {string.Join(' ', dns)}");
                if (!string.IsNullOrWhiteSpace(c.DomainName)) b.Line($"domain-name {c.DomainName.Trim()}");
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(c.DomainName))
                b.Line($"ip domain-name {c.DomainName.Trim()}");
            if (dns.Count > 0) b.Line($"ip name-server {string.Join(' ', dns)}");
        }

        if (!string.IsNullOrWhiteSpace(c.TimeZone))
            b.Line($"clock timezone {c.TimeZone.Trim()} 8");

        if (IsValidIp(c.NtpServer))
            b.Line($"ntp server {c.NtpServer.Trim()}");

        if (IsValidIp(c.SyslogHost))
        {
            b.Line(IsAsa(dt) ? "logging enable" : "logging on");
            // ASA 的日志主机需要绑定接收接口
            b.Line(IsAsa(dt)
                ? $"logging host inside {c.SyslogHost.Trim()}"
                : $"logging host {c.SyslogHost.Trim()}");
        }

        if (!IsAsa(dt))
        {
            b.Line("service timestamps debug datetime msec");
            b.Line("service timestamps log datetime msec");
        }

        // 管理地址
        if (IsValidIp(c.MgmtIp))
        {
            b.Blank();
            if (dt == DeviceType.Switch && c.MgmtVlanId > 0)
            {
                b.Line($"vlan {c.MgmtVlanId}");
                using (b.Indent()) b.Line("name Management");
                b.Line($"interface Vlan{c.MgmtVlanId}");
                using (b.Indent())
                {
                    b.Line($"ip address {c.MgmtIp.Trim()} {c.MgmtMask.Trim()}");
                    b.Line("no shutdown");
                }
                if (IsValidIp(c.DefaultGateway))
                    b.Line($"ip default-gateway {c.DefaultGateway.Trim()}");
            }
            else
            {
                var ifName = string.IsNullOrWhiteSpace(c.LanInterface) ? ExampleInterface(dt) : c.LanInterface.Trim();
                b.Line($"interface {ifName}");
                using (b.Indent())
                {
                    b.Line($"ip address {c.MgmtIp.Trim()} {c.MgmtMask.Trim()}");
                    b.Line("no shutdown");
                }
                if (IsValidIp(c.DefaultGateway))
                {
                    if (IsAsa(dt))
                    {
                        // ASA 的 route 命令第一个参数是接口 nameif 名称，不是物理接口名
                        b.Note("默认路由的出接口使用 nameif 名称 outside，请按实际区域名调整。");
                        b.Line($"route outside 0.0.0.0 0.0.0.0 {c.DefaultGateway.Trim()}");
                    }
                    else
                    {
                        b.Line($"ip route 0.0.0.0 0.0.0.0 {c.DefaultGateway.Trim()}");
                    }
                }
            }
        }

        if (dt is DeviceType.Router or DeviceType.Firewall)
            GenWan(b, s, dt);
    }

    private void GenWan(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var c = s.Basic;
        var wan = string.IsNullOrWhiteSpace(c.WanInterface) ? ExampleInterface(dt) : c.WanInterface.Trim();
        b.Blank();
        b.Section("WAN 上网配置");
        using var _ = b.Indent();

        switch (c.WanMode)
        {
            case WanMode.Dhcp:
                b.Line($"interface {wan}");
                using (b.Indent())
                {
                    b.Line("ip address dhcp");
                    b.Line("no shutdown");
                }
                break;

            case WanMode.Pppoe:
                if (IsAsa(dt))
                {
                    // ASA 的 PPPoE 依赖 vpdn group / ip address pppoe，与 IOS Dialer 完全不同，不做套用
                    b.Note("ASA 拨号上网使用接口配置 ip address pppoe setroute 与 vpdn，命令与 IOS Dialer 差异大，请按 ASA 手册配置（本工具不生成 ASA 拨号命令段）。");
                    break;
                }
                b.Line("interface Dialer1");
                using (b.Indent())
                {
                    b.Line("ip address negotiated");
                    b.Line("encapsulation ppp");
                    b.Line("dialer pool 1");
                    b.Line($"ppp chap hostname {c.PppoeUser}");
                    b.Line($"ppp chap password 0 {c.PppoePassword}");
                }
                b.Line($"interface {wan}");
                using (b.Indent())
                {
                    b.Line("no ip address");
                    b.Line("pppoe enable");
                    b.Line("pppoe-client dial-pool-number 1");
                    b.Line("no shutdown");
                }
                b.Line("ip route 0.0.0.0 0.0.0.0 Dialer1");
                break;

            default:
                b.Line($"interface {wan}");
                using (b.Indent())
                {
                    b.Line($"ip address {c.WanIp.Trim()} {c.WanMask.Trim()}");
                    b.Line("no shutdown");
                }
                if (IsValidIp(c.WanGateway))
                    b.Line(IsAsa(dt)
                        ? $"route outside 0.0.0.0 0.0.0.0 {c.WanGateway.Trim()}"
                        : $"ip route 0.0.0.0 0.0.0.0 {c.WanGateway.Trim()}");
                break;
        }

        if (!string.IsNullOrWhiteSpace(c.LanInterface) && IsValidIp(c.LanIp))
        {
            b.Blank();
            b.Line($"interface {c.LanInterface.Trim()}");
            using (b.Indent())
            {
                b.Line($"ip address {c.LanIp.Trim()} {c.LanMask.Trim()}");
                b.Line("no shutdown");
            }
        }
    }

    // =============================== VLAN ===============================
    protected override void GenVlans(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.Vlans.Where(v => v.Id > 0).ToList();
        if (items.Count == 0) return;

        if (IsAsa(dt))
        {
            b.Section("VLAN 配置");
            using (b.Indent())
                b.Note("ASA 平台不创建交换式 VLAN 与三层 VLAN 接口；如需承载多网段，请使用子接口（GigabitEthernetx/y.z + vlan ID）或 ASDM 配置。");
            return;
        }
        if (IsRouter(dt))
        {
            b.Section("VLAN 配置");
            using (b.Indent())
                b.Note("IOS 路由器不建立 VLAN 数据库（VLAN 由接入交换机承载）；多网段请用子接口：interface GigabitEthernet0/0/0.10 + encapsulation dot1Q 10。");
            return;
        }

        b.Section("VLAN 配置");
        using var _ = b.Indent();

        foreach (var v in items)
        {
            b.Line($"vlan {v.Id}");
            using (b.Indent())
            {
                var name = string.IsNullOrWhiteSpace(v.Name) ? v.Description : v.Name;
                if (!string.IsNullOrWhiteSpace(name)) b.Line($"name {name.Trim()}");
            }
        }

        foreach (var v in items.Where(x => x.L3Enabled && IsValidIp(x.IpAddress)))
        {
            b.Line($"interface Vlan{v.Id}");
            using (b.Indent())
            {
                b.Line($"ip address {v.IpAddress.Trim()} {v.Mask.Trim()}");
                b.Line("no shutdown");
            }
        }
    }

    // =============================== 接口 ===============================
    protected override void GenInterfaces(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.Interfaces.Where(i => !string.IsNullOrWhiteSpace(i.Name)).ToList();
        if (items.Count == 0) return;

        b.Section("接口配置");
        using var _ = b.Indent();

        foreach (var it in items)
        {
            b.Line($"interface {it.Name.Trim()}");
            using (b.Indent())
            {
                if (!string.IsNullOrWhiteSpace(it.Description))
                    b.Line($"description {it.Description.Trim()}");

                if (!IsAsa(dt))
                {
                    switch (it.Mode)
                    {
                        case InterfaceMode.Access:
                            if (dt == DeviceType.Switch) b.Line("switchport mode access");
                            if (dt == DeviceType.Switch) b.Line($"switchport access vlan {it.AccessVlan}");
                            break;

                        case InterfaceMode.Trunk:
                            if (dt == DeviceType.Switch)
                            {
                                b.Line("switchport mode trunk");
                                var vl = ExpandVlanList(it.TrunkVlans);
                                b.Line(vl.Count == 0
                                    ? "switchport trunk allowed vlan all"
                                    : $"switchport trunk allowed vlan {ToCiscoRange(vl)}");
                            }
                            break;

                        case InterfaceMode.Routed:
                            if (dt == DeviceType.Switch) b.Line("no switchport");
                            if (IsValidIp(it.IpAddress)) b.Line($"ip address {it.IpAddress.Trim()} {it.Mask.Trim()}");
                            break;
                    }
                }
                else if (IsValidIp(it.IpAddress))
                {
                    b.Line($"ip address {it.IpAddress.Trim()} {it.Mask.Trim()}");
                }

                b.Line(it.Enabled ? "no shutdown" : "shutdown");
            }
            b.Blank();
        }
    }

    // =============================== 生成树 ===============================
    protected override void GenStp(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var stp = s.Stp;
        b.Section("生成树配置");
        using var _ = b.Indent();

        switch (stp.Mode)
        {
            case StpMode.Stp:
                b.Line("spanning-tree mode pvst");
                b.Line(stp.RootPrimary ? "spanning-tree vlan 1-4094 root primary" : $"spanning-tree vlan 1-4094 priority {stp.Priority}");
                break;

            case StpMode.RapidPvst:
                b.Line("spanning-tree mode rapid-pvst");
                b.Line(stp.RootPrimary ? "spanning-tree vlan 1-4094 root primary" : $"spanning-tree vlan 1-4094 priority {stp.Priority}");
                break;

            case StpMode.Pvst:
                b.Line("spanning-tree mode pvst");
                b.Line(stp.RootPrimary ? "spanning-tree vlan 1-4094 root primary" : $"spanning-tree vlan 1-4094 priority {stp.Priority}");
                break;

            default: // MSTP
                b.Line("spanning-tree mode mst");
                b.Line("spanning-tree mst configuration");
                using (b.Indent())
                {
                    b.Line($"name {stp.RegionName}");
                    foreach (var map in (stp.InstanceMap ?? "").Split(new[] { ';', '，', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts = map.Split(':');
                        if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out var inst)) continue;
                        var vl = ExpandVlanList(parts[1]);
                        if (vl.Count > 0) b.Line($"instance {inst} vlan {ToCiscoRange(vl)}");
                    }
                }
                var inst0 = stp.InstanceMap?.Split(':', 2)[0].Trim();
                var target = int.TryParse(inst0, out var i) ? i : 0;
                b.Line(stp.RootPrimary ? $"spanning-tree mst {target} root primary" : $"spanning-tree mst {target} priority {stp.Priority}");
                break;
        }
    }

    // =============================== 路由 ===============================
    protected override void GenRouting(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var r = s.Routing;
        var on = s.EnabledModules;
        if (!on.Contains(Mod.StatRoute) && !on.Contains(Mod.OspfR) && !on.Contains(Mod.BgpR) && !on.Contains(Mod.DynR))
            return;

        b.Section("路由配置");
        using var _ = b.Indent();

        if (on.Contains(Mod.StatRoute))
        {
            foreach (var sr in s.StaticRoutes.Where(x => IsValidIp(x.Prefix) && !string.IsNullOrWhiteSpace(x.NextHop)))
            {
                var line = IsAsa(dt)
                    ? $"route outside {sr.Prefix.Trim()} {sr.Mask.Trim()} {sr.NextHop.Trim()}"
                    : $"ip route {sr.Prefix.Trim()} {sr.Mask.Trim()} {sr.NextHop.Trim()}";
                if (!IsAsa(dt) && !string.IsNullOrWhiteSpace(sr.Preference)) line += $" {sr.Preference.Trim()}";
                if (!IsAsa(dt) && !string.IsNullOrWhiteSpace(sr.Description)) line += $" name {sr.Description.Trim().Replace(' ', '_')}";
                b.Line(line);
            }
        }

        if (on.Contains(Mod.OspfR))
        {
            b.Blank();
            b.Line($"router ospf {r.OspfProcess}");
            using (b.Indent())
            {
                b.Line($"router-id {r.RouterId}");
                if (r.RedistributeConnected) b.Line("redistribute connected subnets");
                if (r.DefaultRouteAdvertise) b.Line("default-information originate");

                foreach (var area in s.OspfNetworks.Where(x => IsValidIp(x.Network)).GroupBy(x => x.Area))
                {
                    var type = area.First().AreaType;
                    if (type == "Stub") b.Line($"area {area.Key} stub");
                    else if (type == "NSSA") b.Line($"area {area.Key} nssa");

                    foreach (var n in area)
                    {
                        // ASA 使用正掩码，IOS 使用反掩码
                        var mask = IsAsa(dt)
                            ? WildcardToMask(n.Wildcard)
                            : n.Wildcard.Trim();
                        b.Line($"network {n.Network.Trim()} {mask} area {area.Key}");
                    }
                }
            }
        }

        if (on.Contains(Mod.BgpR))
        {
            b.Blank();
            b.Line($"router bgp {r.BgpAs}");
            using (b.Indent())
            {
                b.Line($"bgp router-id {r.RouterId}");
                foreach (var n in s.BgpNeighbors.Where(x => IsValidIp(x.PeerIp)))
                {
                    b.Line($"neighbor {n.PeerIp.Trim()} remote-as {n.RemoteAs}");
                    if (!string.IsNullOrWhiteSpace(n.Description))
                        b.Line($"neighbor {n.PeerIp.Trim()} description {n.Description.Trim()}");
                }
                if (r.RedistributeConnected) b.Line("redistribute connected");
                foreach (var sr in s.StaticRoutes.Where(x => IsValidIp(x.Prefix)))
                    b.Line($"network {sr.Prefix.Trim()} mask {sr.Mask.Trim()}");
            }
        }

        if (on.Contains(Mod.DynR))
        {
            if (IsAsa(dt))
            {
                b.Blank();
                b.Note("ASA 上 EIGRP/RIP 支持有限（EIGRP 需特定版本且掩码为正掩码），本工具推荐用上方 OSPF 或静态路由承载，已跳过。");
            }
            else
            {
                b.Blank();
                b.Line($"router eigrp {r.EigrpAs}");
                using (b.Indent())
                {
                    foreach (var n in s.OspfNetworks.Where(x => IsValidIp(x.Network)))
                        b.Line($"network {n.Network.Trim()} {n.Wildcard.Trim()}");
                    b.Line("no auto-summary");
                    if (r.RedistributeConnected) b.Line("redistribute connected");
                }
                b.Note("动态路由模块在思科平台输出 EIGRP；如需 RIPv2 请手工把本段改为 router rip 并核对网段宣告。");
            }
        }
    }

    private static string WildcardToMask(string wildcard) => WildcardFromMask(wildcard);

    // =============================== ACL ===============================
    protected override void GenAcl(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var groups = s.AclRules.Where(x => !string.IsNullOrWhiteSpace(x.Number))
                               .GroupBy(x => x.Number.Trim()).ToList();
        if (groups.Count == 0) return;

        b.Section("ACL 访问控制列表");
        using var _ = b.Indent();

        if (IsAsa(dt))
        {
            // ASA：ACL 一律命名（不支持编号）、掩码用正掩码而非反掩码
            foreach (var g in groups)
            {
                var extended = g.Any(x => x.Kind == AclKind.Extended);
                var name = "NCP-ACL" + g.Key;
                foreach (var rl in g)
                {
                    var proto = string.IsNullOrWhiteSpace(rl.Protocol) ? "ip" : rl.Protocol.Trim().ToLowerInvariant();
                    b.Line($"access-list {name} extended {rl.Action} {proto} {AsaNet(rl.Source, rl.SrcWildcard)} {AsaNet(rl.Destination, rl.DstWildcard)}");
                }
                b.Note($"ASA 使用命名 ACL，掩码为正掩码；编号 {g.Key} 已并入名 {name}。");
                b.Blank();
            }
            return;
        }

        foreach (var g in groups)
        {
            var extended = g.Any(x => x.Kind == AclKind.Extended);
            var num = CiscoAclNumber(extended, g.Key);
            if (num != g.Key)
                b.Note($"ACL 编号 {g.Key} 按 IOS 编号规范调整为 {num}（标准 1-99/1300-1999，扩展 100-199/2000-2699）。");

            if (extended)
            {
                b.Line($"ip access-list extended {num}");
                using (b.Indent())
                {
                    foreach (var rl in g)
                    {
                        if (!string.IsNullOrWhiteSpace(rl.Description)) b.Line($"remark {rl.Description.Trim()}");
                        var proto = string.IsNullOrWhiteSpace(rl.Protocol) ? "ip" : rl.Protocol.Trim().ToLowerInvariant();
                        b.Line($"{rl.Action} {proto} {CiscoAddr(rl.Source, rl.SrcWildcard)} {CiscoAddr(rl.Destination, rl.DstWildcard)}" + TrSuffix(rl));
                    }
                }
            }
            else
            {
                var stdTimeRange = false;
                foreach (var rl in g)
                {
                    if (!string.IsNullOrWhiteSpace(rl.Description)) b.Line($"access-list {num} remark {rl.Description.Trim()}");
                    // 标准 ACL 不支持 time-range（思科官方：仅扩展编号/命名 ACL 支持）——填了也不输出，避免设备报错
                    if (!string.IsNullOrWhiteSpace(rl.TimeRange)) stdTimeRange = true;
                    else b.Line($"access-list {num} {rl.Action} {CiscoAddr(rl.Source, rl.SrcWildcard)}");
                }
                if (stdTimeRange)
                    b.Note("标准 ACL 不支持 time-range（仅扩展 ACL 支持）：本组规则的“时间段”已忽略，如需定时生效请改用扩展 ACL（编号 100-199/2000-2699 或命名）。");
            }
            b.Blank();
        }
    }

    private static string CiscoAclNumber(bool extended, string raw)
    {
        var n = int.TryParse(raw, out var v) ? v : (extended ? 100 : 10);
        if (extended)
        {
            var ok = (n >= 100 && n <= 199) || (n >= 2000 && n <= 2699);
            return ok ? n.ToString() : "100";
        }
        var okStd = (n >= 1 && n <= 99) || (n >= 1300 && n <= 1999);
        return okStd ? n.ToString() : "10";
    }

    /// <summary>ASA 网段写法：any / host / 正掩码（ASA 不用反掩码）。</summary>
    private static string AsaNet(string? ip, string? wildcard)
    {
        var a = (ip ?? "").Trim();
        if (a.Equals("any", StringComparison.OrdinalIgnoreCase)) return "any";
        var w = (wildcard ?? "").Trim();
        if (w.Equals("0.0.0.0") || w.Length == 0) return $"host {a}";
        if (w.Equals("255.255.255.255")) return "any";
        return $"{a} {WildcardToMask(w)}";
    }

    // =============================== NAT ===============================
    protected override void GenNat(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var n = s.Nat;
        var outside = string.IsNullOrWhiteSpace(n.OutsideInterface) ? ExampleInterface(dt) : n.OutsideInterface.Trim();
        var inside = string.IsNullOrWhiteSpace(n.InsideInterface) ? "" : n.InsideInterface.Trim();

        b.Section("NAT 地址转换");
        using var _ = b.Indent();

        if (IsAsa(dt))
        {
            if (n.Kind == NatKind.EasyIp)
            {
                var net = ParseCidr(s.Basic.LanIp) is null
                    ? ("192.168.1.0", "255.255.255.0")
                    : SubnetOf(s.Basic.LanIp, s.Basic.LanMask);
                b.Line("object network INSIDE-NET");
                using (b.Indent())
                {
                    b.Line($"subnet {net.Item1} {net.Item2}");
                    b.Line("nat (inside,outside) dynamic interface");
                }
            }
            else
            {
                b.Line("object network SERVER-MAP");
                using (b.Indent())
                {
                    b.Line($"host {n.InsideIp.Trim()}");
                    b.Line($"nat (inside,outside) static {n.GlobalIp.Trim()} service {n.Protocol.Trim().ToLowerInvariant()} {n.InsidePort.Trim()} {n.GlobalPort.Trim()}");
                }
            }
            b.Note("ASA 的 inside/outside 为接口 nameif 名称，请与安全区域配置保持一致。");
            return;
        }

        if (n.Kind == NatKind.EasyIp)
        {
            b.Line($"ip nat inside source list {CiscoAclNumber(false, n.AclNumber)} interface {outside} overload");
        }
        else
        {
            b.Line($"ip nat inside source static {n.Protocol.Trim().ToLowerInvariant()} {n.InsideIp.Trim()} {n.InsidePort.Trim()} {n.GlobalIp.Trim()} {n.GlobalPort.Trim()}");
        }

        if (inside.Length > 0)
        {
            b.Line($"interface {inside}");
            using (b.Indent()) b.Line("ip nat inside");
        }
        b.Line($"interface {outside}");
        using (b.Indent()) b.Line("ip nat outside");
    }

    private static (string, string) SubnetOf(string ip, string mask)
    {
        if (!System.Net.IPAddress.TryParse(ip, out var a) || !System.Net.IPAddress.TryParse(mask, out var m))
            return ("192.168.1.0", "255.255.255.0");
        var ab = a.GetAddressBytes();
        var mb = m.GetAddressBytes();
        var net = new byte[4];
        for (var i = 0; i < 4; i++) net[i] = (byte)(ab[i] & mb[i]);
        return (new System.Net.IPAddress(net).ToString(), mask);
    }

    // =============================== 防火墙 ===============================
    protected override void GenFirewall(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var zones = s.Zones.Where(z => !string.IsNullOrWhiteSpace(z.Name)).ToList();
        var policies = s.Policies.Where(p => !string.IsNullOrWhiteSpace(p.Name)).ToList();

        if (zones.Count > 0)
        {
            b.Section("安全区域（ASA nameif）");
            using var _ = b.Indent();
            foreach (var z in zones)
            {
                foreach (var ifn in (z.Interfaces ?? "").Split(new[] { ',', '，', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    b.Line($"interface {ifn.Trim()}");
                    using (b.Indent())
                    {
                        b.Line($"nameif {z.Name.Trim().ToLowerInvariant()}");
                        b.Line($"security-level {z.Priority}");
                    }
                }
            }
        }

        if (policies.Count == 0) return;

        b.Section("安全策略（ASA 访问控制列表）");
        using var __ = b.Indent();

        var byZone = policies.GroupBy(p => p.DestZone.Trim().ToLowerInvariant());
        foreach (var grp in byZone)
        {
            var aclName = $"{grp.Key.ToUpperInvariant()}-IN";
            foreach (var p in grp)
            {
                var service = (p.Service ?? "any").Trim().ToLowerInvariant();
                var port = ServiceToPort(p.Service);

                string proto;
                string portClause = "";
                if (service is "icmp" or "ping")
                {
                    proto = "icmp";
                }
                else if (service is "any" or "")
                {
                    proto = "ip";
                }
                else if (service is "dns" or "domain")
                {
                    proto = "udp";
                    portClause = "eq 53";
                }
                else
                {
                    proto = "tcp";
                    if (port != "any" && int.TryParse(port, out _)) portClause = $"eq {port}";
                }

                var src = ParseCidr(p.Source);
                var dst = ParseCidr(p.Destination);
                var line = $"access-list {aclName} extended {p.Action.Trim().ToLowerInvariant()} {proto} ";
                line += src is null ? "any " : $"{src.Value.Ip} {src.Value.Mask} ";
                line += dst is null ? "any " : $"{dst.Value.Ip} {dst.Value.Mask} ";
                if (portClause.Length > 0) line += portClause;

                b.Line(line.TrimEnd());
            }

            var iface = zones.FirstOrDefault(z => z.Name.Trim().Equals(grp.Key, StringComparison.OrdinalIgnoreCase))
                            ?.Interfaces.Split(new[] { ',', '，', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            .FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(iface))
                b.Line($"access-group {aclName} in interface {iface}");
            b.Blank();
        }
    }

    // =============================== DHCP ===============================
    protected override void GenDhcp(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var pools = s.DhcpPools.Where(p => !string.IsNullOrWhiteSpace(p.Name) && IsValidIp(p.Network)).ToList();
        if (pools.Count == 0) return;

        b.Section("DHCP 服务");
        using var _ = b.Indent();

        if (IsAsa(dt))
        {
            // ASA 使用 dhcpd 指令，与 IOS 的 ip dhcp pool 语法不同
            foreach (var p in pools)
            {
                var start = NextIp(p.ExcludeTo) ?? "192.168.10.100";
                var end = LastUsableHost(p.Network, p.Mask);
                b.Line($"dhcpd address {start}-{end} inside");
                var dns = new[] { p.Dns1, p.Dns2 }.Where(IsValidIp).Select(x => x!.Trim());
                if (dns.Any()) b.Line($"dhcpd dns {string.Join(' ', dns)} interface inside");
                if (IsValidIp(p.Gateway)) b.Line($"dhcpd option 3 ip {p.Gateway.Trim()}");
                b.Line($"dhcpd lease {Math.Max(0, p.LeaseDays) * 86400}");
                b.Line("dhcpd enable inside");
            }
            b.Note("ASA 的 dhcpd 地址池绑定在接口上，inside 请替换为实际内网 nameif 名称。");
            return;
        }

        foreach (var p in pools.Where(x => IsValidIp(x.ExcludeFrom) && IsValidIp(x.ExcludeTo)))
            b.Line($"ip dhcp excluded-address {p.ExcludeFrom.Trim()} {p.ExcludeTo.Trim()}");

        foreach (var p in pools)
        {
            b.Blank();
            b.Line($"ip dhcp pool {p.Name.Trim()}");
            using (b.Indent())
            {
                b.Line($"network {p.Network.Trim()} {p.Mask.Trim()}");
                if (IsValidIp(p.Gateway)) b.Line($"default-router {p.Gateway.Trim()}");
                var dns = new[] { p.Dns1, p.Dns2 }.Where(IsValidIp).Select(x => x!.Trim());
                if (dns.Any()) b.Line($"dns-server {string.Join(' ', dns)}");
                b.Line($"lease {Math.Max(0, p.LeaseDays)}");
            }
        }
    }

    // =============================== 无线 ===============================
    protected override void GenWireless(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var w = s.Wireless;
        var radio = w.RadioBand == "5G" ? "Dot11Radio1" : "Dot11Radio0";

        b.Section("无线配置（Autonomous AP）");
        using var _ = b.Indent();

        b.Line($"dot11 ssid {w.Ssid}");
        using (b.Indent())
        {
            b.Line($"vlan {w.VlanId}");
            b.Line("authentication open");
            if (w.Wpa == WpaMode.Wpa3Sae)
            {
                b.Line("authentication key-management sae");
                b.Note("WPA3/SAE 需 IOS 15.3(3)JN 及以上版本支持。");
            }
            else
            {
                b.Line("authentication key-management wpa version 2");
            }
            b.Line($"wpa-psk ascii 0 {w.Password}");
            if (!w.HideSsid) b.Line("guest-mode");
        }

        b.Line($"interface {radio}");
        using (b.Indent())
        {
            b.Line($"ssid {w.Ssid}");
            b.Line($"encryption vlan {w.VlanId} mode ciphers aes-ccm");
            if (string.IsNullOrWhiteSpace(w.Channel) || w.Channel.Trim().ToLowerInvariant() == "auto")
                b.Line("channel least-congested");
            else
                b.Line($"channel {w.Channel.Trim()}");
            b.Line(w.Power >= 100 ? "power local maximum" : $"power local {w.Power}");
            if (w.MaxUsers > 0) b.Line($"max-associations {w.MaxUsers}");
            b.Line("no shutdown");
        }

        b.Line($"interface {radio}.{w.VlanId}");
        using (b.Indent())
        {
            b.Line($"encapsulation dot1Q {w.VlanId} native");
            b.Line($"bridge-group {w.VlanId}");
        }
        b.Line($"interface GigabitEthernet0.{w.VlanId}");
        using (b.Indent())
        {
            b.Line($"encapsulation dot1Q {w.VlanId} native");
            b.Line($"bridge-group {w.VlanId}");
        }
    }

    // =============================== SNMP ===============================
    protected override void GenSnmp(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var n = s.Snmp;
        b.Section("SNMP 配置");
        using var _ = b.Indent();

        var v3 = n.Version.Trim().Equals("v3", StringComparison.OrdinalIgnoreCase);
        // IOS / ASA 的 version 关键字取值是 1、2c、3，不带前缀 v
        var verToken = v3 ? "3" : n.Version.Trim().ToLowerInvariant().TrimStart('v');

        if (v3)
        {
            // ASA 的 snmp-server group 不支持 read/write 视图参数（IOS 才用 v1default）
            b.Line(IsAsa(dt)
                ? "snmp-server group NETADMIN v3 priv"
                : "snmp-server group NETADMIN v3 priv read v1default write v1default");
            b.Line($"snmp-server user {n.V3User} NETADMIN v3 auth sha {n.V3AuthPassword} priv aes 128 {n.V3PrivPassword}");
            if (IsValidIp(n.TrapHost))
                b.Line(IsAsa(dt)
                    ? $"snmp-server host inside {n.TrapHost.Trim()} version 3 {n.V3User}"
                    : $"snmp-server host {n.TrapHost.Trim()} version 3 priv {n.V3User}");
        }
        else
        {
            b.Line($"snmp-server community {n.Community} RO");
            if (IsValidIp(n.TrapHost))
                b.Line(IsAsa(dt)
                    ? $"snmp-server host inside {n.TrapHost.Trim()} community {n.Community} version {verToken}"
                    : $"snmp-server host {n.TrapHost.Trim()} version {verToken} {n.Community}");
        }

        if (!string.IsNullOrWhiteSpace(n.Location)) b.Line($"snmp-server location {n.Location.Trim()}");
        if (!string.IsNullOrWhiteSpace(n.Contact)) b.Line($"snmp-server contact {n.Contact.Trim()}");
    }

    // =============================== SSH ===============================
    protected override void GenSsh(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        if (!s.Ssh.Enabled) return;
        var ssh = s.Ssh;
        b.Section("SSH 远程管理");
        using var _ = b.Indent();

        if (string.IsNullOrWhiteSpace(s.Basic.DomainName))
            b.Line($"ip domain-name {s.Basic.Hostname}.local");

        b.Line("crypto key generate rsa modulus 2048");
        b.Line($"ip ssh version {ssh.Version}");
        b.Line($"ip ssh time-out {ssh.AuthTimeout}");
        b.Line($"ip ssh authentication-retries {ssh.AuthRetries}");

        b.Line($"username {s.Security.LocalUser} privilege {s.Security.Privilege} secret {s.Security.Password}");
        b.Line("line vty 0 4");
        using (b.Indent())
        {
            b.Line("transport input ssh");
            b.Line("login local");
            b.Line($"exec-timeout {s.Security.IdleTimeout} 0");
            b.Line("logging synchronous");
        }
        if (ssh.Port != 22)
            b.Note("IOS 默认不支持修改 SSH 端口（需 rotary 组配置），此处保持 22。");
    }

    // =============================== 安全加固 ===============================
    protected override void GenSecurity(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var sec = s.Security;
        b.Section("安全加固");
        using var _ = b.Indent();

        b.Line("service password-encryption");
        b.Line(IsAsa(dt)
            ? $"enable password {sec.EnablePassword}"
            : $"enable secret {sec.EnablePassword}");
        b.Line($"username {sec.LocalUser} privilege {sec.Privilege} secret {sec.Password}");

        if (IsAsa(dt))
        {
            // ASA 不支持 IOS 的密码长度策略与登录阻断命令
            b.Line("aaa authentication ssh console LOCAL");
            // Web 管理由「Web 管理」模块统一控制；未启用时保持 http 关闭
            if (!s.WebAdmin.HttpEnabled && !s.WebAdmin.HttpsEnabled) b.Line("no http server");
        }
        else
        {
            b.Line($"security passwords min-length {sec.PasswordMinLength}");
            b.Line($"login block-for 300 attempts {sec.LoginRetryLimit} within 60");
            if (!s.WebAdmin.HttpEnabled) b.Line("no ip http server");
            b.Line("ip http secure-server");
        }

        b.Line("line vty 0 4");
        using (b.Indent())
        {
            b.Line("exec-timeout 10 0");
            b.Line("login local");
            b.Line("transport input ssh");
        }
    }

    // =============================== v2：链路聚合 ===============================
    protected override void GenLag(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.LinkAggs.Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Members)).ToList();
        if (items.Count == 0) return;

        b.Section("链路聚合 (Port-channel)");
        using var _ = b.Indent();
        foreach (var it in items)
        {
            b.Line($"interface Port-channel{it.Id}");
            using (b.Indent())
            {
                if (!string.IsNullOrWhiteSpace(it.Description)) b.Line($"description {it.Description.Trim()}");
            }
            var mode = it.Mode.Trim().ToLowerInvariant() == "lacp" ? "active" : "on";
            foreach (var m in ExpandMembers(it.Members))
            {
                b.Line($"interface {m}");
                using (b.Indent()) b.Line($"channel-group {it.Id} mode {mode}");
            }
            b.Blank();
        }
        b.Note("若 Port-channel 需承载业务 VLAN，请补 switchport trunk encapsulation / allowed vlan 等二层配置。");
    }

    // =============================== v2：VRRP ===============================
    protected override void GenVrrp(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.Vrrps.Where(x => x.GroupId > 0 && IsValidIp(x.VirtualIp)).ToList();
        if (items.Count == 0) return;

        b.Section("VRRP 网关冗余");
        using var _ = b.Indent();
        if (IsAsa(dt))
        {
            b.Note("ASA 防火墙不支持 IOS 风格 VRRP，请使用 failover 或接口 standby 组，已跳过本模块。");
            return;
        }
        foreach (var it in items)
        {
            b.Line($"interface {NormVlanIface(it.Interface)}");
            using (b.Indent())
            {
                b.Line($"vrrp {it.GroupId} ip {it.VirtualIp.Trim()}");
                b.Line($"vrrp {it.GroupId} priority {it.Priority}");
                b.Line(it.Preempt ? $"vrrp {it.GroupId} preempt" : $"no vrrp {it.GroupId} preempt");
                if (!string.IsNullOrWhiteSpace(it.Description)) b.Line($"vrrp {it.GroupId} description {it.Description.Trim()}");
            }
            b.Blank();
        }
        b.Note("两台设备配置相同 VRRP 组与虚拟 IP；主备优先级不同并开启 preempt。");
    }

    // =============================== v2：DHCP Snooping ===============================
    protected override void GenDhcpSnoop(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var cfg = s.DhcpSnoop;
        var vlans = ExpandVlanList(cfg.VlanIds);
        if (vlans.Count == 0) return;

        b.Section("DHCP Snooping");
        using var _ = b.Indent();
        b.Line("ip dhcp snooping");
        b.Line($"ip dhcp snooping vlan {ToCiscoRange(vlans)}");
        if (!cfg.Option82) b.Line("no ip dhcp snooping information option");
        foreach (var port in ExpandMembers(cfg.TrustPorts))
        {
            b.Line($"interface {port}");
            using (b.Indent()) b.Line("ip dhcp snooping trust");
        }
        b.Note("仅上行口设为 trust；IOS 默认会插入 Option82，如需关闭已在上方生成对应命令。");
    }

    // =============================== v2：端口镜像 ===============================
    protected override void GenMirror(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.Mirrors.Where(x => x.SessionId > 0 && !string.IsNullOrWhiteSpace(x.DestPort)).ToList();
        if (items.Count == 0) return;

        b.Section("端口镜像 (SPAN)");
        using var _ = b.Indent();
        foreach (var it in items)
        {
            foreach (var src in ExpandMembers(it.SourcePorts))
                b.Line($"monitor session {it.SessionId} source interface {src} {DirWord(it.Direction)}");
            b.Line($"monitor session {it.SessionId} destination interface {it.DestPort.Trim()}");
            b.Blank();
        }
        b.Note("源接口支持 'GigabitEthernet1/0/1 - 8' 区间写法，方向为 both / rx / tx。");
    }

    // =============================== v2：端口安全 ===============================
    protected override void GenPortSec(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.PortSecs.Where(x => !string.IsNullOrWhiteSpace(x.Interface)).ToList();
        if (items.Count == 0) return;

        b.Section("端口安全");
        using var _ = b.Indent();
        foreach (var it in items)
        {
            b.Line($"interface {it.Interface.Trim()}");
            using (b.Indent())
            {
                b.Line("switchport port-security");
                b.Line($"switchport port-security maximum {Math.Max(1, it.MaxMac)}");
                if (it.Sticky) b.Line("switchport port-security mac-address sticky");
                b.Line($"switchport port-security violation {it.Violation.Trim().ToLowerInvariant()}");
            }
            b.Blank();
        }
    }

    // =============================== v2：802.1X ===============================
    protected override void GenDot1x(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var cfg = s.Dot1x;
        if (!cfg.Enabled || !IsValidIp(cfg.ServerIp)) return;

        b.Section("802.1X 接入认证");
        using var _ = b.Indent();

        b.Line("aaa new-model");
        b.Line("radius server RADIUS");
        using (b.Indent())
        {
            b.Line($"address ipv4 {cfg.ServerIp.Trim()} auth-port {cfg.AuthPort.Trim()} acct-port {cfg.AcctPort.Trim()}");
            b.Line($"key {cfg.SharedKey}");
        }
        b.Line("aaa authentication dot1x default group RADIUS");
        b.Line("dot1x system-auth-control");
        if (!string.IsNullOrWhiteSpace(cfg.Interface))
        {
            b.Line($"interface {cfg.Interface.Trim()}");
            using (b.Indent()) b.Line($"dot1x port-control {cfg.PortControl.Trim().ToLowerInvariant()}");
        }
        b.Note("802.1X 需交换机开启全局认证；接口留空则只下发全局部分。");
    }

    // =============================== v2：LLDP ===============================
    protected override void GenLldp(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        if (!s.Lldp.Enabled) return;
        b.Section("LLDP 链路发现");
        using var _ = b.Indent();
        if (IsAsa(dt))
        {
            b.Note("ASA 防火墙默认无 LLDP（9.3+ 可通过 lldp transmit/receive 启用），已跳过。");
            return;
        }
        b.Line("lldp run");
        b.Note("接口级 lldp transmit / receive 可控制单口收发。");
    }

    private static string DirWord(string dir) => dir.Trim().ToLowerInvariant() switch
    {
        "inbound" => "rx",
        "outbound" => "tx",
        _ => "both"
    };

    // =============================== v3：本地用户 ===============================
    protected override void GenLocalUsers(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var users = s.LocalUsers.Where(u => !string.IsNullOrWhiteSpace(u.Name)).ToList();
        if (users.Count == 0) return;

        b.Section("本地用户账号");
        foreach (var u in users)
        {
            b.Line(IsAsa(dt)
                ? $"username {u.Name.Trim()} password {u.Password} privilege {Math.Clamp(u.Privilege, 0, 15)}"
                : $"username {u.Name.Trim()} privilege {Math.Clamp(u.Privilege, 0, 15)} secret {u.Password}");
        }
        b.Note("IOS/ASA 无法按账号细分接入方式，接入限制请在 line vty / http 服务处用 access-class 或 aaa 完成。");
    }

    // =============================== v3：Telnet ===============================
    protected override void GenTelnet(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        if (!s.Telnet.Enabled) return;
        b.Section("Telnet 接入");
        if (IsAsa(dt))
        {
            b.Note("ASA 通过接口下的 telnet <网络> <反掩码> <接口> 控制来源，请按实际管理接口补充，已跳过 line vty。");
            return;
        }
        b.Line("line vty 0 4");
        using (b.Indent())
        {
            // 同时启用 SSH 模块时放行两者，避免后置 vty 配置把 SSH 覆盖掉
            b.Line(s.Ssh.Enabled ? "transport input telnet ssh" : "transport input telnet");
            if (!string.IsNullOrWhiteSpace(s.Telnet.AclId))
                b.Line($"access-class {s.Telnet.AclId.Trim()} in");
        }
    }

    // =============================== v3：Web 管理 ===============================
    protected override void GenWeb(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var w = s.WebAdmin;
        if (!w.HttpEnabled && !w.HttpsEnabled) return;
        b.Section("Web 管理");
        if (IsAsa(dt))
        {
            b.Line("http server enable");
            if (!string.IsNullOrWhiteSpace(w.AllowSource))
                b.Note($"来源限制 '{w.AllowSource.Trim()}'：ASA 用 http <网络> <反掩码> <接口> 限定，请按实际补充。");
            if (w.HttpsEnabled) b.Note("ASA HTTPS 需要配置 SSL 信任点（ssl trust-point），请按手册补充。");
            return;
        }
        if (w.HttpEnabled)
        {
            b.Line("ip http server");
            if (int.TryParse(w.Port, out _)) b.Line($"ip http port {w.Port.Trim()}");
        }
        if (w.HttpsEnabled) b.Line("ip http secure-server");
        if (!string.IsNullOrWhiteSpace(w.AllowSource))
            b.Note($"来源限制 '{w.AllowSource.Trim()}'：IOS 建议创建 ACL 后绑定 ip http access-class，请按实际补充。");
        b.Note("Web 登录复用本地用户，请确保账号已具备权限并修改默认口令。");
    }

    // =============================== v6：端口映射 ===============================
    protected override void GenPortMaps(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.PortMaps.Where(x => IsValidIp(x.InsideIp) && x.ExternalPort.Trim().Length > 0 && x.ExternalPort.Trim().All(ch => ch >= '0' && ch <= '9')).ToList();
        if (items.Count == 0) return;

        b.Section("端口映射 (NAT Server)");
        using var _ = b.Indent();
        if (IsAsa(dt))
        {
            foreach (var m in items)
            {
                var g = IsValidIp(m.ExternalIp) ? $"{m.ExternalIp.Trim()} {m.ExternalPort.Trim()}" : $"interface {m.ExternalPort.Trim()}";
                b.Line($"static (inside,outside) {(m.Protocol ?? "tcp").ToLowerInvariant()} {g} {m.InsideIp.Trim()} {m.InsidePort.Trim()}");
            }
            b.Note("ASA 默认放行返回流量；如需从 Untrust 主动访问还需配置相应 ACL。");
            return;
        }

        var insideI = string.IsNullOrWhiteSpace(s.Nat.InsideInterface) ? "" : s.Nat.InsideInterface.Trim();
        var outsideI = string.IsNullOrWhiteSpace(s.Nat.OutsideInterface) ? ExampleInterface(dt) : s.Nat.OutsideInterface.Trim();
        if (insideI.Length > 0)
        {
            b.Line($"interface {insideI}");
            using (b.Indent()) b.Line("ip nat inside");
            b.Line($"interface {outsideI}");
            using (b.Indent()) b.Line("ip nat outside");
        }
        foreach (var m in items)
        {
            var proto = (m.Protocol ?? "tcp").Trim().ToLowerInvariant();
            var target = IsValidIp(m.ExternalIp)
                ? $"{m.ExternalIp.Trim()} {m.ExternalPort.Trim()}"
                : $"interface {outsideI} {m.ExternalPort.Trim()}";
            b.Line($"ip nat inside source static {proto} {m.InsideIp.Trim()} {m.InsidePort.Trim()} {target}");
        }
        b.Note("同一外网口端口+协议只能映射一台内网服务器；不同端口可区分多台。");
    }

    // =============================== v6：NAT 静态一对一 ===============================
    protected override void GenStaticNats(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.StaticNats.Where(x => IsValidIp(x.GlobalIp) && IsValidIp(x.InsideIp)).ToList();
        if (items.Count == 0) return;

        b.Section("NAT 静态一对一");
        using var _ = b.Indent();
        if (IsAsa(dt))
        {
            foreach (var m in items)
                b.Line($"static (inside,outside) {m.GlobalIp.Trim()} {m.InsideIp.Trim()} netmask 255.255.255.255");
            b.Note("ASA 静态 NAT 会自动放行入站返回；如需双向初始访问请结合 ACL。");
            return;
        }

        var insideI = string.IsNullOrWhiteSpace(s.Nat.InsideInterface) ? "" : s.Nat.InsideInterface.Trim();
        var outsideI = string.IsNullOrWhiteSpace(s.Nat.OutsideInterface) ? ExampleInterface(dt) : s.Nat.OutsideInterface.Trim();
        if (insideI.Length > 0)
        {
            b.Line($"interface {insideI}");
            using (b.Indent()) b.Line("ip nat inside");
            b.Line($"interface {outsideI}");
            using (b.Indent()) b.Line("ip nat outside");
        }
        foreach (var m in items)
            b.Line($"ip nat inside source static {m.InsideIp.Trim()} {m.GlobalIp.Trim()}");
        b.Note("公网 IP 需为设备可用的合法地址，且未与其他 NAT 配置冲突。");
    }

    // =============================== v6：DHCP 中继 ===============================
    protected override void GenDhcpRelays(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.DhcpRelays.Where(x => !string.IsNullOrWhiteSpace(x.Interface) && IsValidIp(x.ServerIp)).ToList();
        if (items.Count == 0) return;

        b.Section("DHCP 中继 (Helper-address)");
        using var _ = b.Indent();
        foreach (var r in items)
        {
            if (IsAsa(dt))
            {
                b.Note($"ASA 不支持 ios helper-address 直连，请使用 dhcprelay 或转发器，已跳过接口 {r.Interface.Trim()}。");
                continue;
            }
            b.Line($"interface {NormVlanIface(r.Interface)}");
            using (b.Indent()) b.Line($"ip helper-address {r.ServerIp.Trim()}");
            b.Blank();
        }
    }

    // =============================== v6：DHCP 静态绑定 ===============================
    protected override void GenDhcpBinds(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.DhcpBinds.Where(x => !string.IsNullOrWhiteSpace(x.Pool) && IsValidIp(x.Ip) && !string.IsNullOrWhiteSpace(x.Mac)).ToList();
        if (items.Count == 0) return;

        b.Section("DHCP 静态绑定");
        using var _ = b.Indent();
        if (IsAsa(dt))
        {
            b.Note("ASA 不支持 IOS 风格 DHCP 绑定，请使用 DHCP 服务器或外部方案，已跳过。");
            return;
        }
        foreach (var x in items)
        {
            b.Line($"ip dhcp pool {x.Pool.Trim()}");
            using (b.Indent())
            {
                b.Line($"host {x.Ip.Trim()} {x.Mask.Trim()}");
                b.Line($"hardware-address {DottedMac(x.Mac)} ethernet");
            }
            b.Blank();
        }
        b.Note("MAC 已归一为 aaaa.bbbb.cccc 格式；确认地址池不与自动分配范围重叠。");
    }

    // =============================== v6：IP-MAC 绑定 ===============================
    protected override void GenArpBinds(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.ArpBinds.Where(x => IsValidIp(x.Ip) && !string.IsNullOrWhiteSpace(x.Mac)).ToList();
        if (items.Count == 0) return;

        b.Section("IP-MAC 静态绑定 (ARP)");
        using var _ = b.Indent();
        if (IsAsa(dt))
        {
            foreach (var x in items)
            {
                var ifn = string.IsNullOrWhiteSpace(x.Interface) ? "inside" : x.Interface.Trim();
                b.Line($"arp {ifn} {x.Ip.Trim()} {DottedMac(x.Mac)}");
            }
            b.Note("ASA 的静态 ARP 为全局配置：arp <接口名if> <IP> <MAC>；未填接口默认 inside。");
            return;
        }
        foreach (var x in items)
        {
            if (!string.IsNullOrWhiteSpace(x.Interface))
            {
                b.Line($"interface {x.Interface.Trim()}");
                using (b.Indent()) b.Line($"arp {x.Ip.Trim()} {DottedMac(x.Mac)} ARPA");
            }
            else
            {
                b.Line($"arp {x.Ip.Trim()} {DottedMac(x.Mac)} ARPA");
                b.Note("IOS 建议把 ARP 绑定配置在三层接口视图下以获得更精确的限定。");
            }
        }
    }

    // =============================== v6：风暴抑制 ===============================
    protected override void GenStorms(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.Storms.Where(x => !string.IsNullOrWhiteSpace(x.Interface)).ToList();
        if (items.Count == 0) return;

        b.Section("风暴抑制 (Storm-control)");
        using var _ = b.Indent();
        foreach (var x in items)
        {
            b.Line($"interface {x.Interface.Trim()}");
            using (b.Indent())
            {
                if (double.TryParse(x.BroadcastPct, out var bp)) b.Line($"storm-control broadcast level {bp:0.##}");
                if (double.TryParse(x.MulticastPct, out var mp)) b.Line($"storm-control multicast level {mp:0.##}");
            }
            b.Blank();
        }
        b.Note("思科 IOS 对未知单播使用组播/单播 storm-control 的变体，请按型号核对。");
    }

    // =============================== v6：IGMP Snooping ===============================
    protected override void GenIgmp(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var cfg = s.Igmp;
        if (!cfg.Enabled) return;
        var vlans = ExpandVlanList(cfg.VlanIds);

        b.Section("IGMP Snooping");
        using var _ = b.Indent();
        b.Line("ip igmp snooping");
        foreach (var id in vlans)
            b.Line($"ip igmp snooping vlan {id}");
        b.Note("如需静态指定组播路由口，请在接口上配置 ip igmp snooping mrouter learn pim 或静态项。");
    }

    // =============================== v11：策略路由 PBR ===============================
    protected override void GenPbr(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        if (IsAsa(dt))
        {
            b.Section("策略路由 PBR");
            using (b.Indent())
                b.Note("ASA 不支持 IOS 的 route-map/ip policy route-map 策略路由；多出口选路请使用 policy-map 中的 set connection 或 PBR 替代方案（ASA 9.7+ 支持 policy-based routing via route map 有限）。");
            return;
        }
        var items = s.Pbrs.Where(p => !string.IsNullOrWhiteSpace(p.Name)).ToList();
        if (items.Count == 0) return;

        b.Section("策略路由 PBR");
        using var _ = b.Indent();
        var bound = new HashSet<string>();
        var seq = 10;
        foreach (var p in items)
        {
            if (!IsValidIp(p.NextHop) && string.IsNullOrWhiteSpace(p.ExitInterface)) continue;
            b.Line($"route-map {p.Name.Trim()} permit {seq}");
            using (b.Indent())
            {
                if (!string.IsNullOrWhiteSpace(p.MatchAcl)) b.Line($"match ip address {p.MatchAcl.Trim()}");
                if (!string.IsNullOrWhiteSpace(p.NextHop) && IsValidIp(p.NextHop))
                    b.Line($"set ip next-hop {p.NextHop.Trim()}");
                else
                    b.Line($"set interface {p.ExitInterface.Trim()}");
                if (!string.IsNullOrWhiteSpace(p.Description)) b.Line($"description {p.Description.Trim()}");
            }
            seq += 10;
        }
        b.Blank();
        b.Note("PBR 依赖 ACL 匹配（请在「ACL 策略」中定义同名 ACL），动作优先取下一跳。");
        foreach (var p in items)
        {
            var iface = p.ApplyOn.Trim();
            var key = $"{iface}|{p.Name.Trim()}";
            if (iface.Length == 0 || !bound.Add(key)) continue;
            b.Line($"interface {iface}");
            using (b.Indent()) b.Line($"ip policy route-map {p.Name.Trim()}");
        }
        b.Note("route-map 按序号顺序匹配；PBR 优先级高于路由表，未命中流量仍走正常路由。");
    }

    // =============================== v11：QoS 限速 ===============================
    protected override void GenQos(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.Qos.Where(q => !string.IsNullOrWhiteSpace(q.Interface) &&
                                     long.TryParse(q.CirKbps, out _) && long.Parse(q.CirKbps) > 0).ToList();
        if (items.Count == 0) return;

        b.Section("QoS 接口限速（CIR kbps）");
        using var qosScope = b.Indent();
        if (IsAsa(dt))
        {
            b.Note("ASA 接口限速建议用 policy-map + class-default 的 police（如 police input 2048000 conform-action transmit exceed-action drop），以下为 IOS 风格 CAR，仅供对照。");
        }
        foreach (var q in items)
        {
            b.Line($"interface {q.Interface.Trim()}");
            using (b.Indent())
            {
                var words = q.Direction.Trim().ToLowerInvariant() switch
                {
                    "inbound" => new[] { "input" },
                    "outbound" => new[] { "output" },
                    _ => new[] { "input", "output" }
                };
                var bps = long.Parse(q.CirKbps) * 1000;
                foreach (var dir in words)
                    b.Line($"rate-limit {dir} {bps} 8000 16000 conform-action transmit exceed-action drop");
                if (!string.IsNullOrWhiteSpace(q.Description)) b.Line($"description {q.Description.Trim()}");
            }
        }
        b.Note("rate-limit（CAR）为经典 IOS 限速；Catalyst 9000/新 IOS-XE 建议改用 policy-map 的 police，命令随型号核对。");
    }



    // =============================== v12：ACL 时间段 ===============================
    protected override void GenTimeRanges(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        if (IsAsa(dt))
        {
            b.Section("ACL 生效时间段 (time-range)");
            using (b.Indent())
                b.Note("ASA 不支持 IOS 的 time-range 语法；请拆分时段永久规则或按业务规划 ACL，再结合 ASA 对象组实现。");
            return;
        }
        var items = s.TimeRanges.Where(t => !string.IsNullOrWhiteSpace(t.Name)).ToList();
        if (items.Count == 0) return;

        b.Section("ACL 生效时间段 (time-range)");
        using var trScope = b.Indent();
        foreach (var t in items)
        {
            var period = t.Days.Trim().ToLowerInvariant() switch
            {
                "daily" => "daily",
                "off-day" => "weekend",
                _ => "weekdays"
            };
            b.Line($"time-range {t.Name.Trim()}");
            using (b.Indent())
                b.Line($"periodic {period} {t.StartTime.Trim()} to {t.EndTime.Trim()}");
            if (!string.IsNullOrWhiteSpace(t.Description)) b.Line($"description {t.Description.Trim()}");
        }
        b.Note("在「ACL 策略」规则的“时间段”列填该名称即可定时生效（编号/命名扩展 ACL 均可引用）。");
    }

    // =============================== v12：BFD ===============================
    protected override void GenBfd(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        if (IsAsa(dt))
        {
            b.Section("BFD 双向转发检测");
            using (b.Indent())
                b.Note("ASA 不支持 BFD；链路故障检测请使用 route 的 track + SLA 监测方案。");
            return;
        }
        var items = s.Bfds.Where(x => IsValidIp(x.PeerIp)).ToList();
        if (items.Count == 0) return;

        b.Section("BFD 双向转发检测");
        using var bfdScope = b.Indent();
        foreach (var x in items)
        {
            if (string.IsNullOrWhiteSpace(x.Interface))
            {
                b.Note($"{x.PeerIp.Trim()} 未填本端接口，跳过；BFD 需在三层接口下配置。");
                continue;
            }
            b.Line($"interface {x.Interface.Trim()}");
            using (b.Indent())
                b.Line($"bfd interval {x.MinTxMs.Trim()} min_rx {x.MinTxMs.Trim()} multiplier {x.Multiplier.Trim()}");
            if (!string.IsNullOrWhiteSpace(x.Description)) b.Note($"  说明：{x.Description.Trim()}");
        }
        b.Note("路由联动示例：OSPF 视图下 bfd all-interfaces；BGP 邻居下 neighbor X.X.X.X fall-over bfd；静态路由可用跟踪 track。");
    }

    // =============================== v12：PoE ===============================
    protected override void GenPoe(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.PoEs.Where(p => !string.IsNullOrWhiteSpace(p.Interface)).ToList();
        if (items.Count == 0) return;

        b.Section("PoE 供电");
        using var poeScope = b.Indent();
        b.Note("思科 PoE：power inline auto 自动供电、never 关闭；若需给关键设备预留功率可用 power inline static max N（按型号支持）。");
        foreach (var p in items)
        {
            b.Line($"interface {p.Interface.Trim()}");
            using (b.Indent())
            {
                b.Line(p.Enabled ? "power inline auto" : "power inline never");
                if (!string.IsNullOrWhiteSpace(p.Description)) b.Line($"description {p.Description.Trim()}");
            }
        }
    }

    // =============================== v13：IPv6 ===============================
    protected override void GenIpv6(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var v6 = s.Ipv6;
        if (!v6.Enabled || string.IsNullOrWhiteSpace(v6.Interface)) return;
        if (!v6.Address.Contains('/'))
        {
            b.Section("IPv6 基础配置");
            using var v6Skip = b.Indent();
            b.Note($"地址“{v6.Address.Trim()}”缺少前缀长度（如 2001:db8:10::1/64），已跳过；请补全后重新生成。");
            return;
        }

        // 思科没有 Vlanif 命名，SVI 为 Vlan<id>；用户填 Vlanif10 时自动转换
        var iface = v6.Interface.Trim();
        if (iface.StartsWith("Vlanif", StringComparison.OrdinalIgnoreCase))
            iface = "Vlan" + iface["Vlanif".Length..];

        b.Section("IPv6 基础配置");
        using var v6Scope = b.Indent();
        b.Line($"interface {iface}");
        using (b.Indent())
        {
            b.Line("ipv6 enable");
            b.Line($"ipv6 address {v6.Address.Trim().ToLowerInvariant()}");
            b.Line("no shutdown");
        }
        b.Note("思科 IOS 默认发送 RA（终端自动获取地址/网关）；如需关闭在接口视图 ipv6 nd suppress-ra。生产启用前确认上游已放通 ICMPv6/ND。");
    }

    // =============================== v13：GRE ===============================
    protected override void GenGre(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.Gres
            .Where(g => !string.IsNullOrWhiteSpace(g.Name) && IsValidIp(g.Source) && IsValidIp(g.Destination))
            .ToList();
        if (items.Count == 0) return;

        b.Section("GRE 隧道");
        using var greScope = b.Indent();
        foreach (var g in items)
        {
            var name = g.Name.Trim();
            // 思科隧道接口为 Tunnel0/1…，去掉华为式槽位写法
            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^Tunnel\d+/\d+/\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                name = "Tunnel" + System.Text.RegularExpressions.Regex.Match(name, @"\d+$").Value;

            b.Line($"interface {name}");
            using (b.Indent())
            {
                b.Line($"ip address {g.Address.Trim()} {g.Mask.Trim()}");
                b.Line($"tunnel source {g.Source.Trim()}");
                b.Line($"tunnel destination {g.Destination.Trim()}");
                b.Line("no shutdown");
                if (!string.IsNullOrWhiteSpace(g.Description)) b.Line($"description {g.Description.Trim()}");
            }
        }
        b.Note("GRE 不加密：敏感流量请叠加 IPsec profile 或改用 DMVPN/VTI；防火墙上放行 IP 协议 47，且隧道 MTU 建议调整（tunnel path-mtu-discovery 或 ip mtu 1400）。");
    }

    // =============================== v13：语音 VLAN ===============================
    protected override void GenVoice(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.Voices.Where(v => ExpandMembers(v.Ports).Any()).ToList();
        if (items.Count == 0) return;

        b.Section("语音 VLAN（话机与电脑共口）");
        using var vScope = b.Indent();
        foreach (var v in items)
        {
            var ports = ExpandMembers(v.Ports).ToList();
            var dv = v.DataVlan;
            var vv = v.VoiceVlan;
            b.Line($"vlan {dv}");
            b.Line(" exit");
            b.Line($"vlan {vv}");
            b.Line(" exit");
            var range = string.Join(",", PortRuns(ports, "list"));
            b.Line($"interface range {range}");
            using (b.Indent())
            {
                b.Line("switchport mode access");
                b.Line($"switchport access vlan {dv}");
                b.Line($"switchport voice vlan {vv}");
                b.Line("mls qos trust device cisco-phone");
                b.Line("spanning-tree portfast");
            }
            if (!string.IsNullOrWhiteSpace(v.Description)) b.Note($"说明：{v.Description.Trim()}");
        }
        b.Note("switchport voice vlan 需 CDP/LLDP 配合识别话机；部分老 IOS 需先 switchport trunk native/voice 或 power inline，请按型号核对。");
    }

    // =============================== v13：端口隔离 ===============================
    protected override void GenIsolate(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var iso = s.Isolate;
        if (!iso.Enabled) return;
        var ports = ExpandMembers(iso.Ports).ToList();
        if (ports.Count == 0) return;

        b.Section("端口隔离（protected）");
        using var isoScope = b.Indent();
        var range = string.Join(",", PortRuns(ports, "list"));
        b.Line($"interface range {range}");
        using (b.Indent())
        {
            b.Line("switchport protected");
        }
        b.Note("思科 protected 口之间二层不通、均可向上联口转发（全芯片全局生效，无组号概念）；思科部分平台需 switchport protected 在 access 模式下配置。");
    }

}
