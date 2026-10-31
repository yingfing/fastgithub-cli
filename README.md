# FastGithub（CLI 版）

github加速神器，解决github打不开、用户头像无法加载、releases无法上传下载、git-clone、git-pull、git-push失败等问题。

> 本版本**移除了原版的 Windows 图形界面（FastGithub.UI）**，改为**命令行 + 配置文件**控制，更轻量，适合服务器与容器部署。

* 原仓库 https://github.com/dotnetcore/fastgithub 已经没有了，本仓库拿了最新版本改了自用

### 1 写在前面
* **fastgithub不具备"翻墙"功能,也没有相关的计划**
* **fastgithub不支持Windows7等已被发行方停止支持的操作系统,并且也不会主动提供支持**
* **fastgithub不能为您的游戏加速**
* **fastgithub没有主动在github之外的任何渠道发布**

### 2 控制方式
本版本无图形界面，通过**命令行**与**配置文件**控制：命令行支持生命周期指令、配置管理子命令（config）和临时覆盖参数，任何配置项都能通过命令查看和修改。

#### 2.1 生命周期指令

| 指令 | 说明 |
| --- | --- |
| `fastgithub` | 不带指令直接运行，前台运行（Ctrl+C 退出） |
| `fastgithub start` | 安装并启动为系统服务（windows服务 / systemd服务） |
| `fastgithub stop` | 停止并卸载系统服务 |
| `fastgithub -h` / `fastgithub --help` | 打印帮助信息 |

> Linux/macOS 下 `start`/`stop` 需要 root 权限（操作 systemd 服务）。
> 服务方式为持久化运行，请先用 `config set` 改好配置再 `start`。

#### 2.2 配置管理指令（config）

| 指令 | 说明 |
| --- | --- |
| `fastgithub config list` | 列出合并后的全部配置项（appsettings.json + appsettings/\*.json） |
| `fastgithub config get <键>` | 查看某项配置，如 `config get FastGithub:HttpProxyPort` |
| `fastgithub config set <键> <值>` | 修改配置并写入 appsettings.json，如 `config set FastGithub:HttpProxyPort 9000` |
| `fastgithub config unset <键>` | 从 appsettings.json 删除某项配置，如 `config unset FastGithub:DomainConfigs:*.fastgithub.com` |

* 配置键以 `:` 分层，如 `FastGithub:DomainConfigs:github.com:TlsSni`
* `set` 的值支持 JSON 字面量（数字、布尔自动识别），非 JSON 文本按字符串处理
* 保存时以标准 JSON 重写 appsettings.json，原文件中的注释会丢失（值保留）
* `list`/`get` 展示的是配置文件合并后的结果，不含代码默认值

示例：
```bash
fastgithub config list
fastgithub config get FastGithub:DomainConfigs
fastgithub config set FastGithub:HttpProxyPort 9000
fastgithub config set FastGithub:DomainConfigs:github.com:TlsSni false
fastgithub config unset FastGithub:DomainConfigs:github.com
```

#### 2.3 临时覆盖参数（仅当次运行生效，优先级高于配置文件）

| 参数 | 说明 |
| --- | --- |
| `--port <端口>` | http 代理端口，如 `--port 9000` |
| `--listen <地址>` | 监听地址，`localhost` 仅本机（默认），`0.0.0.0` 监听所有网卡允许局域网设备连接 |
| `--dns-listen <地址>` | dns 监听地址，`localhost` 仅本机（默认），`0.0.0.0` 允许局域网设备使用本机 dns |
| `--dns-port <端口>` | dns 监听端口，默认 `53`，仅当 `--dns-listen` 不是 `localhost` 时生效 |
| `--dns <ip:端口>` | 回退 DNS，可多次指定，按序号覆盖配置项 |
| `--log-level <级别>` | 全局日志级别，如 `--log-level Debug` |
| `--udp-port <端口>` | UDP 日志端口 |
| `--<完整配置键>=<值>` | 覆盖任意配置项，如 `--FastGithub:DomainConfigs:github.com:TlsSni=false` |

优先级：**命令行 > appsettings/\*.json > appsettings.json > 代码默认值**。

示例：
```bash
fastgithub --port 9000 --dns 8.8.8.8:53 --dns 119.29.29.29:53 --log-level Debug
fastgithub --FastGithub:DomainConfigs:github.com:TlsSni=false
```

#### 2.4 配置文件

| 文件 | 说明 |
| --- | --- |
| `appsettings.json` | 主配置：代理端口、监听地址、FallbackDns、域名配置、日志级别 |
| `appsettings/appsettings.*.json` | 按主题拆分的子配置（github、google、fastly、microsoft 等），新增子配置重启后生效 |

常用主配置项：

| 配置键 | 默认值 | 说明 |
| --- | --- | --- |
| `FastGithub:HttpProxyPort` | `38457` | http 代理端口 |
| `FastGithub:ListenAddress` | `localhost` | 监听地址；`0.0.0.0` 监听所有网卡，允许局域网其它设备连接（见 3.6） |
| `FastGithub:DnsListenAddress` | `localhost` | dns 监听地址；`0.0.0.0` 允许局域网设备将 dns 指向本机（见 3.7） |
| `FastGithub:DnsListenPort` | `53` | dns 监听端口，仅当 `DnsListenAddress` 不是 `localhost` 时生效 |
| `FastGithub:FallbackDns` | `8.8.8.8:53` 等 | 回退 DNS，须支持 TCP |
| `AppOptions:UdpLoggerPort` | `38457` | UDP 日志端口 |

`appsettings.json` 中每个域名（支持 `*` 通配）可配置：

```json
"DomainConfigs": {
  "*.github.com": {
    "TlsSni": false,               // tls握手时是否发送SNI
    "TlsSniPattern": null,         // SNI表达式，@domain/@ipaddress/@random 变量
    "TlsIgnoreNameMismatch": true, // 是否忽略服务器证书域名不匹配
    "Timeout": null,               // 请求超时时长，如 "00:02:00"
    "IPAddress": null,             // 指定请求的ip
    "Destination": null,           // 请求目的地，绝对或相对Uri
    "Response": null               // 阻断请求直接响应，设置后其它配置不生效
  }
}
```

> 注意：Windows 反向代理端口（80/443/22/9418，Linux 为 3880/38443）为程序内置，被占用时自动顺延，暂不可通过命令或配置修改。

#### 2.5 输错提示与 Tab 补全

**输错提示**：

| 输错情况 | 行为 |
| --- | --- |
| 指令输错，如 `fastgithub strt` | 提示未知指令并列出可用指令，以退出码 1 结束，**不会误启动代理** |
| 参数输错，如 `fastgithub --prt 9000` | 警告未识别的参数并忽略，程序继续运行 |
| `config` 动作输错，如 `config lst` | 打印 config 的用法 |

**Tab 自动补全**（可选安装，补全指令、参数取值，以及 config 的配置键）：

```bash
# bash/zsh（把路径换为实际项目路径）
echo "source /path/to/FastGithub-Project/completions/fastgithub-completion.bash" >> ~/.bashrc
source ~/.bashrc
```

```powershell
# powershell
Add-Content $PROFILE ". /path/to/FastGithub-Project/completions/fastgithub-completion.ps1"
```

补全覆盖范围：

| 输入位置 | 补全内容 |
| --- | --- |
| `fastgithub <Tab>` | start、stop、config 与全部参数 |
| `fastgithub config <Tab>` | list、get、set、unset |
| `fastgithub config get/set/unset <Tab>` | 全部配置键（动态读取） |
| `--port`/`--udp-port` `<Tab>` | 38457、38458、8080、9000 |
| `--dns-port` `<Tab>` | 53、5353、8053 |
| `--listen`/`--dns-listen` `<Tab>` | localhost、0.0.0.0 |
| `--log-level` `<Tab>` | Verbose、Debug、Information、Warning、Error、Fatal |
| `--dns` `<Tab>` | 8.8.8.8:53、119.29.29.29:53、114.114.114.114:53 |

> 配置键补全会运行动态读取 `fastgithub config list`，需 fastgithub 在 PATH 中。
> PowerShell 下在无需补全的位置（如 `fastgithub start ` 之后）按 Tab，会回退为当前目录文件补全，这是 PowerShell 对原生命令的标准行为；bash 下则无任何提示。
> 两个补全脚本均已实测通过（bash 30 个场景、PowerShell 29 个场景，含尾随空格、前缀过滤、多参数跟踪）。

### 3 部署方式
#### 3.1 windows-x64 服务
```
fastgithub.exe start  // 以windows服务安装并启动
fastgithub.exe stop   // 以windows服务卸载并删除
```

#### 3.2 linux-x64 终端
```
sudo ./fastgithub
```
设置系统自动代理为`http://127.0.0.1:38457`，或手动代理http/https为`127.0.0.1:38457`

#### 3.3 linux-x64 服务
```
sudo ./fastgithub start  // 以systemd服务安装并启动
sudo ./fastgithub stop   // 以systemd服务卸载并删除
```
设置系统自动代理为`http://127.0.0.1:38457`，或手动代理http/https为`127.0.0.1:38457`

#### 3.4 macOS-x64
```
./fastgithub
```
安装`cacert/fastgithub.cer`并设置信任，设置系统自动代理为`http://127.0.0.1:38457`
[macOS 详细配置](docs/MacOSXConfig.md)

#### 3.5 docker-compose一键部署
准备好docker 18.09, docker-compose. 在源码目录下，有一个docker-compose.yaml 文件，专用于在实际项目中，临时使用github.com源码，而做的demo配置。根据自己的需要更新docker-compose.yaml中的sample和build镜像即可完成拉github.com源码加速，并基于源码做后续的操作。

#### 3.6 局域网设备接入（让其它设备通过本代理加速GitHub）

http 代理默认只监听本机（`localhost`），其它设备无法连接。按以下步骤开放给局域网（以端口 38457 为例）：

**1. 服务端开放监听**

```bash
fastgithub config set FastGithub:ListenAddress 0.0.0.0   # 监听所有网卡
# 重启生效；也可当次临时生效：fastgithub --listen 0.0.0.0
```

> Windows 部署默认只做本机透明拦截、不监听代理端口；配置 `ListenAddress` 为非 `localhost` 后，才会额外提供 http 代理。
> 监听地址也可指定为本机某块网卡的具体 IP，仅对该网卡开放。

**2. 防火墙放行端口**

```bash
# linux (firewalld)
sudo firewall-cmd --add-port=38457/tcp --permanent && sudo firewall-cmd --reload
# linux (ufw)
sudo ufw allow 38457/tcp
# windows（管理员权限）
netsh advfirewall firewall add rule name="FastGithub" dir=in action=allow protocol=TCP localport=38457
```

**3. 客户端配置代理**（`<服务器IP>` 换成运行 fastgithub 设备的局域网 IP，如 `192.168.1.10`）

* 浏览器/系统设置：HTTP 与 HTTPS 代理均填 `http://<服务器IP>:38457`
* 或使用自动代理：PAC 地址填 `http://<服务器IP>:38457/proxy.pac`（脚本按访问地址自适应生成）
* git：

```bash
git config --global http.proxy http://<服务器IP>:38457
git config --global http.sslverify false
```

**4. 客户端安装并信任 CA 证书**

代理会为每个域名签发证书，客户端必须信任服务端的 CA 证书，否则报证书错误。证书文件在服务端运行目录的 `cacert/fastgithub.cer`，拷贝到客户端后安装：

* Windows：双击导入 → 存储到“受信任的根证书颁发机构”
* Firefox：设置->隐私与安全->证书->查看证书->证书颁发机构->导入，勾选“信任由此证书颁发机构来标识网站”
* macOS：钥匙串访问导入，信任选项设为 Always Trust
* iOS/安卓：下载证书后在系统设置中安装（iOS 还需在 设置->通用->关于本机->证书信任设置 中开启）

**5. 验证**

客户端访问 github.com，或执行 `git clone https://github.com/<用户>/<仓库>.git`，速度正常即接入成功。

> **安全提示**：代理中间人会解密并重新签发 TLS 流量，请仅在可信内网使用，切勿暴露到公网；服务端的 CA 证书私钥不要泄露给他人。

#### 3.7 局域网 DNS 接入（让其它设备把 DNS 指向本机）

内置 dnscrypt-proxy 默认只监听本机回环地址。按以下步骤把它开放到局域网（以端口 53 为例）：

**1. 服务端开放监听**

```bash
fastgithub config set FastGithub:DnsListenAddress 0.0.0.0
# 可选：改监听端口，默认 53
fastgithub config set FastGithub:DnsListenPort 53
# 重启生效；也可当次临时生效
fastgithub --dns-listen 0.0.0.0 --dns-port 53
```

> `DnsListenAddress` 与 `ListenAddress` 相互独立：只开 DNS 不必开 http 代理，反之亦然。
> 53 端口在 Linux/macOS 属于特权端口，需要 root 或 `CAP_NET_BIND_SERVICE`；非 root 请改用 `--dns-port 5353` 等高位端口。
> 监听地址也可指定为本机某块网卡的具体 IP，仅对该网卡开放。
> 端口被占用或无权绑定时，程序会打印警告并回退为仅本机监听，不会影响其它功能。

**2. 防火墙放行端口**

```bash
# linux (firewalld)
sudo firewall-cmd --add-port=53/udp --add-port=53/tcp --permanent && sudo firewall-cmd --reload
# linux (ufw)
sudo ufw allow 53/udp
sudo ufw allow 53/tcp
# windows（管理员权限）
netsh advfirewall firewall add rule name="FastGithubDNS" dir=in action=allow protocol=UDP localport=53
netsh advfirewall firewall add rule name="FastGithubDNS" dir=in action=allow protocol=TCP localport=53
```

**3. 客户端配置 DNS**（`<服务器IP>` 换成运行 fastgithub 设备的局域网 IP）

* 系统/路由器 DNS：首选 DNS 填 `<服务器IP>`；路由器统一下发时，所有局域网设备自动生效
* 或只对单台设备生效：在网卡 DNS 设置里加入 `<服务器IP>`
* 端口不是 53 时，系统级 DNS 不支持自定义端口，需用支持 `ip:port` 的客户端（如 `dig @<服务器IP> -p 5353 github.com`、`nslookup -port=5353 github.com <服务器IP>`）

**4. 验证**

```bash
dig @<服务器IP> github.com
nslookup github.com <服务器IP>
```

**5. 注意**

* 只开放 DNS 时，DNS 解析会返回加速后的 IP，但 HTTPS 流量仍会直连；要完整加速网页/仓库访问，仍需按 3.6 配置代理并安装 CA 证书。
* DNS 服务本身不区分访问来源，请仅在可信内网开放，切勿把 53 端口暴露到公网。

### 4 日志
* 控制台实时输出
* 按天滚动写入 `logs/log.txt`
* 内置 UDP 日志端口（默认跟随 `UdpLoggerPort`，见 `AppOptions`），可供自定义客户端接收

### 5 软件功能
* 提供域名的纯净IP解析；
* 提供IP测速并选择最快的IP；
* 提供域名的tls连接自定义配置；
* google的CDN资源替换，解决大量国外网站无法加载js和css的问题；

### 6 证书验证
#### 6.1 git
git操作提示`SSL certificate problem`
需要关闭git的证书验证：`git config --global http.sslverify false`

#### 6.2 firefox
firefox提示`连接有潜在的安全问题`
设置->隐私与安全->证书->查看证书->证书颁发机构，导入`cacert/fastgithub.cer`，勾选"信任由此证书颁发机构来标识网站"

### 7 安全性说明
FastGithub为每台不同的主机生成自颁发CA证书，保存在cacert文件夹下。客户端设备需要安装和无条件信任自颁发的CA证书，请不要将证书私钥泄露给他人，以免造成损失。

### 8 合法性说明
《国际联网暂行规定》第六条规定："计算机信息网络直接进行国际联网，必须使用邮电部国家公用电信网提供的国际出入口信道。任何单位和个人不得自行建立或者使用其他信道进行国际联网。"
FastGithub本地代理使用的都是"公用电信网提供的国际出入口信道"，从国外Github服务器到国内用户电脑上FastGithub程序的流量，使用的是正常流量通道，其间未对流量进行任何额外加密（仅有网页原有的TLS加密，区别于VPN的流量加密），而FastGithub获取到网页数据之后发生的整个代理过程完全在国内，不再适用国际互联网相关之规定。

### 9 项目结构
```
FastGithub-Project/
├── FastGithub.sln            # 解决方案（7 个项目）
├── Directory.Build.props     # 全局构建属性（net7.0 / 版本号）
├── src/                      # 源代码
│   ├── FastGithub/               # 主机程序（CLI 入口）
│   ├── FastGithub.Configuration/ # 配置模型与绑定
│   ├── FastGithub.DomainResolve/ # 域名解析（dnscrypt-proxy）
│   ├── FastGithub.FlowAnalyze/   # 流量统计
│   ├── FastGithub.Http/          # HttpClient 工厂
│   ├── FastGithub.HttpServer/    # Kestrel 服务与各类中间件
│   └── FastGithub.PacketIntercept/# 数据包拦截（Windows）
├── tools/
│   └── dnscrypt-proxy/       # 各平台 dnscrypt-proxy 可执行文件与配置
├── docs/                     # 文档与配图
├── completions/              # shell自动补全脚本（bash/zsh与powershell）
├── docker-compose.yaml
├── publish.cmd               # windows 一键发布各平台
├── pack.sh                   # 发布后打包脚本
├── LICENSE
└── README.md
```

### 10 编译与发布
环境要求：.NET 7 SDK（或更高版本，.NET 8 SDK 可编译 net7.0 目标）

```bash
# 调试构建
dotnet build FastGithub.sln -c Release

# 发布单文件（以 linux-x64 为例）
dotnet publish -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true \
  --self-contained -r linux-x64 -o publish/fastgithub_linux-x64 \
  ./src/FastGithub/FastGithub.csproj

# windows 下一键发布全部平台
publish.cmd
```
