using System.Net.Http;
using System.Text;
using System.Text.Json;
using LogAnalyzer.Models;

namespace LogAnalyzer.Services;

/// <summary>
/// AI 增强解析服务：本地解析不完整时，联网调用免费 AI 工具解析日志，
/// 返回结构化的设备档案（识别码 + 固定字段 + 时序指标数值序列）。
/// 依次尝试配置的多个提供商，首个成功即返回。
/// </summary>
public class AiParserService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>AI 解析返回的中间 JSON 结构</summary>
    private class AiParseResult
    {
        public string DeviceId { get; set; } = "";
        public string IdType { get; set; } = "";
        public string Model { get; set; } = "";
        public List<KeyValue> Fields { get; set; } = new();
        public List<AiMetric> Metrics { get; set; } = new();
    }
    private class KeyValue { public string Key { get; set; } = ""; public string Value { get; set; } = ""; }
    private class AiMetric
    {
        public string Key { get; set; } = "";
        public string Unit { get; set; } = "";
        public List<double> Values { get; set; } = new();
    }

    private const string Prompt = """
你是一名嵌入式设备日志解析专家。请分析下面这段设备日志，提取以下信息并**只返回 JSON**（不要任何解释或 Markdown 代码块）：
1. deviceId：设备唯一识别码（IMEI/MAC/SN 均可）
2. idType：识别码类型，取值 imei / mac / sn
3. model：机型/型号（若能判断）
4. fields：固定设备信息数组，每项 { "key": 字段名, "value": 值 }
5. metrics：时序指标数组，每项 { "key": 指标名, "unit": 单位, "values": [数值列表] }（只列随时间变化的数值参数）

JSON 格式示例：
{"deviceId":"869223040402990","idType":"imei","model":"B203","fields":[{"key":"SW","value":"V1.0.3"}],"metrics":[{"key":"RSSI","unit":"dBm","values":[-51,-113,-68]}]}

日志内容：
""";

    /// <summary>
    /// 联网调用 AI 解析日志。返回设备档案；失败（无配置/无网络/AI 出错）返回 null。
    /// </summary>
    public async Task<DeviceProfile?> AnalyzeAsync(string fileName, string logText)
    {
        if (!AiConfig.HasProvider) return null;

        // 截取样本（前 12000 字符 + 尾部 3000 字符，避免超长）
        var sample = logText.Length <= 15000
            ? logText
            : logText.Substring(0, 12000) + "\n...(省略)...\n" + logText.Substring(logText.Length - 3000);

        foreach (var provider in AiConfig.Providers)
        {
            var profile = await TryProviderAsync(provider, sample);
            if (profile != null)
            {
                profile.SourceFiles.Add(fileName);
                return profile;
            }
        }
        return null;
    }

    private async Task<DeviceProfile?> TryProviderAsync(AiProvider provider, string sample)
    {
        try
        {
            var req = new
            {
                model = provider.Model,
                messages = new object[]
                {
                    new { role = "system", content = "你是设备日志解析专家，只返回 JSON。" },
                    new { role = "user", content = Prompt + sample }
                },
                temperature = 0.1,
                max_tokens = 2000
            };
            var json = JsonSerializer.Serialize(req);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var msg = new HttpRequestMessage(HttpMethod.Post, provider.BaseUrl.TrimEnd('/') + "/chat/completions");
            msg.Headers.Add("Authorization", "Bearer " + provider.ApiKey);
            var resp = await Http.SendAsync(msg);
            if (!resp.IsSuccessStatusCode) return null;

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0) return null;
            var contentText = choices[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(contentText)) return null;

            return ParseAiJson(contentText);
        }
        catch
        {
            return null;
        }
    }

    private DeviceProfile? ParseAiJson(string contentText)
    {
        try
        {
            // 剥离可能的 Markdown 代码块
            var start = contentText.IndexOf('{');
            var end = contentText.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            var json = contentText.Substring(start, end - start + 1);

            var r = JsonSerializer.Deserialize<AiParseResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (r == null) return null;

            var profile = new DeviceProfile
            {
                DeviceId = r.DeviceId,
                Model = string.IsNullOrWhiteSpace(r.Model) ? "AI解析" : r.Model,
                IdType = r.IdType.ToLowerInvariant() switch
                {
                    "imei" => DeviceIdType.Imei,
                    "mac" => DeviceIdType.Mac,
                    "sn" => DeviceIdType.Sn,
                    _ => DeviceIdType.Custom,
                },
            };

            foreach (var f in r.Fields)
                if (!string.IsNullOrWhiteSpace(f.Key) && !string.IsNullOrWhiteSpace(f.Value))
                    profile.Fields.Add(new DeviceField { Key = f.Key, Value = f.Value, DisplayName = f.Key });

            var baseTime = DateTime.Now;
            foreach (var m in r.Metrics)
            {
                if (string.IsNullOrWhiteSpace(m.Key) || m.Values == null || m.Values.Count == 0) continue;
                var series = new MetricSeries
                {
                    Key = m.Key.ToUpperInvariant(),
                    DisplayName = m.Key,
                    Unit = m.Unit ?? "",
                };
                for (int i = 0; i < m.Values.Count; i++)
                    series.Points.Add(new MetricPoint { Timestamp = baseTime.AddSeconds(i), Value = m.Values[i] });
                profile.Metrics.Add(series);
            }

            // AI 未给出识别码时，用本地提取链兜底
            if (string.IsNullOrWhiteSpace(profile.DeviceId))
            {
                var id = Parsers.DeviceIdExtractor.Extract(contentText);
                if (id != null)
                {
                    profile.DeviceId = id.Value.id;
                    profile.IdType = id.Value.type;
                }
            }

            return profile;
        }
        catch
        {
            return null;
        }
    }
}
