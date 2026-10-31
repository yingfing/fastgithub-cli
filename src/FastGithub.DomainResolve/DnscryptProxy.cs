using FastGithub.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static PInvoke.AdvApi32;

namespace FastGithub.DomainResolve
{
    /// <summary>
    /// DnscryptProxy服务
    /// </summary>
    sealed class DnscryptProxy
    {
        private readonly ILogger<DnscryptProxy> logger;
        private readonly IOptionsMonitor<FastGithubOptions> options;
        private readonly string processName;
        private readonly string serviceName;
        private readonly string exeFilePath;
        private readonly string tomlFilePath;

        /// <summary>
        /// 相关进程
        /// </summary>
        private Process? process;

        /// <summary>
        /// 获取监听的节点
        /// </summary>
        public IPEndPoint? LocalEndPoint { get; private set; }

        /// <summary>
        /// 获取对外提供dns服务的节点，未开启局域网dns或未启动时为空
        /// </summary>
        public IPEndPoint? ServerEndPoint { get; private set; }

        /// <summary>
        /// DnscryptProxy服务
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="options"></param>
        public DnscryptProxy(ILogger<DnscryptProxy> logger, IOptionsMonitor<FastGithubOptions> options)
        {
            const string PATH = "dnscrypt-proxy";
            const string NAME = "dnscrypt-proxy";

            this.logger = logger;
            this.options = options;
            this.processName = NAME;
            this.serviceName = $"{nameof(FastGithub)}.{NAME}";
            this.exeFilePath = Path.Combine(PATH, OperatingSystem.IsWindows() ? $"{NAME}.exe" : NAME);
            this.tomlFilePath = Path.Combine(PATH, $"{NAME}.toml");
        }

        /// <summary>
        /// 启动dnscrypt-proxy
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await this.StartCoreAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning($"{this.processName}启动失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 启动dnscrypt-proxy
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task StartCoreAsync(CancellationToken cancellationToken)
        {
            var endPoints = this.GetListenEndPoints();
            var localEndPoint = endPoints.Local;
            var serverEndPoint = endPoints.Server;

            await TomlUtil.SetListensAsync(this.tomlFilePath, endPoints.All, cancellationToken);
            await TomlUtil.SetLogLevelAsync(this.tomlFilePath, 6, cancellationToken);
            await TomlUtil.SetLBStrategyAsync(this.tomlFilePath, "ph", cancellationToken);
            await TomlUtil.SetMinMaxTTLAsync(this.tomlFilePath, TimeSpan.FromMinutes(1d), TimeSpan.FromMinutes(2d), cancellationToken);

            if (OperatingSystem.IsWindows() && Environment.UserInteractive == false)
            {
                ServiceInstallUtil.StopAndDeleteService(this.serviceName);
                ServiceInstallUtil.InstallAndStartService(this.serviceName, this.exeFilePath, ServiceStartType.SERVICE_DEMAND_START);
                this.process = Process.GetProcessesByName(this.processName).FirstOrDefault(item => item.SessionId == 0);
            }
            else
            {
                this.process = StartDnscryptProxy();
            }

            if (this.process != null)
            {
                this.LocalEndPoint = localEndPoint;
                this.ServerEndPoint = serverEndPoint;
                this.process.EnableRaisingEvents = true;
                this.process.Exited += (s, e) =>
                {
                    this.LocalEndPoint = null;
                    this.ServerEndPoint = null;
                };

                if (serverEndPoint != null)
                {
                    this.logger.LogInformation($"已监听dns://{serverEndPoint}，局域网设备可将dns指向该地址");
                }
            }
        }

        /// <summary>
        /// 获取dns监听节点
        /// </summary>
        /// <returns></returns>
        private (IPEndPoint Local, IPEndPoint? Server, IPEndPoint[] All) GetListenEndPoints()
        {
            var localPort = GlobalListener.GetAvailablePort(5533);
            var local = new IPEndPoint(IPAddress.Loopback, localPort);

            var server = this.GetServerEndPoint(local);
            if (server == null)
            {
                return (local, null, new[] { local });
            }
            return (local, server, new[] { local, server });
        }

        /// <summary>
        /// 获取局域网dns监听节点，未开启或不可用时返回null
        /// </summary>
        /// <param name="local"></param>
        /// <returns></returns>
        private IPEndPoint? GetServerEndPoint(IPEndPoint local)
        {
            var address = this.options.CurrentValue.DnsListenAddress;
            if (string.IsNullOrEmpty(address) ||
                string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // windows下本机dns由WinDivert透明拦截直接应答，原先会误应答局域网查询，故直接禁用；
            // 现已让DnsInterceptor按本机dns监听地址排除局域网来源包，此处允许开启

            if (IPAddress.TryParse(address, out var ip) == false)
            {
                this.logger.LogWarning($"dns监听地址{address}无效，应为localhost、0.0.0.0或本机网卡的具体ip，已仅监听本机");
                return null;
            }

            var port = this.options.CurrentValue.DnsListenPort;
            if (port < 1 || port > IPEndPoint.MaxPort)
            {
                this.logger.LogWarning($"dns监听端口{port}无效，已仅监听本机");
                return null;
            }

            var server = new IPEndPoint(ip, port);
            if (server.Port == local.Port)
            {
                this.logger.LogWarning($"dns监听端口{port}与本机内部dns端口相同，已仅监听本机");
                return null;
            }

            if (CanBind(server) == false)
            {
                this.logger.LogWarning($"dns监听{server}失败，端口可能被占用或缺少绑定权限（1024以下端口需要root），已仅监听本机");
                return null;
            }

            return server;
        }

        /// <summary>
        /// 检测节点是否可以绑定
        /// </summary>
        /// <param name="endPoint"></param>
        /// <returns></returns>
        private static bool CanBind(IPEndPoint endPoint)
        {
            try
            {
                using var udp = new System.Net.Sockets.Socket(endPoint.AddressFamily, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
                udp.Bind(endPoint);
            }
            catch (Exception)
            {
                return false;
            }

            try
            {
                using var tcp = new System.Net.Sockets.Socket(endPoint.AddressFamily, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
                tcp.Bind(endPoint);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 停止服务
        /// </summary>
        public void Stop()
        {
            try
            {
                if (OperatingSystem.IsWindows() && Environment.UserInteractive == false)
                {
                    ServiceInstallUtil.StopAndDeleteService(this.serviceName);
                }

                if (this.process != null && this.process.HasExited == false)
                {
                    this.process.Kill();
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning($"{this.processName}停止失败：{ex.Message }");
            }
            finally
            {
                this.LocalEndPoint = null;
                this.ServerEndPoint = null;
            }
        }

        /// <summary>
        /// 启动DnscryptProxy进程
        /// </summary> 
        /// <returns></returns>
        private Process? StartDnscryptProxy()
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = this.exeFilePath,
                WorkingDirectory = Path.GetDirectoryName(this.exeFilePath),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return this.processName;
        }
    }
}
