using System;
using System.Collections.Generic;
using System.Linq;

namespace FastGithub
{
    /// <summary>
    /// 命令行参数
    /// </summary>
    static class CommandLineParser
    {
        /// <summary>
        /// 简写参数到配置键的映射
        /// </summary>
        private static readonly Dictionary<string, string> aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["--port"] = "FastGithub:HttpProxyPort",
            ["--listen"] = "FastGithub:ListenAddress",
            ["--dns-listen"] = "FastGithub:DnsListenAddress",
            ["--dns-port"] = "FastGithub:DnsListenPort",
            ["--dns"] = "FastGithub:FallbackDns",
            ["--log-level"] = "Serilog:MinimumLevel:Default",
            ["--udp-port"] = "AppOptions:UdpLoggerPort",
        };

        /// <summary>
        /// 完整配置键的顶层前缀
        /// </summary>
        private static readonly string[] configRoots = new[]
        {
            "--FastGithub:",
            "--Serilog:",
            "--AppOptions:",
        };

        /// <summary>
        /// 尝试执行无需启动主机的指令（帮助、配置管理）
        /// </summary>
        /// <param name="args">命令行参数</param>
        /// <returns>已处理则返回true，程序应退出</returns>
        public static bool TryExecute(string[] args)
        {
            var argument = args.FirstOrDefault();

            if (string.Equals(argument, "-h", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, "--help", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleUtil.PrintUsage();
                return true;
            }

            if (string.Equals(argument, "config", StringComparison.OrdinalIgnoreCase))
            {
                ConfigCommand.Execute(args);
                return true;
            }

            // start/stop 为服务控制指令，需启动主机后由 UseCommand 处理
            if (string.Equals(argument, "start", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, "stop", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 首参数既非指令也不以-开头（不是参数），判定为输错指令：
            // 给出提示并不再启动主机，避免误以为执行了指令
            if (string.IsNullOrEmpty(argument) == false && argument.StartsWith("-") == false)
            {
                Console.WriteLine($"未知指令{argument}");
                Console.WriteLine("可用指令：start、stop、config，或用 -h 查看帮助");
                Environment.ExitCode = 1;
                return true;
            }

            // 未识别的参数给出警告（不阻止运行）
            foreach (var arg in args.Where(item => item.StartsWith("--") && IsKnownParameter(item) == false))
            {
                Console.WriteLine($"警告：未识别的参数{arg.Split('=')[0]}，已忽略");
                Console.WriteLine("覆盖任意配置请使用--配置键=值形式，如--FastGithub:HttpProxyPort=9000");
            }

            return false;
        }

        /// <summary>
        /// 是否为已支持的参数（简写别名或完整配置键）
        /// </summary>
        private static bool IsKnownParameter(string arg)
        {
            var name = arg.Split('=')[0];
            if (aliases.ContainsKey(name) == true)
            {
                return true;
            }

            foreach (var root in configRoots)
            {
                if (name.StartsWith(root, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 解析简写参数为配置项（仅当次运行生效）
        /// </summary>
        /// <param name="args">命令行参数</param>
        public static Dictionary<string, string?> ParseOverrides(string[] args)
        {
            var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var dnsValues = new List<string>();

            for (var i = 0; i < args.Length; i++)
            {
                if (aliases.TryGetValue(args[i], out var key) == false)
                {
                    continue;
                }

                if (i + 1 >= args.Length || args[i + 1].StartsWith("-"))
                {
                    Console.WriteLine($"参数{args[i]}缺少取值，已忽略");
                    continue;
                }

                var value = args[++i];
                if (key == "FastGithub:FallbackDns")
                {
                    // 多个dns按序号收集，最后统一转为数组配置项
                    dnsValues.Add(value);
                }
                else
                {
                    overrides[key] = value;
                }
            }

            for (var i = 0; i < dnsValues.Count; i++)
            {
                overrides[$"FastGithub:FallbackDns:{i}"] = dnsValues[i];
            }

            return overrides;
        }
    }
}
