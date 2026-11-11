namespace NetConfigPro.Services;

/// <summary>一条命令手册条目。</summary>
public sealed class HelpEntry
{
    public string Name { get; init; } = "";      // 功能名（如 "配置管理地址"）
    public string Cmd { get; init; } = "";       // 命令/语法示例
    public string Note { get; init; } = "";      // 要点与厂商差异
}

/// <summary>一个手册主题。</summary>
public sealed class HelpTopic
{
    public string Title { get; init; } = "";
    public List<HelpEntry> Entries { get; init; } = [];
}

/// <summary>内置命令手册（内容经厂商官方语法审计；上真机前仍请按实际型号核对）。</summary>
public static class HelpDb
{
    public static readonly List<HelpTopic> Topics =
    [
        new HelpTopic
        {
            Title = "系统与远程管理",
            Entries =
            [
                new HelpEntry { Name = "主机名", Cmd = "华为/华三: sysname R1｜思科: hostname R1｜锐捷: hostname R1",
                    Note = "华为/华三在 system-view 下配；思科/锐捷在全局配置模式（enable→configure terminal）下配。" },
                new HelpEntry { Name = "DNS / NTP / Syslog", Cmd = "华为: dns server X｜ntp-service unicast-server X｜info-center loghost X",
                    Note = "华三同体系；思科: ip name-server X / ntp server X / logging host X；锐捷: ip name-server / ntp server / logging host。ASA 的 logging host 必须带接口名（logging host inside X）。" },
                new HelpEntry { Name = "SSH 服务", Cmd = "华为: stelnet server enable + rsa local-key-pair create",
                    Note = "华三: ssh server enable + public-key local create rsa；思科: ip ssh version 2 + crypto key generate rsa；锐捷: enable service ssh-server。华为/华三需先有 RSA 密钥；思科密钥生成是交互式（本工具输出请手工执行）。" },
                new HelpEntry { Name = "Telnet 服务", Cmd = "华为/华三: telnet server enable",
                    Note = "思科 IOS: line vty 0 4 下 transport input telnet；锐捷: enable service telnet-server。安全加固模块仅在未勾 Telnet 模块时输出关闭命令，避免先关后开冲突。" },
                new HelpEntry { Name = "Web 管理", Cmd = "华为: http server enable / http secure-server enable",
                    Note = "华三: ip http enable / ip https enable；思科 IOS: ip http server / ip http secure-server（ASA: http server enable）；锐捷: enable service web-server。Web 登录复用本地用户，务必改默认口令；HTTPS 需证书。" },
                new HelpEntry { Name = "本地用户", Cmd = "华为: aaa 视图内 local-user X password cipher P / privilege level N / service-type ssh",
                    Note = "华为 local-user 必须在 aaa 视图（不是系统视图）；华三: local-user X class manage + authorization-attribute user-role network-admin；思科: username X privilege 15 secret P；锐捷: username X password P。接入方式 token：ssh/telnet/terminal/ftp（华三 http 写 web）。" }
            ]
        },
        new HelpTopic
        {
            Title = "接口与二层",
            Entries =
            [
                new HelpEntry { Name = "接口 Access / Trunk", Cmd = "华为: port link-type access + port default vlan N｜trunk: port trunk allow-pass vlan A to B",
                    Note = "华三同体系但 trunk 放行写 port trunk permit vlan（华为 allow-pass）；思科: switchport mode access/trunk + switchport access vlan N + switchport trunk allowed vlan A-B；锐捷: switchport access vlan / switchport trunk allowed vlan。防火墙（华为 USG/华三/ASA）接口无二层模式，走三层口或子接口。" },
                new HelpEntry { Name = "三层 VLAN 接口", Cmd = "华为: interface Vlanif10",
                    Note = "华三: interface Vlan-interface10；思科 Catalyst: interface Vlan10；锐捷: interface VLAN 10。注意思科/华三/锐捷路由器（无交换引擎）不能建 VLAN 接口，用子接口 encapsulate/encapsulation dot1Q。" },
                new HelpEntry { Name = "链路聚合", Cmd = "华为: interface Eth-Trunk1 + 成员口 eth-trunk 1",
                    Note = "华三: interface Bridge-Aggregation1（成员口 port link-aggregation group 1）；思科: interface Port-channel1（成员口 channel-group 1 mode active）；锐捷: interface AggregatePort 1。成员口输入支持 A to B 区间展开。" },
                new HelpEntry { Name = "端口镜像", Cmd = "华为: observe-port 1 interface X + 源口 port-mirroring to observe-port 1 both",
                    Note = "华三: mirroring-group 1 local + monitor-port/source；思科: monitor session 1 source/destination；锐捷: monitor session 1 source/destination。方向词：华为/华三 inbound/outbound，思科/锐捷 rx/tx。" },
                new HelpEntry { Name = "端口安全", Cmd = "华为: 接口下 port-security enable + port-security max-mac-num N",
                    Note = "违规动作华为 protect/restrict/shutdown、思科 protect/restrict/shutdown、锐捷同；思科/锐捷接口下用 switchport port-security。仅交换机适用。" }
            ]
        },
        new HelpTopic
        {
            Title = "VLAN 与生成树",
            Entries =
            [
                new HelpEntry { Name = "创建 VLAN", Cmd = "华为/华三: vlan 10（视图内 description/name）",
                    Note = "思科: vlan 10 数据库式创建（name 可选）；锐捷: vlan 10 + name。VLAN 范围 1-4094；批量可用 vlan batch 10 to 20（华三/华为）。" },
                new HelpEntry { Name = "生成树模式", Cmd = "华为: stp mode rstp / mstp（+ stp region-configuration + instance N vlan）",
                    Note = "思科只有 pvst/rapid-pvst/mst（无 rstp 关键字）；锐捷: spanning-tree mode rstp/mstp；华三同华为体系。MSTP 需所有交换机同一 region 名/修订号。" }
            ]
        },
        new HelpTopic
        {
            Title = "路由",
            Entries =
            [
                new HelpEntry { Name = "静态路由", Cmd = "华为/华三: ip route-static 0.0.0.0 0.0.0.0 下一跳",
                    Note = "思科: ip route + 前缀/掩码（ASA 用 route outside 0 0 网关，首参为 nameif）；锐捷: ip route（接口 Dialer 等带空格写法不同）。可加优先级（华为 preference N / 思科后置 N）。" },
                new HelpEntry { Name = "OSPF", Cmd = "华为: ospf 1 router-id X → area 0 → network 网段 反掩码",
                    Note = "华为/华三/思科/锐捷 network 后均用反掩码；ASA 用正掩码。区域类型：stub/nssa（华为/华三在 area 视图内写 stub/nssa；思科 area N stub/nssa 单行）。" },
                new HelpEntry { Name = "BGP", Cmd = "华为: bgp 65001 + router-id + peer X as-number Y",
                    Note = "华三邻居须在地址族下 peer enable；思科/锐捷: router bgp + neighbor X remote-as Y；思科 BGP 需 network 掩码精确匹配（network 10.0.0.0 mask 255.0.0.0）。" }
            ]
        },
        new HelpTopic
        {
            Title = "ACL 与时间段",
            Entries =
            [
                new HelpEntry { Name = "ACL 编号段", Cmd = "华为/华三/锐捷: 基本 2000-2999，高级 3000-3999",
                    Note = "思科: 标准 1-99/1300-1999，扩展 100-199/2000-2699。本工具预检会按平台提示越界。锐捷部分版本支持思科式编号（差异见 README）。" },
                new HelpEntry { Name = "ACL 规则引用时间段", Cmd = "华为: rule 5 permit ip source X time-range office",
                    Note = "华为/华三基本与高级 ACL 均支持 time-range；思科 time-range 只支持扩展 ACL（标准 ACL 不支持，本工具在标准行不拼 time-range）；ASA 不支持 IOS time-range 语法。" },
                new HelpEntry { Name = "time-range 定义", Cmd = "华为/华三: time-range NAME hh:mm to hh:mm working-day|daily|off-day",
                    Note = "思科/锐捷: time-range NAME + periodic weekdays|daily|weekend hh:mm to hh:mm。时间格式 24 小时 HH:MM。" }
            ]
        },
        new HelpTopic
        {
            Title = "NAT 与端口映射",
            Entries =
            [
                new HelpEntry { Name = "Easy IP（出网转换）", Cmd = "华为: nat outbound 2000（需先建 ACL 2000）",
                    Note = "华三: nat outbound 2000；思科: ip nat inside source list 1 interface GigabitEthernet0/0/1 overload；锐捷: ip nat inside source list 1 interface X overload。Easy IP 引用的 ACL 必须存在，否则内网无法上网。" },
                new HelpEntry { Name = "端口映射（NAT Server）", Cmd = "华为: nat server protocol tcp global 公网IP 8080 inside 内网IP 80",
                    Note = "华三: nat server protocol tcp global current-interface 8080 inside X 80；思科: ip nat inside source static tcp 内网IP 80 公网IP 8080；锐捷同思科。公网 IP 留空表示使用接口地址。" },
                new HelpEntry { Name = "NAT 静态一对一", Cmd = "华为: nat static global 公网 inside 内网",
                    Note = "华三/思科/锐捷类似（思科 ip nat inside source static）。NAT 仅路由器/防火墙适用（交换机无出口 NAT）。" }
            ]
        },
        new HelpTopic
        {
            Title = "防火墙策略",
            Entries =
            [
                new HelpEntry { Name = "安全区域", Cmd = "华为: firewall zone trust + add interface GigabitEthernet0/0/1",
                    Note = "华三: security-zone name trust + import interface；锐捷: security-zone trust（老版本 zone）区域归属接口后，跨域流量才受安全策略约束。华为 USG/华三/锐捷防火墙接口配 IP 即可（无 port link-type）。" },
                new HelpEntry { Name = "区域间策略", Cmd = "华为: security-policy → rule name X → source-zone trust → destination-zone untrust → action permit",
                    Note = "华三: security-policy ip + rule X name + source-ip-subnet/destination-ip-subnet + action pass；锐捷: security-policy + rule。默认所有跨域 deny，务必放行回应流量。" },
                new HelpEntry { Name = "ASA 特点", Cmd = "思科 ASA ACL 只能命名（access-list NCP-IN extended permit ip 任意 任意 netmask 反掩码）",
                    Note = "ASA 访问列表用正掩码；默认放行内→外需显式写（无 implicit permit）。ASA 支持 OSPF/EIGRP/RIP/BGP，但 BFD/VRRP/time-range 不支持。" }
            ]
        },
        new HelpTopic
        {
            Title = "DHCP",
            Entries =
            [
                new HelpEntry { Name = "DHCP 服务", Cmd = "华为: dhcp enable + ip pool X + network/gateway-list/dns-list → 接口 dhcp select global",
                    Note = "华三: dhcp server ip-pool（接口 dhcp server apply ip-pool）；思科: ip dhcp pool（思科路由器接口自动下发无需 select）；锐捷: service dhcp + ip dhcp pool。华为/华三地址池若未绑定网关三层口，客户端拿不到地址（VlanId 必填）。" },
                new HelpEntry { Name = "DHCP 中继", Cmd = "华为: 接口 dhcp select relay + dhcp relay server-ip X",
                    Note = "华三同；思科: ip helper-address X；锐捷同思科。DHCP Snooping 信任口/option82 仅交换机。" }
            ]
        },
        new HelpTopic
        {
            Title = "无线（FAT AP）",
            Entries =
            [
                new HelpEntry { Name = "SSID 与安全", Cmd = "华为: ssid-profile/security-profile/vap-profile + ap-group",
                    Note = "华三: wlan service-template N + akm mode psk + preshared-key；思科 autonomous: dot11 ssid X + authentication open + guest-mode（业务 vlan 绑定合法）；锐捷: dot11 wlan 1 + security wpa2 akm psk set-key ascii。瘦 AP（AC 架构）命令完全不同，本工具面向 FAT/直连场景。" }
            ]
        },
        new HelpTopic
        {
            Title = "策略路由 / QoS / BFD / VRRP",
            Entries =
            [
                new HelpEntry { Name = "策略路由 PBR", Cmd = "华为/华三: policy-based-route NAME permit node 10 + if-match acl X + apply next-hop Y + 入口接口 ip policy-based-route",
                    Note = "思科/锐捷: route-map X permit 10 + match ip address + set ip next-hop + 接口 ip policy route-map。PBR 优先于路由表；ASA 不支持（提示改用 policy-map）。" },
                new HelpEntry { Name = "接口限速 QoS", Cmd = "华为/华三: 接口 qos lr outbound cir 2048（kbps）",
                    Note = "思科/锐捷 CAR: rate-limit output 2048000 8000 16000 conform-action transmit exceed-action drop（注意 bps=值×1000）。direction both 输出两行。" },
                new HelpEntry { Name = "BFD", Cmd = "华为/华三: bfd bfd1 bind peer-ip X interface Y + discriminator local/remote + commit",
                    Note = "思科/锐捷: 接口 bfd interval 100 min_rx 100 multiplier 3。对端 local/remote discriminator 互换；OSPF/BGP 联动使能命令随版本。ASA 不支持 BFD。" },
                new HelpEntry { Name = "VRRP 网关冗余", Cmd = "华为: interface Vlanif10 → vrrp vrid 1 virtual-ip 网关IP + priority 120",
                    Note = "华三同体系（preempt-mode disable 关抢占）；思科/锐捷: 接口 vrrp 1 ip 网关 + priority + preempt。主备两台配置相同组/虚拟 IP、不同优先级。" }
            ]
        },
        new HelpTopic
        {
            Title = "巡检 / 诊断常用",
            Entries =
            [
                new HelpEntry { Name = "系统与资源", Cmd = "华为: display version / cpu-usage / memory-usage / device",
                    Note = "华三同 display 体系；思科: show version / processes cpu / memory / environment all；锐捷: show version / cpu / memory / environment。" },
                new HelpEntry { Name = "接口与转发表", Cmd = "华为: display interface brief / ip routing-table / arp / mac-address",
                    Note = "思科: show ip interface brief / ip route / arp / mac address-table；仅交换机才查 MAC/生成树（display stp / show spanning-tree）。巡检页已按设备过滤。" },
                new HelpEntry { Name = "会话与配置", Cmd = "华为防火墙: display firewall session table；思科 ASA: show conn count + xlate count",
                    Note = "仅防火墙有会话表；日志 display logbuffer / show logging；整机导出华为 diagnostic-information、思科 tech-support、锐捷 tech-support（内容大慎用）。" }
            ]
        }
    ];
}
