using System.Text.RegularExpressions;
using LogAnalyzer.Models;

namespace LogAnalyzer.Parsers;

/// <summary>
/// B504 可靠性测试上位机日志解析器（被动接收 log）。
/// 格式：>> [MMdd-HH:mm:ss-ddd:HH:mm:ss.mmm] 内容
/// 时序：sht4x 温湿度、震动唤醒计数、看门狗计数等；固定信息：IMEI + Fs.* 配置。
/// </summary>
public class B504Parser : ILogParser
{
    public string Model => "B504";

    // 时间戳：[1015-15:57:19-003:56:30.978]
    private static readonly Regex Ts = new(@"^>>\s*\[(\d{4})-(\d{2}:\d{2}:\d{2})-\d{3}:\d{2}:\d{2}\.\d+\]", RegexOptions.Compiled);
    private static readonly Regex FsKv = new(@"Fs(?:Common)?\.([A-Za-z0-9_]+):\s*(.*)$", RegexOptions.Compiled);
    private static readonly Regex Temper = new(@"Temper:\s*(-?\d+(?:\.\d+)?)C", RegexOptions.Compiled);
    private static readonly Regex Humidity = new(@"Humidity:\s*(-?\d+(?:\.\d+)?)%", RegexOptions.Compiled);

    public bool Detect(string fileName, string sampleText)
    {
        if (fileName.Contains("B504", StringComparison.OrdinalIgnoreCase)) return true;
        return sampleText.Contains("Recive debug data", StringComparison.OrdinalIgnoreCase)
            || (sampleText.Contains("Fs.Ok:", StringComparison.OrdinalIgnoreCase)
                && sampleText.Contains("sht4x", StringComparison.OrdinalIgnoreCase));
    }

    public (string id, DeviceIdType type)? ExtractDeviceId(string text)
    {
        // 优先真 IMEI（B504 有 IMEI:353544730584695）
        var imei = DeviceIdExtractor.ExtractImei(text);
        if (imei != null) return imei;
        return DeviceIdExtractor.Extract(text);
    }

    public DeviceProfile Parse(string fileName, string text)
    {
        var profile = new DeviceProfile { Model = Model };
        profile.SourceFiles.Add(fileName);

        var id = ExtractDeviceId(text);
        if (id != null)
        {
            profile.DeviceId = id.Value.id;
            profile.IdType = id.Value.type;
        }

        // 从文件名推断年份基准（如 20261015）
        int year = DateTime.Now.Year;
        var yM = Regex.Match(fileName, @"(20\d{2})(\d{2})(\d{2})");
        if (yM.Success) year = int.Parse(yM.Groups[1].Value);

        var temp = new MetricSeries { Key = "TEMPERATURE", DisplayName = "温度", Unit = "°C" };
        var humid = new MetricSeries { Key = "HUMIDITY", DisplayName = "湿度", Unit = "%" };
        var shock = new MetricSeries { Key = "SHOCK_WAKEUP", DisplayName = "震动唤醒计数", Unit = "次" };
        var wdog = new MetricSeries { Key = "WATCHDOG_FEED", DisplayName = "看门狗喂狗计数", Unit = "次" };

        long lines = 0;
        DateTime? cur = null;
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in text.Split('\n'))
        {
            lines++;
            var line = raw.TrimEnd('\r', '\n');

            var tsM = Ts.Match(line);
            if (tsM.Success)
            {
                // MMdd + HH:mm:ss + 推断年份
                var mmdd = tsM.Groups[1].Value;
                var hms = tsM.Groups[2].Value;
                if (int.TryParse(mmdd.Substring(0, 2), out var mo) &&
                    int.TryParse(mmdd.Substring(2, 2), out var dy) &&
                    DateTime.TryParse($"{year}-{mo:00}-{dy:00} {hms}", out var dt))
                    cur = dt;
            }

            // Fs.* 配置字段（固定信息）
            var fsM = FsKv.Match(line);
            if (fsM.Success)
            {
                var k = fsM.Groups[1].Value.Trim();
                var v = fsM.Groups[2].Value.Trim();
                if (k.Length > 0 && v.Length > 0 && !fields.ContainsKey(k))
                    fields[k] = v;
            }

            if (cur != null)
            {
                // 温湿度：sht4x Temper: 31.7C, Fix Temper: 29.9C; Humidity: 41.8%, ...
                var tM = Temper.Match(line);
                if (tM.Success && double.TryParse(tM.Groups[1].Value, out var tv))
                    temp.Points.Add(new MetricPoint { Timestamp = cur.Value, Value = tv });

                var hM = Humidity.Match(line);
                if (hM.Success && double.TryParse(hM.Groups[1].Value, out var hv))
                    humid.Points.Add(new MetricPoint { Timestamp = cur.Value, Value = hv });

                // 震动唤醒计数：Fs.ShockWakeupCnts: 3
                var sM = Regex.Match(line, @"ShockWakeupCnts:\s*(\d+)");
                if (sM.Success && double.TryParse(sM.Groups[1].Value, out var sv))
                    shock.Points.Add(new MetricPoint { Timestamp = cur.Value, Value = sv });

                // 看门狗：Flag.WatchDogFeedCnt: 33
                var wM = Regex.Match(line, @"WatchDogFeedCnt:\s*(\d+)");
                if (wM.Success && double.TryParse(wM.Groups[1].Value, out var wv))
                    wdog.Points.Add(new MetricPoint { Timestamp = cur.Value, Value = wv });
            }
        }

        // IMEI 放入固定字段
        if (id != null && id.Value.type == DeviceIdType.Imei)
            if (!fields.ContainsKey("IMEI")) fields["IMEI"] = id.Value.id;

        foreach (var kv in fields)
            profile.Fields.Add(new DeviceField { Key = kv.Key, Value = kv.Value, DisplayName = kv.Key });

        if (temp.Count > 0) profile.Metrics.Add(temp);
        if (humid.Count > 0) profile.Metrics.Add(humid);
        if (shock.Count > 0) profile.Metrics.Add(shock);
        if (wdog.Count > 0) profile.Metrics.Add(wdog);

        profile.TotalLines = lines;
        profile.StartTime = profile.Metrics.SelectMany(m => m.Points).Select(p => p.Timestamp).DefaultIfEmpty().Min();
        profile.EndTime = profile.Metrics.SelectMany(m => m.Points).Select(p => p.Timestamp).DefaultIfEmpty().Max();
        if (profile.StartTime == default) { profile.StartTime = null; profile.EndTime = null; }

        return profile;
    }
}
