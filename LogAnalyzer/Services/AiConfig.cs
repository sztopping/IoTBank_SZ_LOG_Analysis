using System.IO;
using System.Text.Json;

namespace LogAnalyzer.Services;

/// <summary>
/// 一个免费/开放 AI 提供商（OpenAI 兼容接口）。
/// </summary>
public class AiProvider
{
    public string Name { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
}

/// <summary>
/// AI 解析配置：从程序目录 ai_config.json 读取（可配置多个免费 AI 提供商）。
/// 未配置 ApiKey 时，AI 增强自动跳过（仅本地解析）。
/// </summary>
public static class AiConfig
{
    public static List<AiProvider> Providers { get; private set; } = new();

    public static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "ai_config.json");

    /// <summary>加载配置；文件不存在或为空时返回空列表。</summary>
    public static void Load()
    {
        Providers = new List<AiProvider>();
        try
        {
            if (!File.Exists(ConfigPath)) return;
            var json = File.ReadAllText(ConfigPath);
            var cfg = JsonSerializer.Deserialize<AiConfigFile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (cfg?.Providers != null)
                Providers = cfg.Providers.Where(p => !string.IsNullOrWhiteSpace(p.ApiKey)).ToList();
        }
        catch
        {
            // 配置解析失败时忽略，仅本地解析
        }
    }

    /// <summary>是否已配置可用的 AI 提供商</summary>
    public static bool HasProvider => Providers.Count > 0;

    private class AiConfigFile
    {
        public List<AiProvider> Providers { get; set; } = new();
    }
}
