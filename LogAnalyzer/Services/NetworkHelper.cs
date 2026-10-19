using System.Net.Http;

namespace LogAnalyzer.Services;

/// <summary>
/// 联网检测工具：判断电脑当前是否可访问互联网。
/// 用于「需联网功能」的前置提示（如未来 AI 联网智能解析）。
/// </summary>
public static class NetworkHelper
{
    /// <summary>
    /// 异步检测是否联网（3 秒超时）。
    /// 通过访问微软连接测试端点判断，无 DNS 劫持风险。
    /// </summary>
    public static async Task<bool> IsOnlineAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var resp = await client.GetAsync("https://www.msftconnecttest.com/connecttest.txt");
            if (!resp.IsSuccessStatusCode) return false;
            var body = await resp.Content.ReadAsStringAsync();
            return body.Contains("Microsoft Connect Test", StringComparison.OrdinalIgnoreCase)
                || body.Trim() == "Microsoft Connect Test";
        }
        catch
        {
            return false;
        }
    }
}
