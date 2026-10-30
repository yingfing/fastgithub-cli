using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FastGithub
{
    /// <summary>
    /// 配置管理指令
    /// </summary>
    static class ConfigCommand
    {
        private const string fileName = "appsettings.json";
        private const string settingsDir = "appsettings";

        /// <summary>
        /// 执行配置指令
        /// </summary>
        /// <param name="args">命令行参数，如 ["config", "set", "键", "值"]</param>
        public static void Execute(string[] args)
        {
            var action = args.ElementAtOrDefault(1)?.ToLowerInvariant();
            switch (action)
            {
                case "list":
                    List();
                    break;
                case "get":
                    Get(args);
                    break;
                case "set":
                    Set(args);
                    break;
                case "unset":
                    Unset(args);
                    break;
                default:
                    PrintUsage();
                    break;
            }
        }

        /// <summary>
        /// 打印用法
        /// </summary>
        private static void PrintUsage()
        {
            Console.WriteLine("用法：fastgithub config <动作> [键] [值]");
            Console.WriteLine();
            Console.WriteLine("动作：");
            Console.WriteLine("  list              列出配置文件中的全部配置项");
            Console.WriteLine("  get <键>          查看某项配置，如 get FastGithub:HttpProxyPort");
            Console.WriteLine("  set <键> <值>     修改配置并写入appsettings.json，如 set FastGithub:HttpProxyPort 9000");
            Console.WriteLine("  unset <键>        从appsettings.json删除某项配置");
            Console.WriteLine();
            Console.WriteLine("配置键以:分层，如 FastGithub:DomainConfigs:github.com:TlsSni");
        }

        /// <summary>
        /// 列出合并后的全部配置项
        /// </summary>
        private static void List()
        {
            var items = GetConfiguration().AsEnumerable()
                .Where(item => item.Value != null)
                .OrderBy(item => item.Key);

            foreach (var item in items)
            {
                Console.WriteLine($"{item.Key}={item.Value}");
            }
        }

        /// <summary>
        /// 查看某项配置
        /// </summary>
        private static void Get(string[] args)
        {
            var key = args.ElementAtOrDefault(2);
            if (string.IsNullOrEmpty(key) == true)
            {
                PrintUsage();
                return;
            }

            var items = GetConfiguration().AsEnumerable()
                .Where(item => item.Value != null)
                .Where(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                               item.Key.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Key)
                .ToArray();

            if (items.Length == 0)
            {
                Console.WriteLine($"未找到配置项{key}");
                return;
            }

            foreach (var item in items)
            {
                Console.WriteLine($"{item.Key}={item.Value}");
            }
        }

        /// <summary>
        /// 修改配置并写入appsettings.json
        /// </summary>
        private static void Set(string[] args)
        {
            var key = args.ElementAtOrDefault(2);
            var value = args.ElementAtOrDefault(3);

            if (string.IsNullOrEmpty(key) == true || value == null)
            {
                PrintUsage();
                return;
            }

            var path = GetFilePath();
            var root = ReadJson(path) ?? new JsonObject();

            var node = root;
            var segments = key.Split(':');
            for (var i = 0; i < segments.Length - 1; i++)
            {
                // 中间层级不存在或不是对象时，创建为对象
                if (node[segments[i]] is not JsonObject child)
                {
                    child = new JsonObject();
                    node[segments[i]] = child;
                }
                node = child;
            }

            node[segments[^1]] = ParseValue(value);
            WriteJson(path, root);
            Console.WriteLine($"已设置{key}={value}");
        }

        /// <summary>
        /// 从appsettings.json删除某项配置
        /// </summary>
        private static void Unset(string[] args)
        {
            var key = args.ElementAtOrDefault(2);
            if (string.IsNullOrEmpty(key) == true)
            {
                PrintUsage();
                return;
            }

            var path = GetFilePath();
            var root = ReadJson(path);
            if (root != null)
            {
                JsonObject? node = root as JsonObject;
                var segments = key.Split(':');
                for (var i = 0; i < segments.Length - 1; i++)
                {
                    node = node?[segments[i]] as JsonObject;
                    if (node == null)
                    {
                        break;
                    }
                }

                if (node != null && node.ContainsKey(segments[^1]) == true)
                {
                    node.Remove(segments[^1]);
                    WriteJson(path, root);
                    Console.WriteLine($"已删除{key}");
                    return;
                }
            }

            Console.WriteLine($"appsettings.json中未找到配置项{key}");
            Console.WriteLine("来自appsettings/子配置文件的配置项，请手动编辑对应文件");
        }

        /// <summary>
        /// 获取配置文件路径
        /// </summary>
        private static string GetFilePath()
        {
            return Path.Combine(AppContext.BaseDirectory, fileName);
        }

        /// <summary>
        /// 读取配置文件（跳过注释）
        /// </summary>
        private static JsonNode? ReadJson(string path)
        {
            if (File.Exists(path) == false)
            {
                return null;
            }

            var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip };
            return JsonNode.Parse(File.ReadAllText(path), documentOptions: options);
        }

        /// <summary>
        /// 写入配置文件
        /// </summary>
        private static void WriteJson(string path, JsonNode root)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, root.ToJsonString(options));
        }

        /// <summary>
        /// 解析配置值（JSON字面量或字符串）
        /// </summary>
        private static JsonNode? ParseValue(string value)
        {
            try
            {
                // 数字、布尔等按JSON字面量写入
                return JsonNode.Parse(value);
            }
            catch (JsonException)
            {
                // 非JSON文本按字符串写入
                return JsonValue.Create(value);
            }
        }

        /// <summary>
        /// 获取合并后的配置（appsettings.json + appsettings/*.json）
        /// </summary>
        private static IConfigurationRoot GetConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(fileName, true, true);

            var dir = Path.Combine(AppContext.BaseDirectory, settingsDir);
            if (Directory.Exists(dir) == true)
            {
                foreach (var file in Directory.GetFiles(dir, "appsettings.*.json"))
                {
                    builder.AddJsonFile(Path.Combine(settingsDir, Path.GetFileName(file)), true, true);
                }
            }

            return builder.Build();
        }
    }
}
