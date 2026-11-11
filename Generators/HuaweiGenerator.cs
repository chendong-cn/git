using NetConfigPro.Models;
using static NetConfigPro.Generators.NetHelper;

namespace NetConfigPro.Generators;

/// <summary>华为 VRP 平台配置生成器（S 系列交换机 / AR 路由器 / USG 防火墙 / AP）。</summary>
public sealed class HuaweiGenerator : VendorGenerator
{
    public override Vendor Vendor => Vendor.Huawei;
    protected override string Comment => "#";
    protected override string[] EnterCommands => ["system-view"];
    protected override string ExitCommand => "return";
    protected override string SaveCommand => "save";

    public override string ExampleInterface(DeviceType dt) => dt switch
    {
        DeviceType.Switch => "GigabitEthernet0/0/1",
        DeviceType.Router => "GigabitEthernet0/0/1",
        DeviceType.Firewall => "GigabitEthernet0/0/1",
        DeviceType.AccessPoint => "GigabitEthernet0/0/0",
        _ => "GigabitEthernet0/0/1"
    };

    // =============================== 基础配置 ===============================
    protected override void GenBasic(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var c = s.Basic;
        b.Section("基础配置");
        using var _ = b.Indent();

        b.Line($"sysname {c.Hostname}");

        if (!string.IsNullOrWhiteSpace(c.DomainName))
            b.Line($"dns domain {c.DomainName.Trim()}");

        foreach (var dns in (c.DnsServers ?? "").Split(new[] { ' ', ',', '，' }, StringSplitOptions.RemoveEmptyEntries))
            if (IsValidIp(dns)) b.Line($"dns server {dns.Trim()}");

        if (!string.IsNullOrWhiteSpace(c.TimeZone))
            b.Line($"clock timezone {c.TimeZone.Trim()} add 08:00:00");

        if (IsValidIp(c.NtpServer))
            b.Line($"ntp-service unicast-server {c.NtpServer.Trim()}");

        if (IsValidIp(c.SyslogHost))
        {
            b.Line("info-center enable");
            b.Line($"info-center loghost {c.SyslogHost.Trim()}");
        }

        // 管理地址：交换机走 VLANIF（创建管理 VLAN），AP 仅用内置 VLANIF（不创建 VLAN），路由器/防火墙直接用三层口
        if (IsValidIp(c.MgmtIp))
        {
            b.Blank();
            if (dt is DeviceType.Switch or DeviceType.AccessPoint && c.MgmtVlanId > 0)
            {
                if (dt == DeviceType.Switch)
                {
                    b.Line($"vlan {c.MgmtVlanId}");
                    using (b.Indent()) b.Line("description Management");
                }
                b.Line($"interface Vlanif{c.MgmtVlanId}");
                using (b.Indent()) b.Line($"ip address {c.MgmtIp.Trim()} {c.MgmtMask.Trim()}");
            }
            else
            {
                var ifName = string.IsNullOrWhiteSpace(c.LanInterface) ? ExampleInterface(dt) : c.LanInterface.Trim();
                b.Line($"interface {ifName}");
                using (b.Indent()) b.Line($"ip address {c.MgmtIp.Trim()} {c.MgmtMask.Trim()}");
            }

            if (IsValidIp(c.DefaultGateway))
                b.Line($"ip route-static 0.0.0.0 0.0.0.0 {c.DefaultGateway.Trim()}");
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
                using (b.Indent()) b.Line("ip address dhcp-alloc");
                break;

            case WanMode.Pppoe:
                b.Line("dialer-rule 1 ip permit");
                b.Line("interface Dialer1");
                using (b.Indent())
                {
                    b.Line("link-protocol ppp");
                    b.Line($"ppp chap user {c.PppoeUser}");
                    b.Line($"ppp chap password cipher {c.PppoePassword}");
                    b.Line("ip address ppp-negotiate");
                    b.Line($"dialer user {c.PppoeUser}");
                    b.Line("dialer bundle 1");
                    b.Line("dialer-group 1");
                }
                b.Line($"interface {wan}");
                using (b.Indent()) b.Line("pppoe-client dial-bundle-number 1");
                b.Line("ip route-static 0.0.0.0 0.0.0.0 Dialer1");
                break;

            default:
                b.Line($"interface {wan}");
                using (b.Indent()) b.Line($"ip address {c.WanIp.Trim()} {c.WanMask.Trim()}");
                if (IsValidIp(c.WanGateway))
                    b.Line($"ip route-static 0.0.0.0 0.0.0.0 {c.WanGateway.Trim()}");
                break;
        }

        if (!string.IsNullOrWhiteSpace(c.LanInterface) && IsValidIp(c.LanIp))
        {
            b.Blank();
            b.Line($"interface {c.LanInterface.Trim()}");
            using (b.Indent()) b.Line($"ip address {c.LanIp.Trim()} {c.LanMask.Trim()}");
        }
    }

    // =============================== VLAN ===============================
    protected override void GenVlans(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.Vlans.Where(v => v.Id > 0).ToList();
        if (items.Count == 0) return;

        b.Section("VLAN 配置");
        using var _ = b.Indent();
        if (IsRouter(dt))
            b.Note("仅带二层交换口/交换板的 AR 型号可创建 VLAN；纯三层接口请使用子接口（Vlanif 承载网关）或配合交换机使用。");

        foreach (var v in items)
        {
            b.Line($"vlan {v.Id}");
            using (b.Indent())
            {
                var desc = string.IsNullOrWhiteSpace(v.Name) ? v.Description : v.Name;
                if (!string.IsNullOrWhiteSpace(desc)) b.Line($"description {desc.Trim()}");
            }
        }

        foreach (var v in items.Where(x => x.L3Enabled && IsValidIp(x.IpAddress)))
        {
            b.Line($"interface Vlanif{v.Id}");
            using (b.Indent()) b.Line($"ip address {v.IpAddress.Trim()} {v.Mask.Trim()}");
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

                switch (it.Mode)
                {
                    case InterfaceMode.Access:
                        // 防火墙/USG 接口为三层安全区域成员，不执行二层划 VLAN 命令
                        if (!IsRouter(dt) && !IsFirewall(dt))
                        {
                            b.Line("port link-type access");
                            b.Line($"port default vlan {it.AccessVlan}");
                        }
                        break;

                    case InterfaceMode.Trunk:
                        if (!IsRouter(dt) && !IsFirewall(dt))
                        {
                            b.Line("port link-type trunk");
                            var vl = ExpandVlanList(it.TrunkVlans);
                            b.Line(vl.Count == 0 ? "port trunk allow-pass vlan all" : $"port trunk allow-pass vlan {ToHuaweiRange(vl)}");
                        }
                        break;

                    case InterfaceMode.Routed:
                        if (IsSwitch(dt)) b.Line("undo portswitch");
                        if (IsValidIp(it.IpAddress)) b.Line($"ip address {it.IpAddress.Trim()} {it.Mask.Trim()}");
                        break;
                }

                b.Line(it.Enabled ? "undo shutdown" : "shutdown");
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

        var mode = stp.Mode switch
        {
            StpMode.Stp => "stp",
            StpMode.Rstp => "rstp",
            _ => "mstp"
        };
        b.Line($"stp mode {mode}");

        if (stp.Mode == StpMode.Mstp)
        {
            b.Line("stp region-configuration");
            using (b.Indent())
            {
                b.Line($"region-name {stp.RegionName}");
                foreach (var map in (stp.InstanceMap ?? "").Split(new[] { ';', '，', ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = map.Split(':');
                    if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out var inst)) continue;
                    var vl = ExpandVlanList(parts[1]);
                    if (vl.Count > 0) b.Line($"instance {inst} vlan {ToHuaweiRange(vl)}");
                }
                b.Line("active region-configuration");
            }
            var inst0 = stp.InstanceMap?.Split(':', 2)[0].Trim();
            var target = int.TryParse(inst0, out var i) ? i : 0;
            b.Line(stp.RootPrimary ? $"stp instance {target} root primary" : $"stp instance {target} priority {stp.Priority}");
        }
        else
        {
            b.Line(stp.RootPrimary ? "stp root primary" : $"stp priority {stp.Priority}");
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
                var line = $"ip route-static {sr.Prefix.Trim()} {sr.Mask.Trim()} {sr.NextHop.Trim()}";
                if (!string.IsNullOrWhiteSpace(sr.Preference)) line += $" preference {sr.Preference.Trim()}";
                if (!string.IsNullOrWhiteSpace(sr.Description)) line += $" description {sr.Description.Trim()}";
                b.Line(line);
            }
        }

        if (on.Contains(Mod.OspfR))
        {
            b.Blank();
            b.Line($"ospf {r.OspfProcess} router-id {r.RouterId}");
            using (b.Indent())
            {
                if (r.RedistributeConnected) b.Line("import-route direct");
                if (r.DefaultRouteAdvertise) b.Line("default-route-advertise");
                foreach (var area in s.OspfNetworks.Where(x => IsValidIp(x.Network)).GroupBy(x => x.Area))
                {
                    b.Line($"area {area.Key}");
                    using (b.Indent())
                    {
                        var type = area.First().AreaType;
                        if (type == "Stub") b.Line("stub");
                        else if (type == "NSSA") b.Line("nssa");
                        foreach (var n in area)
                            b.Line($"network {n.Network.Trim()} {n.Wildcard.Trim()}");
                    }
                }
            }
        }

        if (on.Contains(Mod.BgpR))
        {
            b.Blank();
            b.Line($"bgp {r.BgpAs}");
            using (b.Indent())
            {
                b.Line($"router-id {r.RouterId}");
                foreach (var n in s.BgpNeighbors.Where(x => IsValidIp(x.PeerIp)))
                {
                    b.Line($"peer {n.PeerIp.Trim()} as-number {n.RemoteAs}");
                    if (!string.IsNullOrWhiteSpace(n.Description))
                        b.Line($"peer {n.PeerIp.Trim()} description {n.Description.Trim()}");
                }
                b.Line("ipv4-family unicast");
                using (b.Indent())
                {
                    if (r.RedistributeConnected) b.Line("import-route direct");
                    foreach (var sr in s.StaticRoutes.Where(x => IsValidIp(x.Prefix)))
                    {
                        var net = ParseCidr($"{sr.Prefix} {sr.Mask}");
                        if (net != null) b.Line($"network {net.Value.Ip} {net.Value.Mask}");
                    }
                }
            }
        }

        if (on.Contains(Mod.DynR))
        {
            b.Blank();
            b.Line("rip 1");
            using (b.Indent())
            {
                b.Line("version 2");
                b.Line("undo summary");
                foreach (var n in s.OspfNetworks.Where(x => IsValidIp(x.Network)))
                    b.Line($"network {n.Network.Trim()}");
            }
            b.Note("EIGRP 为思科私有协议，本平台动态路由按 RIPv2 输出。");
        }
    }

    // =============================== ACL ===============================
    protected override void GenAcl(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var groups = s.AclRules.Where(x => !string.IsNullOrWhiteSpace(x.Number))
                               .GroupBy(x => x.Number.Trim()).ToList();
        if (groups.Count == 0) return;

        b.Section("ACL 访问控制列表");
        using var _ = b.Indent();

        foreach (var g in groups)
        {
            var extended = g.Any(x => x.Kind == AclKind.Extended) || int.TryParse(g.Key, out var num) && num >= 3000;
            b.Line($"acl {g.Key}");
            using (b.Indent())
            {
                var seq = 5;
                foreach (var rl in g)
                {
                    if (extended)
                    {
                        var proto = string.IsNullOrWhiteSpace(rl.Protocol) ? "ip" : rl.Protocol.Trim().ToLowerInvariant();
                        b.Line($"rule {seq} {rl.Action} {proto} source {HwAddr(rl.Source, rl.SrcWildcard)} destination {HwAddr(rl.Destination, rl.DstWildcard)}" + TrSuffix(rl));
                    }
                    else
                    {
                        b.Line($"rule {seq} {rl.Action} source {HwAddr(rl.Source, rl.SrcWildcard)}" + TrSuffix(rl));
                    }
                    seq += 5;
                }
            }
            b.Blank();
        }
        b.Note("华为 ACL 规则不支持逐条描述，规则说明请见生成工具中的备注。");
    }

    // =============================== NAT ===============================
    protected override void GenNat(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var n = s.Nat;
        var outside = string.IsNullOrWhiteSpace(n.OutsideInterface) ? ExampleInterface(dt) : n.OutsideInterface.Trim();

        b.Section("NAT 地址转换");
        using var _ = b.Indent();

        b.Line($"interface {outside}");
        using (b.Indent())
        {
            if (n.Kind == NatKind.EasyIp)
            {
                b.Line($"nat outbound {n.AclNumber}");
            }
            else
            {
                var proto = (n.Protocol ?? "tcp").Trim().ToLowerInvariant();
                b.Line($"nat server protocol {proto} global {n.GlobalIp.Trim()} {n.GlobalPort.Trim()} inside {n.InsideIp.Trim()} {n.InsidePort.Trim()}");
            }
        }
        b.Note("Easy IP 需先在 ACL 模块中配置允许上网的内网网段，并引用对应 ACL 编号。");
    }

    // =============================== 防火墙 ===============================
    protected override void GenFirewall(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var zones = s.Zones.Where(z => !string.IsNullOrWhiteSpace(z.Name)).ToList();
        var policies = s.Policies.Where(p => !string.IsNullOrWhiteSpace(p.Name)).ToList();
        if (zones.Count == 0 && policies.Count == 0) return;

        if (zones.Count > 0)
        {
            b.Section("安全区域");
            using var _ = b.Indent();
            foreach (var z in zones)
            {
                b.Line($"firewall zone {z.Name.Trim().ToLowerInvariant()}");
                using (b.Indent())
                {
                    b.Line($"set priority {z.Priority}");
                    foreach (var ifn in (z.Interfaces ?? "").Split(new[] { ',', '，', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                        b.Line($"add interface {ifn.Trim()}");
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
                        b.Line($"source-zone {p.SourceZone.Trim().ToLowerInvariant()}");
                        b.Line($"destination-zone {p.DestZone.Trim().ToLowerInvariant()}");
                        var src = ParseCidr(p.Source);
                        if (src != null) b.Line($"source-address {src.Value.Ip} mask {src.Value.Mask}");
                        var dst = ParseCidr(p.Destination);
                        if (dst != null) b.Line($"destination-address {dst.Value.Ip} mask {dst.Value.Mask}");
                        var svc = ServiceToPort(p.Service);
                        if (svc != "any") b.Line($"service {p.Service.Trim().ToLowerInvariant()}");
                        b.Line($"action {p.Action.Trim().ToLowerInvariant()}");
                    }
                    b.Blank();
                }
            }
        }
    }

    // =============================== DHCP ===============================
    protected override void GenDhcp(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var pools = s.DhcpPools.Where(p => !string.IsNullOrWhiteSpace(p.Name) && IsValidIp(p.Network)).ToList();
        if (pools.Count == 0) return;

        b.Section("DHCP 服务");
        using var _ = b.Indent();
        b.Line("dhcp enable");

        foreach (var p in pools)
        {
            b.Blank();
            b.Line($"ip pool {p.Name.Trim()}");
            using (b.Indent())
            {
                b.Line($"gateway-list {p.Gateway.Trim()}");
                b.Line($"network {p.Network.Trim()} mask {p.Mask.Trim()}");
                var dns = new[] { p.Dns1, p.Dns2 }.Where(IsValidIp).Select(x => x!.Trim());
                if (dns.Any()) b.Line($"dns-list {string.Join(' ', dns)}");
                if (IsValidIp(p.ExcludeFrom) && IsValidIp(p.ExcludeTo))
                    b.Line($"excluded-ip-address {p.ExcludeFrom.Trim()} {p.ExcludeTo.Trim()}");
                b.Line($"lease day {Math.Max(0, p.LeaseDays)}");
            }

            if (p.VlanId > 0)
            {
                b.Line($"interface Vlanif{p.VlanId}");
                using (b.Indent()) b.Line("dhcp select global");
            }
            else
            {
                b.Note($"地址池 {p.Name.Trim()} 未绑定接口（VLAN 留空）：请在实际网关三层口手工执行 dhcp select global，否则客户端拿不到地址。");
            }
        }
    }

    // =============================== 无线 ===============================
    protected override void GenWireless(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var w = s.Wireless;
        b.Section("无线配置（FAT AP）");
        using var _ = b.Indent();

        b.Note("ap-mode fat 在用户视图下执行，切换后需重启 AP 生效：");
        b.Line("ap-mode fat");
        b.Blank();

        b.Line("ssid-profile name wlan-ssid");
        using (b.Indent())
        {
            b.Line($"ssid {w.Ssid}");
            if (w.HideSsid) b.Line("ssid-hide enable");
        }

        b.Line("security-profile name wlan-sec");
        using (b.Indent())
        {
            if (w.Wpa == WpaMode.Wpa3Sae) b.Line($"security wpa3 sae pass-phrase cipher {w.Password} aes");
            else b.Line($"security wpa2 psk pass-phrase cipher {w.Password} aes");
            if (w.Wpa == WpaMode.Wpa2Wpa3) b.Note("WPA2/WPA3 混合模式请在 Web 界面启用 transition 开关。");
        }

        b.Line("vap-profile name wlan-vap");
        using (b.Indent())
        {
            b.Line("ssid-profile wlan-ssid");
            b.Line("security-profile wlan-sec");
            b.Line($"service-vlan vlan-id {w.VlanId}");
            b.Line("forward-mode direct-forward");
            if (w.MaxUsers > 0) b.Line($"max-sta-number {w.MaxUsers}");
        }

        b.Line("ap-group name default");
        using (b.Indent())
        {
            b.Line("vap-profile wlan-vap wlan 1 radio 0");
            b.Line("vap-profile wlan-vap wlan 1 radio 1");
        }

        var radio = w.RadioBand == "5G" ? "Wlan-Radio 0/0/1" : "Wlan-Radio 0/0/0";
        b.Line($"interface {radio}");
        using (b.Indent())
        {
            if (!string.IsNullOrWhiteSpace(w.Channel) && w.Channel.Trim().ToLowerInvariant() != "auto")
                b.Line($"channel 20mhz {w.Channel.Trim()}");
            b.Line($"eirp {Math.Clamp(w.Power / 5, 1, 27)}");
            b.Line("undo shutdown");
        }
    }

    // =============================== SNMP ===============================
    protected override void GenSnmp(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var n = s.Snmp;
        b.Section("SNMP 配置");
        using var _ = b.Indent();

        b.Line("snmp-agent");
        b.Line($"snmp-agent sys-info version {n.Version.Trim().ToLowerInvariant()}");

        if (n.Version.Trim().Equals("v3", StringComparison.OrdinalIgnoreCase))
        {
            b.Line("snmp-agent group v3 netadmin privacy read-view iso write-view iso notify-view iso");
            b.Line($"snmp-agent usm-user v3 {n.V3User} netadmin authentication-mode sha cipher {n.V3AuthPassword} privacy-mode aes128 cipher {n.V3PrivPassword}");
        }
        else
        {
            b.Line($"snmp-agent community read cipher {n.Community}");
        }

        if (!string.IsNullOrWhiteSpace(n.Location)) b.Line($"snmp-agent sys-info location {n.Location.Trim()}");
        if (!string.IsNullOrWhiteSpace(n.Contact)) b.Line($"snmp-agent sys-info contact {n.Contact.Trim()}");
        if (IsValidIp(n.TrapHost))
        {
            var isV3 = n.Version.Trim().Equals("v3", StringComparison.OrdinalIgnoreCase);
            // v3 的 securityname 必须是 USM 用户名（非团体字）；v2c 才用 community
            var sec = isV3 ? $"{n.V3User} v3 privacy" : $"{n.Community} v2c";
            b.Line($"snmp-agent target-host trap-hostname NMS address {n.TrapHost.Trim()} udp-port 162 params securityname {sec}");
        }
    }

    // =============================== SSH ===============================
    protected override void GenSsh(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        if (!s.Ssh.Enabled) return;
        var ssh = s.Ssh;
        b.Section("SSH 远程管理");
        using var _ = b.Indent();

        b.Line("stelnet server enable");
        b.Line($"ssh server port {ssh.Port}");
        b.Line($"ssh server timeout {ssh.AuthTimeout}");
        b.Line($"ssh server authentication-retries {ssh.AuthRetries}");
        b.Line("rsa local-key-pair create");
        b.Note("执行 rsa local-key-pair create 时按提示输入密钥长度 2048。");
    }

    // =============================== 安全加固 ===============================
    protected override void GenSecurity(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var sec = s.Security;
        b.Section("安全加固");
        using var _ = b.Indent();

        b.Line("aaa");
        using (b.Indent())
        {
            b.Line($"local-user {sec.LocalUser} password cipher {sec.Password}");
            b.Line($"local-user {sec.LocalUser} privilege level {sec.Privilege}");
            b.Line($"local-user {sec.LocalUser} service-type ssh terminal http");
            b.Line($"local-user {sec.LocalUser} state active");
        }

        b.Line("user-interface vty 0 4");
        using (b.Indent())
        {
            b.Line("authentication-mode aaa");
            b.Line("protocol inbound ssh");
            b.Line($"idle-timeout {sec.IdleTimeout} 0");
            b.Line("screen-length 0");
        }

        if (!s.Telnet.Enabled) b.Line("undo telnet server enable");
        b.Note("华为平台登录锁定与防攻击命令随软件版本差异较大（如 local-aaa-user wrong-password），请按设备型号手工补充。");
    }

    // =============================== v2：链路聚合 ===============================
    protected override void GenLag(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.LinkAggs.Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Members)).ToList();
        if (items.Count == 0) return;

        b.Section("链路聚合 (Eth-Trunk)");
        using var _ = b.Indent();
        foreach (var it in items)
        {
            b.Line($"interface Eth-Trunk{it.Id}");
            using (b.Indent())
            {
                if (!string.IsNullOrWhiteSpace(it.Description)) b.Line($"description {it.Description.Trim()}");
                if (it.Mode.Trim().ToLowerInvariant() == "lacp") b.Line("mode lacp-static");
                foreach (var m in ExpandMembers(it.Members)) b.Line($"trunkport {m}");
            }
            b.Blank();
        }
        b.Note("若聚合口需承载业务 VLAN，请在「接口配置」中再为该 Eth-Trunk 添加 Trunk 透传配置。");
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
            b.Line($"interface {it.Interface.Trim()}");
            using (b.Indent())
            {
                b.Line($"vrrp vrid {it.GroupId} virtual-ip {it.VirtualIp.Trim()}");
                b.Line($"vrrp vrid {it.GroupId} priority {it.Priority}");
                if (!it.Preempt) b.Line($"vrrp vrid {it.GroupId} preempt-mode disable");
                if (!string.IsNullOrWhiteSpace(it.Description)) b.Line($"vrrp vrid {it.GroupId} description {it.Description.Trim()}");
            }
            b.Blank();
        }
        b.Note("主备两台设备配置相同的 VRID 与虚拟 IP；请确认该接口已有三层地址（VLANIF 或物理口）。");
    }

    // =============================== v2：DHCP Snooping ===============================
    protected override void GenDhcpSnoop(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var cfg = s.DhcpSnoop;
        var vlans = ExpandVlanList(cfg.VlanIds);
        if (vlans.Count == 0) return;

        b.Section("DHCP Snooping");
        using var _ = b.Indent();
        b.Line("dhcp snooping enable");
        b.Line($"dhcp snooping enable vlan {ToHuaweiRange(vlans)}");
        if (cfg.Option82) b.Line("dhcp snooping information enable");
        foreach (var port in ExpandMembers(cfg.TrustPorts))
        {
            b.Line($"interface {port}");
            using (b.Indent()) b.Line("dhcp snooping trusted");
        }
        b.Note("仅上行口（连接 DHCP 服务器方向）设为 trusted，其余口保持非信任以拦截私接 DHCP。");
    }

    // =============================== v2：端口镜像 ===============================
    protected override void GenMirror(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.Mirrors.Where(x => x.SessionId > 0 && !string.IsNullOrWhiteSpace(x.DestPort)).ToList();
        if (items.Count == 0) return;

        b.Section("端口镜像 (Observe-port)");
        using var _ = b.Indent();
        foreach (var it in items)
        {
            b.Line($"mirroring-group {it.SessionId} local");
            foreach (var src in ExpandMembers(it.SourcePorts))
                b.Line($"mirroring-group {it.SessionId} mirroring-port {src} {DirWord(it.Direction)}");
            b.Line($"mirroring-group {it.SessionId} monitor-port {it.DestPort.Trim()}");
            b.Blank();
        }
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
                b.Line($"port-security max-mac-num {Math.Max(1, it.MaxMac)}");
                if (it.Sticky) b.Line("port-security mac-address sticky");
                b.Line($"port-security protect-action {it.Violation.Trim().ToLowerInvariant()}");
            }
            b.Blank();
        }
        b.Note("华为端口安全 protect-action 支持 protect / restrict / shutdown，不同版本命令略有差异。");
    }

    // =============================== v2：802.1X ===============================
    protected override void GenDot1x(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var cfg = s.Dot1x;
        if (!cfg.Enabled || !IsValidIp(cfg.ServerIp)) return;

        b.Section("802.1X 接入认证");
        using var _ = b.Indent();

        b.Line("radius-server template radius1");
        using (b.Indent())
        {
            b.Line($"radius-server shared-key cipher {cfg.SharedKey}");
            b.Line($"radius-server authentication {cfg.ServerIp.Trim()} {cfg.AuthPort.Trim()}");
            b.Line($"radius-server accounting {cfg.ServerIp.Trim()} {cfg.AcctPort.Trim()}");
        }

        b.Line("aaa");
        using (b.Indent())
        {
            b.Line("authentication-scheme dot1x-auth");
            using (b.Indent()) b.Line("authentication-mode radius");
            b.Line("accounting-scheme dot1x-acct");
            using (b.Indent()) b.Line("accounting-mode radius");
            b.Line("domain default");
            using (b.Indent())
            {
                b.Line("authentication-scheme dot1x-auth");
                b.Line("accounting-scheme dot1x-acct");
                b.Line("radius-server radius1");
            }
        }

        b.Line("dot1x enable");
        if (!string.IsNullOrWhiteSpace(cfg.Interface))
        {
            b.Line($"interface {cfg.Interface.Trim()}");
            using (b.Indent())
            {
                b.Line("dot1x enable");
                b.Line($"dot1x port-control {cfg.PortControl.Trim().ToLowerInvariant()}");
            }
        }
        b.Note("华为 802.1X 也可在 RADIUS 模板视图下用 accounting-mode 关闭计费；命令细节随 VRP 版本变化，请核对。");
    }

    // =============================== v2：LLDP ===============================
    protected override void GenLldp(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        if (!s.Lldp.Enabled) return;
        b.Section("LLDP 链路发现");
        using var _ = b.Indent();
        b.Line("lldp enable");
        b.Note("默认收发均开启；如需调整发送间隔/保持倍数，请在接口视图使用 lldp timer 相关命令。");
    }

    private static string DirWord(string dir) => dir.Trim().ToLowerInvariant() switch
    {
        "inbound" => "inbound",
        "outbound" => "outbound",
        _ => "both"
    };

    // =============================== v3：本地用户 ===============================
    protected override void GenLocalUsers(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var users = s.LocalUsers.Where(u => !string.IsNullOrWhiteSpace(u.Name)).ToList();
        if (users.Count == 0) return;

        b.Section("本地用户账号");
        b.Line("aaa"); // 华为 VRP 的 local-user 命令须在 AAA 视图下配置
        using (b.Indent())
        {
            foreach (var u in users)
            {
                b.Line($"local-user {u.Name.Trim()} password cipher {u.Password}");
                b.Line($"local-user {u.Name.Trim()} privilege level {Math.Clamp(u.Privilege, 0, 15)}");
                b.Line($"local-user {u.Name.Trim()} service-type {FilterTokens(u.Services, "ssh terminal", "ssh", "telnet", "terminal", "ftp", "http")}");
                b.Line(u.Active ? $"local-user {u.Name.Trim()} state active" : $"local-user {u.Name.Trim()} state block");
                b.Blank();
            }
        }
        b.Note("VTY 登录需配合 aaa 认证（见「安全加固」）；cipher 仅作展示参考，正式使用建议 irreversible-cipher。");
    }

    // =============================== v3：Telnet ===============================
    protected override void GenTelnet(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        if (!s.Telnet.Enabled) return;
        b.Section("Telnet 接入");
        b.Line("telnet server enable");
        b.Line("user-interface vty 0 4");
        using (b.Indent())
        {
            b.Line("protocol inbound all");
            if (!string.IsNullOrWhiteSpace(s.Telnet.AclId))
                b.Line($"acl {s.Telnet.AclId.Trim()} inbound");
        }
        b.Note("protocol inbound all 允许 SSH 与 Telnet 并存；如仅 Telnet 可改为 protocol inbound telnet。");
    }

    // =============================== v3：Web 管理 ===============================
    protected override void GenWeb(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var w = s.WebAdmin;
        if (!w.HttpEnabled && !w.HttpsEnabled) return;
        b.Section("Web 管理");
        if (w.HttpEnabled)
        {
            b.Line("http server enable");
            if (int.TryParse(w.Port, out _)) b.Line($"http server port {w.Port.Trim()}");
        }
        if (w.HttpsEnabled)
        {
            b.Line("http secure-server enable");
            b.Note("HTTPS 需先在设备上配置 ssl-policy 与证书，不同型号命令差异较大。");
        }
        if (!string.IsNullOrWhiteSpace(w.AllowSource))
            b.Note($"来源限制 '{w.AllowSource.Trim()}'：不同型号通过 http acl / 安全策略实现，请按设备手册补充。");
        b.Note("Web 登录复用「本地用户/安全加固」中的账号，请务必修改默认口令。");
    }

    // =============================== v6：端口映射 ===============================
    protected override void GenPortMaps(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.PortMaps.Where(x => IsValidIp(x.InsideIp) && x.ExternalPort.Trim().Length > 0 && x.ExternalPort.Trim().All(ch => ch >= '0' && ch <= '9')).ToList();
        if (items.Count == 0) return;

        b.Section("端口映射 (NAT Server)");
        var outside = string.IsNullOrWhiteSpace(s.Nat.OutsideInterface) ? ExampleInterface(dt) : s.Nat.OutsideInterface.Trim();
        b.Line($"interface {outside}");
        using var _ = b.Indent();
        foreach (var m in items)
        {
            var proto = (m.Protocol ?? "tcp").Trim().ToLowerInvariant();
            var g = IsValidIp(m.ExternalIp) ? m.ExternalIp.Trim() + " " : "";
            b.Line($"nat server protocol {proto} global {g}{m.ExternalPort.Trim()} inside {m.InsideIp.Trim()} {m.InsidePort.Trim()}");
        }
        b.Note("同一公网端口只能映射一台内网服务器；如需多台请使用不同 ExternalPort。");
    }

    // =============================== v6：NAT 静态一对一 ===============================
    protected override void GenStaticNats(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.StaticNats.Where(x => IsValidIp(x.GlobalIp) && IsValidIp(x.InsideIp)).ToList();
        if (items.Count == 0) return;

        b.Section("NAT 静态一对一");
        var outside = string.IsNullOrWhiteSpace(s.Nat.OutsideInterface) ? ExampleInterface(dt) : s.Nat.OutsideInterface.Trim();
        b.Line($"interface {outside}");
        using var _ = b.Indent();
        foreach (var m in items)
            b.Line($"nat static global {m.GlobalIp.Trim()} inside {m.InsideIp.Trim()}");
        b.Note("一对一 NAT 通常还需在安全策略中放行相应流量，请一并核对。");
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
            b.Line($"interface {r.Interface.Trim()}");
            using (b.Indent())
            {
                b.Line("dhcp select relay");
                b.Line($"dhcp relay server-ip {r.ServerIp.Trim()}");
            }
            b.Blank();
        }
        b.Note("DHCP 服务器须与设备三层互通；部分 VRP 版本需先在系统视图执行 dhcp enable。");
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
            b.Line($"ip pool {x.Pool.Trim()}");
            using (b.Indent())
                b.Line($"static-bind ip-address {x.Ip.Trim()} {x.Mask.Trim()} mac-address {DashedMac(x.Mac)}");
        }
        b.Note("绑定前需确认地址池已有同名地址池；MAC 显示格式已归一为 xxxx-xxxx-xxxx，请核对。");
    }

    // =============================== v6：IP-MAC 绑定 ===============================
    protected override void GenArpBinds(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.ArpBinds.Where(x => IsValidIp(x.Ip) && !string.IsNullOrWhiteSpace(x.Mac)).ToList();
        if (items.Count == 0) return;

        b.Section("IP-MAC 静态绑定 (ARP)");
        using var _ = b.Indent();
        foreach (var x in items)
            b.Line($"arp static {x.Ip.Trim()} {DashedMac(x.Mac)}");
        b.Note("防欺骗可配合端口/VLAN 参数限定范围；仅靠全局 arp static 无法完全阻断欺骗，请按型号补充。");
    }

    // =============================== v6：风暴抑制 ===============================
    protected override void GenStorms(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.Storms.Where(x => !string.IsNullOrWhiteSpace(x.Interface)).ToList();
        if (items.Count == 0) return;

        b.Section("风暴抑制");
        using var _ = b.Indent();
        foreach (var x in items)
        {
            b.Line($"interface {x.Interface.Trim()}");
            using (b.Indent())
            {
                if (double.TryParse(x.BroadcastPct, out var bp)) b.Line($"broadcast-suppression {bp:0.##}");
                if (double.TryParse(x.MulticastPct, out var mp)) b.Line($"multicast-suppression {mp:0.##}");
                if (double.TryParse(x.UnknownUcPct, out var up)) b.Line($"unicast-suppression {up:0.##}");
            }
            b.Blank();
        }
        b.Note("华为接口抑制以百分比配置，0-100；不同型号可能支持 pps/比特率形式。");
    }

    // =============================== v6：IGMP Snooping ===============================
    protected override void GenIgmp(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var cfg = s.Igmp;
        if (!cfg.Enabled) return;
        var vlans = ExpandVlanList(cfg.VlanIds);

        b.Section("IGMP Snooping");
        using var _ = b.Indent();
        b.Line("igmp snooping enable");
        foreach (var id in vlans)
        {
            b.Line($"vlan {id}");
            using (b.Indent()) b.Line("igmp snooping enable");
        }
        b.Note("如需在汇聚口固定连接组播源/路由器，可把上行口配置为 igmp snooping router-port。");
    }

    // =============================== v11：策略路由 PBR ===============================
    protected override void GenPbr(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.Pbrs.Where(p => !string.IsNullOrWhiteSpace(p.Name)).ToList();
        if (items.Count == 0) return;

        b.Section("策略路由 PBR");
        using var _ = b.Indent();
        var bound = new HashSet<string>();
        var node = 10;
        foreach (var p in items)
        {
            if (!IsValidIp(p.NextHop) && string.IsNullOrWhiteSpace(p.ExitInterface)) continue;
            b.Line($"policy-based-route {p.Name.Trim()} permit node {node}");
            using (b.Indent())
            {
                if (!string.IsNullOrWhiteSpace(p.MatchAcl)) b.Line($"if-match acl {p.MatchAcl.Trim()}");
                if (!string.IsNullOrWhiteSpace(p.NextHop) && IsValidIp(p.NextHop))
                    b.Line($"apply next-hop {p.NextHop.Trim()}");
                else
                    b.Line($"apply output-interface {p.ExitInterface.Trim()}");
                if (!string.IsNullOrWhiteSpace(p.Description)) b.Line($"description {p.Description.Trim()}");
            }
            node += 10;
        }
        b.Blank();
        b.Note("PBR 依赖 ACL 匹配（请在「ACL 策略」模块中定义同名 ACL），动作优先取下一跳。");
        foreach (var p in items)
        {
            var iface = p.ApplyOn.Trim();
            var key = $"{iface}|{p.Name.Trim()}";
            if (iface.Length == 0 || !bound.Add(key)) continue;
            b.Line($"interface {iface}");
            using (b.Indent()) b.Line($"ip policy-based-route {p.Name.Trim()}");
        }
        b.Note("策略路由按 ACL 命中顺序匹配；与默认路由共存时，PBR 优先级高于路由表。");
        b.Note("华为 PBR（policy-based-route）为主流 S/AR 平台的转发策略路由写法；如需复杂匹配也可用 QoS 流策略（traffic behavior 中 redirect ip-nexthop）实现，命令随平台版本差异请核对。");
    }

    // =============================== v11：QoS 限速 ===============================
    protected override void GenQos(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.Qos.Where(q => !string.IsNullOrWhiteSpace(q.Interface) &&
                                     long.TryParse(q.CirKbps, out _) && long.Parse(q.CirKbps) > 0).ToList();
        if (items.Count == 0) return;

        b.Section("QoS 接口限速（CIR kbps）");
        using var qosScope = b.Indent();
        b.Note("华为限速命令随型号差异较大：S 系列/AR 常用 qos lr（限速），USG 防火墙建议用接口下 car 策略。");
        foreach (var q in items)
        {
            b.Line($"interface {q.Interface.Trim()}");
            using (b.Indent())
            {
                var words = q.Direction.Trim().ToLowerInvariant() switch
                {
                    "inbound" => new[] { "inbound" },
                    "outbound" => new[] { "outbound" },
                    _ => new[] { "inbound", "outbound" }
                };
                foreach (var dir in words)
                    b.Line($"qos lr {dir} cir {q.CirKbps.Trim()}");
                if (!string.IsNullOrWhiteSpace(q.Description)) b.Line($"description {q.Description.Trim()}");
            }
        }
        b.Note("qos lr 为整形限速；如需按报文丢弃限速（CAR）请按型号改用 qos car {dir} cir X。");
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
            var days = t.Days.Trim().ToLowerInvariant() switch
            {
                "daily" => "daily",
                "off-day" => "off-day",
                _ => "working-day"
            };
            b.Line($"time-range {t.Name.Trim()} {t.StartTime.Trim()} to {t.EndTime.Trim()} {days}");
            if (!string.IsNullOrWhiteSpace(t.Description)) b.Note($"  说明：{t.Description.Trim()}");
        }
        b.Note("在「ACL 策略」规则的“时间段”列填该名称即可定时生效；华为 time-range 引用的 ACL 请确认平台支持。");
    }

    // =============================== v12：BFD ===============================
    protected override void GenBfd(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.Bfds.Where(x => IsValidIp(x.PeerIp)).ToList();
        if (items.Count == 0) return;

        b.Section("BFD 双向转发检测");
        using var bfdScope = b.Indent();
        var idx = 0;
        foreach (var x in items)
        {
            idx++;
            if (string.IsNullOrWhiteSpace(x.Interface))
            {
                b.Note($"{x.PeerIp.Trim()} 未填本端接口，跳过会话；BFD 需绑定接口（三层口/VLANIF）。");
                continue;
            }
            var name = $"bfd{idx}";
            b.Line($"bfd {name} bind peer-ip {x.PeerIp.Trim()} interface {x.Interface.Trim()}");
            using (b.Indent())
            {
                b.Line($"discriminator local {1000 + idx * 2}");
                b.Line($"discriminator remote {1001 + idx * 2}");
                b.Line($"min-tx-interval {x.MinTxMs.Trim()}");
                b.Line($"min-rx-interval {x.MinTxMs.Trim()}");
                b.Line($"detect-multiplier {x.Multiplier.Trim()}");
                b.Line("commit");
            }
        }
        b.Note("对端设备请把 local/remote discriminator 互换；与 OSPF/BGP/静态路由联动的使能命令随 VRP 版本（如 OSPF 视图 bfd all-interfaces）请按型号补充。");
    }

    // =============================== v12：PoE ===============================
    protected override void GenPoe(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var items = s.PoEs.Where(p => !string.IsNullOrWhiteSpace(p.Interface)).ToList();
        if (items.Count == 0) return;

        b.Section("PoE 供电");
        using var poeScope = b.Indent();
        b.Note("PoE 命令随华为系列差异较大：S 系列接口 poe enable/priority 通用，全局功率预算（如 poe-power management）按型号补充。");
        foreach (var p in items)
        {
            b.Line($"interface {p.Interface.Trim()}");
            using (b.Indent())
            {
                b.Line(p.Enabled ? "poe enable" : "poe disable");
                if (p.Enabled) b.Line($"poe priority {p.Priority.Trim().ToLowerInvariant()}");
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

        b.Section("IPv6 基础配置");
        using var v6Scope = b.Indent();
        b.Line("ipv6");
        b.Line($"ipv6 interface {v6.Interface.Trim()}");
        using (b.Indent())
        {
            b.Line("ipv6 enable");
            b.Line($"ipv6 address {v6.Address.Trim()}");
            b.Line(v6.NdRa ? "undo ipv6 nd ra halt" : "ipv6 nd ra halt");
        }
        b.Note("undo ipv6 nd ra halt = 放行 RA 路由通告（终端自动获取 IPv6 地址与网关）；生产启用前确认上游已分配 IPv6 前缀并放通协议 58（ICMPv6/ND）。");
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
            b.Line($"interface {g.Name.Trim()}");
            using (b.Indent())
            {
                b.Line("tunnel-protocol gre");
                b.Line($"source {g.Source.Trim()}");
                b.Line($"destination {g.Destination.Trim()}");
                b.Line($"ip address {g.Address.Trim()} {g.Mask.Trim()}");
                if (!string.IsNullOrWhiteSpace(g.Description)) b.Line($"description {g.Description.Trim()}");
            }
        }
        b.Note("GRE 不加密：敏感流量请在隧道上再套 IPSec 或改用 IPsec VPN；两站点公网需双向可达，防火墙上需放行 GRE（IP 协议号 47）。");
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
            b.Line($"vlan batch {dv} {vv}");
            foreach (var run in PortRuns(ports))
            {
                b.Line($"port-group group-member {run}");
                using (b.Indent())
                {
                    b.Line("port link-type hybrid");
                    b.Line($"port hybrid untagged vlan {dv} {vv}");
                    b.Line($"port hybrid pvid vlan {dv}");
                    b.Line($"voice-vlan {vv} enable");
                }
            }
            if (!string.IsNullOrWhiteSpace(v.Description)) b.Note($"说明：{v.Description.Trim()}");
        }
        b.Note("电脑数据帧走 PVID（untagged），话机语音帧按 voice-vlan 打标签上行；部分 VRP 版本还需 voice-vlan remark-mode mac-address，请按型号核对。");
    }

    // =============================== v13：端口隔离 ===============================
    protected override void GenIsolate(CfgBuilder b, ConfigSpec s, DeviceType dt)
    {
        var iso = s.Isolate;
        if (!iso.Enabled) return;
        var ports = ExpandMembers(iso.Ports).ToList();
        if (ports.Count == 0) return;

        b.Section("端口隔离");
        using var isoScope = b.Indent();
        b.Note($"隔离组 {iso.GroupId}：组内端口二层互不通、均可访问上联口。");
        foreach (var run in PortRuns(ports))
        {
            b.Line($"port-group group-member {run}");
            using (b.Indent())
            {
                b.Line($"port-isolate enable group {iso.GroupId}");
            }
        }
        if (!string.IsNullOrWhiteSpace(iso.Uplink)) b.Note($"上联口 {iso.Uplink.Trim()} 不要加入隔离组（保持互通）。");
    }

}
