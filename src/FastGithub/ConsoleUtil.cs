using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FastGithub
{
    static class ConsoleUtil
    {
        private const uint ENABLE_QUICK_EDIT = 0x0040;

        private const int STD_INPUT_HANDLE = -10;

        [DllImport("kernel32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        /// <summary>
        /// 禁用快速编辑模式
        /// </summary>
        /// <returns></returns>
        public static bool DisableQuickEdit()
        {
            if (OperatingSystem.IsWindows())
            {
                var hwnd = GetStdHandle(STD_INPUT_HANDLE);
                if (GetConsoleMode(hwnd, out uint mode))
                {
                    mode &= ~ENABLE_QUICK_EDIT;
                    return SetConsoleMode(hwnd, mode);
                }
            }

            return false;
        }

        /// <summary>
        /// 打印用法
        /// </summary>
        public static void PrintUsage()
        {
            Console.WriteLine("用法：fastgithub [指令|参数]");
            Console.WriteLine();
            Console.WriteLine("指令：");
            Console.WriteLine("  start                    安装并启动为系统服务（windows服务/systemd服务）");
            Console.WriteLine("  stop                     停止并卸载系统服务");
            Console.WriteLine("  config list              列出配置项");
            Console.WriteLine("  config get <键>         查看某项配置，如 config get FastGithub:HttpProxyPort");
            Console.WriteLine("  config set <键> <值>    修改配置并写入appsettings.json，如 config set FastGithub:HttpProxyPort 9000");
            Console.WriteLine("  config unset <键>       删除某项配置");
            Console.WriteLine("  -h, --help               打印帮助信息");
            Console.WriteLine();
            Console.WriteLine("不带指令直接运行即为前台运行");
            Console.WriteLine();
            Console.WriteLine("临时覆盖参数（仅当次运行生效，优先级高于配置文件）：");
            Console.WriteLine("  --port <端口>            http代理端口，如 --port 9000");
            Console.WriteLine("  --listen <地址>         监听地址，localhost仅本机(默认)，0.0.0.0允许局域网设备连接");
            Console.WriteLine("  --dns-listen <地址>     dns监听地址，localhost仅本机(默认)，0.0.0.0允许局域网设备使用本机dns");
            Console.WriteLine("  --dns-port <端口>       dns监听端口，默认53，仅当--dns-listen不是localhost时生效");
            Console.WriteLine("  --dns <ip:端口>         回退dns，可多次指定，按序号覆盖");
            Console.WriteLine("  --log-level <级别>      全局日志级别，如 --log-level Debug");
            Console.WriteLine("  --udp-port <端口>       udp日志端口");
            Console.WriteLine("  --<完整配置键>=<值>     覆盖任意配置项，如 --FastGithub:DomainConfigs:github.com:TlsSni=false");
            Console.WriteLine();
            Console.WriteLine("配置键以:分层，配置说明见appsettings.json与appsettings/appsettings.*.json");
            Console.WriteLine("Tab自动补全脚本见completions/目录（bash/zsh与powershell）");
        }
    }
}
