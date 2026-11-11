using NetConfigPro.Models;
using static NetConfigPro.Generators.NetHelper;

namespace NetConfigPro.Generators;

/// <summary>锐捷 RGOS 平台配置生成器（RG-S 交换机 / RG-RSR 路由器 / RG-WALL 防火墙 / RG-AP）。</summary>
public sealed class RuijieGenerator : VendorGenerator
{
    public override Vendor Vendor => Vendor.Ruijie;
    protected override string Comment => "#";
    protected override string[] EnterCommands => ["enable", "configure terminal"];
    protected override string ExitCommand => "end";
    protected override string SaveCommand => "write";

    public override string ExampleInterface(DeviceType dt) => dt switch
    {
        DeviceType.Switch => "GigabitEthernet 0/1",
        DeviceType.Router => "GigabitEthernet 0/1",
        DeviceType.Firewall => "GigabitEthernet 0/1",
        DeviceType.AccessPoint => "GigabitEthernet 0/1",
        _ => "GigabitEthernet 0/1"
    };

    protected override void GenBasic(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var c = s.Basic;
        b.Section("基础配置");
        using var _ = b.Indent();

        b.Line($"hostname {c.Hostname}");

        if (!string.IsNullOrWhiteSpace(c.DomainName))
            b.Line($"ip domain-name {c.DomainName.Trim()}");

        var dns = (c.DnsServers ?? "").Split(new[] { ' ', ',', '，' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Where(IsValidIp).Select(x => x.Trim()).ToList();
        if (dns.Count > 0) b.Line($"ip name-server {string.Join(' ', dns)}");

        if (!string.IsNullOrWhiteSpace(c.TimeZone))
            b.Line($"clock timezone {c.TimeZone.Trim()} 8");

        if (IsValidIp(c.NtpServer))
            b.Line($"ntp server {c.NtpServer.Trim()}");

        if (IsValidIp(c.SyslogHost))
            b.Line($"logging server {c.SyslogHost.Trim()}");

        if (IsValidIp(c.MgmtIp))
        {
            b.Blank();
            if (dt is DeviceType.Switch or DeviceType.AccessPoint && c.MgmtVlanId > 0)
            {
                if (dt == DeviceType.Switch)
                {
                    b.Line($"vlan {c.MgmtVlanId}");
                    using (b.Indent()) b.Line("name Management");
                }
                b.Line($"interface VLAN {c.MgmtVlanId}");
                using (b.Indent()) b.Line($"ip address {c.MgmtIp.Trim()} {c.MgmtMask.Trim()}");
            }
            else
            {
                var ifName = string.IsNullOrWhiteSpace(c.LanInterface) ? ExampleInterface(dt) : c.LanInterface.Trim();
                b.Line($"interface {ifName}");
                using (b.Indent()) b.Line($"ip address {c.MgmtIp.Trim()} {c.MgmtMask.Trim()}");
            }

            if (IsValidIp(c.DefaultGateway))
                b.Line($"ip route 0.0.0.0 0.0.0.0 {c.DefaultGateway.Trim()}");
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
                // 1) 逻辑拨号口（RGOS：encapsulation ppp + 协商地址 + 绑定 dialer pool/group）
                b.Line("interface Dialer 1");
                using (b.Indent())
                {
                    b.Line("encapsulation ppp");
                    b.Line("ip address negotiate");
                    b.Line("dialer pool 1");
                    b.Line("dialer-group 1");
                    b.Line($"ppp chap hostname {c.PppoeUser}");
                    b.Line($"ppp chap password 0 {c.PppoePassword}");
                    b.Line($"ppp pap sent-username {c.PppoeUser} password 0 {c.PppoePassword}");
                    b.Note("RGOS 拨号建议配置 mtu 1488 与 ip tcp adjust-mss，可显著改善大包访问问题。");
                }
                // 2) 物理 WAN 口建立 PPPoE 会话（pppoe enable + pppoe-client 绑定拨号池）
                var phys = wan.Contains("Dialer", StringComparison.OrdinalIgnoreCase) ? "" : wan;
                if (phys.Length > 0)
                {
                    b.Line($"interface {phys}");
                    using (b.Indent())
                    {
                        b.Line("no ip address");
                        b.Line("pppoe enable");
                        b.Line("pppoe-client dial-pool-number 1 no-ddr");
                        b.Line("no shutdown");
                    }
                }
                else
                {
                    b.Note($"请在与运营商相连的物理口上执行 pppoe enable + pppoe-client dial-pool-number 1 no-ddr（WAN 接口名未填写物理口）。");
                }
                b.Line("ip route 0.0.0.0 0.0.0.0 Dialer 1");
                break;

            default:
                b.Line($"interface {wan}");
                using (b.Indent())
                {
                    b.Line($"ip address {c.WanIp.Trim()} {c.WanMask.Trim()}");
                    b.Line("no shutdown");
                }
                if (IsValidIp(c.WanGateway))
                    b.Line($"ip route 0.0.0.0 0.0.0.0 {c.WanGateway.Trim()}");
                break;
        }

        if (!string.IsNullOrWhiteSpace(c.LanInterface) && IsValidIp(c.LanIp))
        {
            b.Blank();
            b.Line($"interface {c.LanInterface.Trim()}");
            using (b.Indent()) b.Line($"ip address {c.LanIp.Trim()} {c.LanMask.Trim()}");
        }
    }

    protected override void GenVlans(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.Vlans.Where(v => v.Id > 0).ToList();
        if (items.Count == 0) return;

        b.Section("VLAN 配置");
        using var _ = b.Indent();
        if (IsRouter(dt) || IsFirewall(dt))
        {
            b.Note("锐捷路由器/防火墙不提供交换机式 VLAN 数据库；多网段请使用子接口承载（VLAN 由接入交换机划分）。");
            return;
        }

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
            b.Line($"interface VLAN {v.Id}");
            using (b.Indent()) b.Line($"ip address {v.IpAddress.Trim()} {v.Mask.Trim()}");
        }
    }

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

                switch (it.Mode)
                {
                    case InterfaceMode.Access:
                        if (dt == DeviceType.Switch)
                        {
                            b.Line("switchport mode access");
                            b.Line($"switchport access vlan {it.AccessVlan}");
                        }
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

                b.Line(it.Enabled ? "no shutdown" : "shutdown");
            }
            b.Blank();
        }
    }

    protected override void GenStp(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var stp = s.Stp;
        b.Section("生成树配置");
        using var _ = b.Indent();

        if (stp.Mode == StpMode.Mstp)
        {
            b.Line("spanning-tree mode mstp");
            b.Line("spanning-tree mst configuration");
            using (b.Indent())
            {
                b.Line($"name {stp.RegionName}");
                b.Line("revision 1");
                foreach (var map in (stp.InstanceMap ?? "").Split(new[] { ';', '，', ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = map.Split(':');
                    if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out var inst)) continue;
                    var vl = ExpandVlanList(parts[1]);
                    if (vl.Count > 0) b.Line($"instance {inst} vlan {ToCiscoRange(vl)}");
                }
            }
            b.Line("spanning-tree");
            var inst0 = stp.InstanceMap?.Split(':', 2)[0].Trim();
            var target = int.TryParse(inst0, out var i) ? i : 0;
            b.Line(stp.RootPrimary ? $"spanning-tree mst {target} priority 0" : $"spanning-tree mst {target} priority {stp.Priority}");
        }
        else
        {
            b.Line(stp.Mode == StpMode.Stp ? "spanning-tree mode stp" : "spanning-tree mode rstp");
            b.Line("spanning-tree");
            b.Line(stp.RootPrimary ? "spanning-tree priority 0" : $"spanning-tree priority {stp.Priority}");
        }
    }

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
                var line = $"ip route {sr.Prefix.Trim()} {sr.Mask.Trim()} {sr.NextHop.Trim()}";
                if (!string.IsNullOrWhiteSpace(sr.Preference)) line += $" {sr.Preference.Trim()}";
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
                if (r.DefaultRouteAdvertise) b.Line("default-information originate");
                if (r.RedistributeConnected) b.Line("redistribute connected");
                foreach (var area in s.OspfNetworks.Where(x => IsValidIp(x.Network)).GroupBy(x => x.Area))
                {
                    var type = area.First().AreaType;
                    if (type == "Stub") b.Line($"area {area.Key} stub");
                    else if (type == "NSSA") b.Line($"area {area.Key} nssa");
                    foreach (var n in area)
                        b.Line($"network {n.Network.Trim()} {n.Wildcard.Trim()} area {area.Key}");
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
            b.Blank();
            b.Line("router rip");
            using (b.Indent())
            {
                b.Line("version 2");
                foreach (var n in s.OspfNetworks.Where(x => IsValidIp(x.Network)))
                    b.Line($"network {n.Network.Trim()}");
                b.Line("no auto-summary");
            }
            b.Note("EIGRP 为思科私有协议，本平台动态路由按 RIPv2 输出。");
        }
    }

    protected override void GenAcl(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var groups = s.AclRules.Where(x => !string.IsNullOrWhiteSpace(x.Number))
                               .GroupBy(x => x.Number.Trim()).ToList();
        if (groups.Count == 0) return;

        b.Section("ACL 访问控制列表");
        using var _ = b.Indent();

        foreach (var g in groups)
        {
            var extended = g.Any(x => x.Kind == AclKind.Extended);
            b.Line($"ip access-list {(extended ? "extended" : "standard")} {g.Key}");
            var stdTimeRange = false;
            using (b.Indent())
            {
                // RGOS 按行号排序执行；remark 与规则使用不同序号段（5/15/25… 与 10/20/30…），避免同号规则互相覆盖
                var ruleSeq = 10;
                var remarkSeq = 5;
                foreach (var rl in g)
                {
                    if (!string.IsNullOrWhiteSpace(rl.Description))
                    {
                        b.Line($"{remarkSeq} remark {rl.Description.Trim()}");
                        remarkSeq += 10;
                    }
                    if (extended)
                    {
                        var proto = string.IsNullOrWhiteSpace(rl.Protocol) ? "ip" : rl.Protocol.Trim().ToLowerInvariant();
                        b.Line($"{ruleSeq} {rl.Action} {proto} {CiscoAddr(rl.Source, rl.SrcWildcard)} {CiscoAddr(rl.Destination, rl.DstWildcard)}" + TrSuffix(rl));
                    }
                    else
                    {
                        // 标准 ACL 不支持 time-range（同思科规则），填了不输出
                        if (string.IsNullOrWhiteSpace(rl.TimeRange))
                            b.Line($"{ruleSeq} {rl.Action} {CiscoAddr(rl.Source, rl.SrcWildcard)}");
                        else
                            stdTimeRange = true;
                    }
                    ruleSeq += 10;
                }
            }
            if (stdTimeRange)
                b.Note("标准 ACL 不支持 time-range（仅扩展 ACL 支持）：本组规则的“时间段”已忽略，如需定时生效请改用扩展 ACL。");
            b.Blank();
        }
    }

    protected override void GenNat(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var n = s.Nat;
        var outside = string.IsNullOrWhiteSpace(n.OutsideInterface) ? ExampleInterface(dt) : n.OutsideInterface.Trim();
        var inside = string.IsNullOrWhiteSpace(n.InsideInterface) ? "" : n.InsideInterface.Trim();

        b.Section("NAT 地址转换");
        using var _ = b.Indent();

        if (n.Kind == NatKind.EasyIp)
            b.Line($"ip nat inside source list {n.AclNumber} interface {outside} overload");
        else
            b.Line($"ip nat inside source static {n.Protocol.Trim().ToLowerInvariant()} {n.InsideIp.Trim()} {n.InsidePort.Trim()} {n.GlobalIp.Trim()} {n.GlobalPort.Trim()}");

        if (inside.Length > 0)
        {
            b.Line($"interface {inside}");
            using (b.Indent()) b.Line("ip nat inside");
        }
        b.Line($"interface {outside}");
        using (b.Indent()) b.Line("ip nat outside");
    }

    protected override void GenFirewall(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var zones = s.Zones.Where(z => !string.IsNullOrWhiteSpace(z.Name)).ToList();
        var policies = s.Policies.Where(p => !string.IsNullOrWhiteSpace(p.Name)).ToList();

        if (zones.Count > 0)
        {
            b.Section("安全区域");
            using var _ = b.Indent();
            foreach (var z in zones)
            {
                b.Line($"security-zone {z.Name.Trim()}");
                using (b.Indent())
                {
                    b.Line($"priority {z.Priority}");
                    foreach (var ifn in (z.Interfaces ?? "").Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries))
                        b.Line($"interface {ifn.Trim()}");
                }
            }
        }

        if (policies.Count > 0)
        {
            b.Section("安全策略");
            using var _ = b.Indent();
            b.Line("security-policy");
            using (b.Indent())
            {
                foreach (var p in policies)
                {
                    b.Line($"rule name {p.Name.Trim()}");
                    using (b.Indent())
                    {
                        if (!string.IsNullOrWhiteSpace(p.Description)) b.Line($"description {p.Description.Trim()}");
                        b.Line($"source-zone {p.SourceZone.Trim()}");
                        b.Line($"destination-zone {p.DestZone.Trim()}");
                        var src = ParseCidr(p.Source);
                        if (src != null) b.Line($"source-address {src.Value.Ip} {src.Value.Mask}");
                        var dst = ParseCidr(p.Destination);
                        if (dst != null) b.Line($"destination-address {dst.Value.Ip} {dst.Value.Mask}");
                        var svc = ServiceToPort(p.Service);
                        if (svc != "any") b.Line($"service {p.Service.Trim().ToLowerInvariant()}");
                        b.Line($"action {p.Action.Trim().ToLowerInvariant()}");
                    }
                    b.Blank();
                }
            }
        }
    }

    protected override void GenDhcp(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var pools = s.DhcpPools.Where(p => !string.IsNullOrWhiteSpace(p.Name) && IsValidIp(p.Network)).ToList();
        if (pools.Count == 0) return;

        b.Section("DHCP 服务");
        using var _ = b.Indent();
        b.Line("service dhcp");

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
                b.Line($"lease {Math.Max(0, p.LeaseDays)} 0 0");
            }
        }
    }

    protected override void GenWireless(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var w = s.Wireless;
        var radio = w.RadioBand == "5G" ? "Dot11radio 2/0" : "Dot11radio 1/0";

        b.Section("无线配置（FAT AP）");
        using var _ = b.Indent();

        b.Line("dot11 wlan 1");
        using (b.Indent())
        {
            b.Line($"ssid {w.Ssid}");
            b.Line($"vlan {w.VlanId}");
            if (w.Wpa == WpaMode.Wpa3Sae)
            {
                b.Line("security wpa3 enable");
                b.Line("security wpa3 akm sae enable");
                b.Line($"security wpa3 akm sae set-key ascii {w.Password}");
            }
            else
            {
                b.Line("security wpa2 enable");
                b.Line("security wpa2 ciphers aes enable");
                b.Line("security wpa2 akm psk enable");
                b.Line($"security wpa2 akm psk set-key ascii {w.Password}");
            }
            if (w.HideSsid) b.Line("ssid-hide");
        }

        b.Line($"interface {radio}");
        using (b.Indent())
        {
            b.Line("wlan-id 1");
            if (!string.IsNullOrWhiteSpace(w.Channel) && w.Channel.Trim().ToLowerInvariant() != "auto")
                b.Line($"channel {w.Channel.Trim()}");
            b.Line($"power local {w.Power}");
            if (w.MaxUsers > 0) b.Line($"max-sta-number {w.MaxUsers}");
            b.Line("no shutdown");
        }
    }

    protected override void GenSnmp(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var n = s.Snmp;
        b.Section("SNMP 配置");
        using var _ = b.Indent();

        if (n.Version.Trim().Equals("v3", StringComparison.OrdinalIgnoreCase))
        {
            b.Line("snmp-server group NETADMIN v3 priv read default write default");
            b.Line($"snmp-server user {n.V3User} NETADMIN v3 auth sha {n.V3AuthPassword} priv aes128 {n.V3PrivPassword}");
            if (IsValidIp(n.TrapHost)) b.Line($"snmp-server host {n.TrapHost.Trim()} traps version 3 priv {n.V3User}");
        }
        else
        {
            b.Line($"snmp-server community {n.Community} ro");
            if (IsValidIp(n.TrapHost))
                b.Line($"snmp-server host {n.TrapHost.Trim()} traps version {n.Version.Trim().ToLowerInvariant()} {n.Community}");
        }

        if (!string.IsNullOrWhiteSpace(n.Location)) b.Line($"snmp-server location {n.Location.Trim()}");
        if (!string.IsNullOrWhiteSpace(n.Contact)) b.Line($"snmp-server contact {n.Contact.Trim()}");
    }

    protected override void GenSsh(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        if (!s.Ssh.Enabled) return;
        var ssh = s.Ssh;
        b.Section("SSH 远程管理");
        using var _ = b.Indent();

        b.Line("enable service ssh-server");
        b.Line($"ip ssh version {ssh.Version}");
        b.Line($"ip ssh port {ssh.Port}");
        b.Line($"ip ssh time-out {ssh.AuthTimeout}");
        b.Line($"ip ssh authentication-retries {ssh.AuthRetries}");
        b.Line("crypto key generate rsa");

        b.Line($"username {s.Security.LocalUser} password {s.Security.Password}");
        b.Line($"username {s.Security.LocalUser} privilege {s.Security.Privilege}");
        b.Line("line vty 0 4");
        using (b.Indent())
        {
            b.Line("login local");
            b.Line("transport input ssh");
            b.Line($"exec-timeout {s.Security.IdleTimeout} 0");
        }
        b.Note("执行 crypto key generate rsa 时按提示输入密钥长度 2048。");
    }

    protected override void GenSecurity(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var sec = s.Security;
        b.Section("安全加固");
        using var _ = b.Indent();

        b.Line("service password-encryption");
        b.Line($"enable secret {sec.EnablePassword}");
        b.Line($"username {sec.LocalUser} privilege {sec.Privilege} password {sec.Password}");
        b.Line($"login block-for 300 attempts {sec.LoginRetryLimit} within 60");
        // Web 管理由「Web 管理」模块控制；未启用时保持服务关闭
        if (!s.WebAdmin.HttpEnabled && !s.WebAdmin.HttpsEnabled) b.Line("no enable service web-server");
        if (!s.Telnet.Enabled) b.Line("no enable service telnet-server");

        b.Line("line vty 0 4");
        using (b.Indent())
        {
            b.Line($"exec-timeout {sec.IdleTimeout} 0");
            b.Line("login local");
            b.Line("transport input ssh");
        }
    }

    // =============================== v2：链路聚合 ===============================
    protected override void GenLag(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.LinkAggs.Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Members)).ToList();
        if (items.Count == 0) return;

        b.Section("链路聚合 (AggregatePort)");
        using var _ = b.Indent();
        foreach (var it in items)
        {
            b.Line($"interface AggregatePort {it.Id}");
            using (b.Indent())
            {
                if (!string.IsNullOrWhiteSpace(it.Description)) b.Line($"description {it.Description.Trim()}");
            }
            var mode = it.Mode.Trim().ToLowerInvariant() == "lacp" ? "active" : "on";
            foreach (var m in ExpandMembers(it.Members))
            {
                b.Line($"interface {m}");
                using (b.Indent()) b.Line($"port-group {it.Id} mode {mode}");
            }
            b.Blank();
        }
        b.Note("RGOS 聚合命令在不同型号间有差异（如 lldp/description），请按实际版本核对。");
    }

    // =============================== v2：VRRP ===============================
    protected override void GenVrrp(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.Vrrps.Where(x => x.GroupId > 0 && IsValidIp(x.VirtualIp)).ToList();
        if (items.Count == 0) return;

        b.Section("VRRP 网关冗余");
        using var _ = b.Indent();
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
        b.Note("两台设备配置相同 VRRP 组与虚拟 IP；主备优先级不同并开启抢占。");
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
        foreach (var port in ExpandMembers(cfg.TrustPorts))
        {
            b.Line($"interface {port}");
            using (b.Indent()) b.Line("ip dhcp snooping trust");
        }
        b.Note("RGOS 部分版本需 ip dhcp snooping enable 全局开启，请按实际版本核对；Option82 默认启用。");
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
        b.Note("锐捷部分型号使用 monitor port / mirroring-group 语法，请按设备手册核对。");
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
                b.Line("port-security enable");
                b.Line($"port-security max-mac-count {Math.Max(1, it.MaxMac)}");
                if (it.Sticky) b.Line("port-security mac-address sticky");
                b.Note($"违规动作 '{it.Violation}' 请按版本映射 restrict/protect/shutdown 后手工补充。");
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
        b.Line("dot1x");
        if (!string.IsNullOrWhiteSpace(cfg.Interface))
        {
            b.Line($"interface {cfg.Interface.Trim()}");
            using (b.Indent()) b.Line($"dot1x port-control {cfg.PortControl.Trim().ToLowerInvariant()}");
        }
        b.Note("RGOS 802.1X 也可在接口视图执行 dot1x enable，请按实际版本核对。");
    }

    // =============================== v2：LLDP ===============================
    protected override void GenLldp(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        if (!s.Lldp.Enabled) return;
        b.Section("LLDP 链路发现");
        using var _ = b.Indent();
        b.Line("lldp run");
        b.Note("锐捷部分版本为 lldp enable，请按实际版本核对。");
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
            b.Line($"username {u.Name.Trim()} password {u.Password}");
            b.Line($"username {u.Name.Trim()} privilege {Math.Clamp(u.Privilege, 0, 15)}");
        }
        b.Note("RGOS 无法按账号细分接入方式；Telnet/Web/SSH 的启用与来源限制请在对应模块完成。");
    }

    // =============================== v3：Telnet ===============================
    protected override void GenTelnet(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        if (!s.Telnet.Enabled) return;
        b.Section("Telnet 接入");
        b.Line("enable service telnet-server");
        b.Line("line vty 0 4");
        using (b.Indent()) b.Line("login local");
        if (!string.IsNullOrWhiteSpace(s.Telnet.AclId))
            b.Note($"来源限制 '{s.Telnet.AclId.Trim()}'：RGOS 建议结合 ACL 与 line vty 实现，命令随版本差异，请核对。");
        b.Note("RGOS 部分版本 telnet 默认允许 vty 接入，若要停用请使用 no enable service telnet-server。");
    }

    // =============================== v3：Web 管理 ===============================
    protected override void GenWeb(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var w = s.WebAdmin;
        if (!w.HttpEnabled && !w.HttpsEnabled) return;
        b.Section("Web 管理");
        if (w.HttpEnabled) b.Line("enable service web-server");
        if (w.HttpsEnabled) b.Line("enable service https-server");
        if (int.TryParse(w.Port, out _)) b.Note($"端口 {w.Port.Trim()}：RGOS 修改 Web 端口命令随型号差异，请按设备手册补充。");
        if (!string.IsNullOrWhiteSpace(w.AllowSource))
            b.Note($"来源限制 '{w.AllowSource.Trim()}'：RGOS 通过访问控制列表实现，请按实际补充。");
        b.Note("Web 登录复用本地用户，请修改默认口令。");
    }

    // =============================== v6：端口映射 ===============================
    protected override void GenPortMaps(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.PortMaps.Where(x => IsValidIp(x.InsideIp) && x.ExternalPort.Trim().Length > 0 && x.ExternalPort.Trim().All(ch => ch >= '0' && ch <= '9')).ToList();
        if (items.Count == 0) return;

        b.Section("端口映射 (NAT Server)");
        using var _ = b.Indent();
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
        b.Note("RGOS 不同型号 NAT 命令存在差异（也支持 nat server 风格），请按实际版本核对。");
    }

    // =============================== v6：NAT 静态一对一 ===============================
    protected override void GenStaticNats(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.StaticNats.Where(x => IsValidIp(x.GlobalIp) && IsValidIp(x.InsideIp)).ToList();
        if (items.Count == 0) return;

        b.Section("NAT 静态一对一");
        using var _ = b.Indent();
        foreach (var m in items)
            b.Line($"ip nat inside source static {m.InsideIp.Trim()} {m.GlobalIp.Trim()}");
        b.Note("请确认内外接口已用 ip nat inside/outside 标记，来源公网 IP 未被其它映射占用。");
    }

    // =============================== v6：DHCP 中继 ===============================
    protected override void GenDhcpRelays(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.DhcpRelays.Where(x => !string.IsNullOrWhiteSpace(x.Interface) && IsValidIp(x.ServerIp)).ToList();
        if (items.Count == 0) return;

        b.Section("DHCP 中继");
        using var _ = b.Indent();
        foreach (var r in items)
        {
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
        b.Note("MAC 已归一为 aaaa.bbbb.cccc；确认与自动分配范围不重叠。");
    }

    // =============================== v6：IP-MAC 绑定 ===============================
    protected override void GenArpBinds(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.ArpBinds.Where(x => IsValidIp(x.Ip) && !string.IsNullOrWhiteSpace(x.Mac)).ToList();
        if (items.Count == 0) return;

        b.Section("IP-MAC 静态绑定 (ARP)");
        using var _ = b.Indent();
        foreach (var x in items)
        {
            var iface = string.IsNullOrWhiteSpace(x.Interface) ? "" : x.Interface.Trim();
            if (iface.Length > 0)
            {
                b.Line($"interface {iface}");
                using (b.Indent()) b.Line($"arp {x.Ip.Trim()} {DottedMac(x.Mac)} ARPA");
            }
            else
            {
                b.Line($"arp {x.Ip.Trim()} {DottedMac(x.Mac)} ARPA");
            }
        }
        b.Note("锐捷部分版本建议在对应三层接口视图配置 arp 绑定，请按型号核对。");
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
                if (double.TryParse(x.UnknownUcPct, out var up)) b.Line($"storm-control unicast level {up:0.##}");
            }
            b.Blank();
        }
        b.Note("RGOS 风暴抑制命令随版本差异较大，请按实际型号核对。");
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
        {
            b.Line($"vlan {id}");
            using (b.Indent()) b.Line("ip igmp snooping");
        }
        b.Note("部分 RGOS 版本需先开启全局组播与 igmp，请按型号核对。");
    }

    // =============================== v11：策略路由 PBR ===============================
    protected override void GenPbr(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
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
        b.Note("锐捷 RGOS 的 route-map 写法与思科一致；策略路由命中优先级高于普通路由表。");
    }

    // =============================== v11：QoS 限速 ===============================
    protected override void GenQos(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.Qos.Where(q => !string.IsNullOrWhiteSpace(q.Interface) &&
                                     long.TryParse(q.CirKbps, out _) && long.Parse(q.CirKbps) > 0).ToList();
        if (items.Count == 0) return;

        b.Section("QoS 接口限速（CIR kbps）");
        using var qosScope = b.Indent();
        b.Note("RGOS 限速命令随系列差异大（S 系列常用接口 rate-limit，部分型号用 traffic-shape），请按实际型号核对。");
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
        b.Note("rate-limit（CAR）为类思科写法；S5750 等型号可直接支持，老款 S 系列请核对接口下的速率限制命令。");
    }



    // =============================== v12：ACL 时间段 ===============================
    protected override void GenTimeRanges(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
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
            if (!string.IsNullOrWhiteSpace(t.Description)) b.Note($"  说明：{t.Description.Trim()}");
        }
        b.Note("在「ACL 策略」规则的“时间段”列填该名称即可定时生效；部分 RGOS 版本需确认 time-range 在编号 ACL 上的支持。");
    }

    // =============================== v12：BFD ===============================
    protected override void GenBfd(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.Bfds.Where(x => IsValidIp(x.PeerIp)).ToList();
        if (items.Count == 0) return;

        b.Section("BFD 双向转发检测");
        using var bfdScope = b.Indent();
        b.Note("RGOS 的 BFD 需相应功能授权，且不同系列接口下命令略有差异，请按型号核对。");
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
        b.Note("与路由联动（如 OSPF/BGP 下 bfd）的命令随版本差异大，请按型号补充；对端设备需同时开启。");
    }

    // =============================== v12：PoE ===============================
    protected override void GenPoe(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.PoEs.Where(p => !string.IsNullOrWhiteSpace(p.Interface)).ToList();
        if (items.Count == 0) return;

        b.Section("PoE 供电");
        using var poeScope = b.Indent();
        b.Note("锐捷 PoE 供电命令随系列差异较大（部分型号需全局开启 poe 后接口 poe enable），请按实际型号核对。");
        foreach (var p in items)
        {
            b.Line($"interface {p.Interface.Trim()}");
            using (b.Indent())
            {
                b.Line(p.Enabled ? "poe enable" : "poe disable");
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

        // 锐捷 SVI 为 Vlan<id>，与思科一致
        var iface = v6.Interface.Trim();
        if (iface.StartsWith("Vlanif", StringComparison.OrdinalIgnoreCase))
            iface = "Vlan" + iface["Vlanif".Length..];

        b.Section("IPv6 基础配置");
        using var v6Scope = b.Indent();
        b.Line("ipv6 enable");
        b.Line($"interface {iface}");
        using (b.Indent())
        {
            b.Line($"ipv6 address {v6.Address.Trim().ToLowerInvariant()}");
            b.Line("no shutdown");
        }
        b.Note("锐捷命令与思科同构；部分老版本需 ipv6 unicast-routing 类全局命令，请按版本核对。生产启用前确认上游已分配 IPv6 前缀。");
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
            // 锐捷隧道接口为 Tunnel0/1…，去掉华为式槽位写法
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
        b.Note("锐捷与思科同构；GRE 不加密，敏感流量请叠加 IPSec；防火墙上放行 IP 协议 47。");
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
            b.Line($"vlan {vv}");
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
        b.Note("锐捷与思科同构；部分型号 LLDP 识别话机命令为 lldp voice-vlan，请按版本核对。");
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
        b.Note("锐捷 protected 与思科同义：组内二层互不通、均可上联；上联口不要放进隔离范围。");
    }

}
