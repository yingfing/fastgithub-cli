using System;
using System.Collections.Generic;
using System.Net;

namespace FastGithub.Configuration
{
    /// <summary>
    /// FastGithub的配置
    /// </summary>
    public class FastGithubOptions
    {
        /// <summary>
        /// http代理端口
        /// </summary>
        public int HttpProxyPort { get; set; } = 38457;

        /// <summary>
        /// http代理的监听地址
        /// localhost仅本机访问；0.0.0.0监听所有网卡，允许局域网其它设备连接；也可指定本机网卡的具体ip
        /// </summary>
        public string ListenAddress { get; set; } = "localhost";

        /// <summary>
        /// 回退的dns
        /// </summary>
        public IPEndPoint[] FallbackDns { get; set; } = Array.Empty<IPEndPoint>();

        /// <summary>
        /// 代理的域名配置
        /// </summary>
        public Dictionary<string, DomainConfig> DomainConfigs { get; set; } = new();
    }
}
