using System.Text.RegularExpressions;
using LogAnalyzer.Models;

namespace LogAnalyzer.Parsers;

/// <summary>
/// B509ES（nRF Connect SDK / Zephyr 固件）日志解析器。
/// 格式：00> [MMdd-HH:mm:ss-ddd:HH:mm:ss.mmm] &lt;inf&gt; 模块: 消息
/// 固定信息：IMEI、SW 版本、SDK、OS、UniqueID、Gsensor 类型；时序：电池电压。
/// </summary>
public class B509EsParser : ILogParser
{
    public string Model => "B509ES";

    private static readonly Regex Ts = new(@"^00>\s*\[(\d{4})-(\d{2}:\d{2}:\d{2})-\d{3}:\d{2}:\d{2}\.\d+\]", RegexOptions.Compiled);
    private static readonly Regex Imei = new(@"imei:\s*(\d{14,16})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SwVer = new(@"====+\s*([A-Za-z0-9_.]+)\s*====+", RegexOptions.Compiled);
    private static readonly Regex SdkVer = new(@"Booting nRF Connect SDK v([^\s]+)", RegexOptions.Compiled);
    private static readonly Regex OsVer = new(@"Using Zephyr OS v([^\s]+)", RegexOptions.Compiled);
    private static readonly Regex UniqueId = new(@"Unique ID \(0x4B\)\s*:\s*([0-9A-Fa-f]+)", RegexOptions.Compiled);
    private static readonly Regex Gsensor = new(@"Gsensor type:\s*([^\s]+)", RegexOptions.Compiled);
    private static readonly Regex Battery = new(@"Battery voltage:\s*(\d+)\s*mv", RegexOptions.Compiled);

    public bool Detect(string fileName, string sampleText)
    {
        if (fileName.Contains("B509", StringComparison.OrdinalIgnoreCase)) return true;
        return sampleText.Contains("Booting nRF Connect SDK", StringComparison.OrdinalIgnoreCase)
            || sampleText.Contains("Zephyr OS", StringComparison.OrdinalIgnoreCase);
    }

    public (string id, DeviceIdType type)? ExtractDeviceId(string text)
    {
        var m = Imei.Match(text);
        if (m.Success) return (m.Groups[1].Value, DeviceIdType.Imei);
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

        int year = DateTime.Now.Year;
        var battery = new MetricSeries { Key = "BATTERY_VOLTAGE", DisplayName = "电池电压", Unit = "mV" };

        long lines = 0;
        DateTime? cur = null;
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 一次性固定字段匹配
        void AddField(string key, Match m)
        {
            if (m.Success && !fields.ContainsKey(key))
                fields[key] = m.Groups[1].Value.Trim();
        }

        AddField("SDK", SdkVer.Match(text));
        AddField("OS", OsVer.Match(text));
        AddField("UNIQUE_ID", UniqueId.Match(text));
        AddField("GSENSOR", Gsensor.Match(text));

        var swM = SwVer.Match(text);
        // SW 版本行形如 "============ B509IB_V9.5.1_T12 ============"
        if (swM.Success && swM.Groups[1].Value.Contains('.'))
            if (!fields.ContainsKey("SW")) fields["SW"] = swM.Groups[1].Value;

        if (id != null && id.Value.type == DeviceIdType.Imei)
            if (!fields.ContainsKey("IMEI")) fields["IMEI"] = id.Value.id;

        foreach (var raw in text.Split('\n'))
        {
            lines++;
            var line = raw.TrimEnd('\r', '\n');

            var tsM = Ts.Match(line);
            if (tsM.Success)
            {
                var mmdd = tsM.Groups[1].Value;
                var hms = tsM.Groups[2].Value;
                if (int.TryParse(mmdd.Substring(0, 2), out var mo) &&
                    int.TryParse(mmdd.Substring(2, 2), out var dy) &&
                    DateTime.TryParse($"{year}-{mo:00}-{dy:00} {hms}", out var dt))
                    cur = dt;
            }

            if (cur != null)
            {
                var bM = Battery.Match(line);
                if (bM.Success && double.TryParse(bM.Groups[1].Value, out var bv))
                    battery.Points.Add(new MetricPoint { Timestamp = cur.Value, Value = bv });
            }
        }

        foreach (var kv in fields)
            profile.Fields.Add(new DeviceField { Key = kv.Key, Value = kv.Value, DisplayName = kv.Key });

        if (battery.Count > 0) profile.Metrics.Add(battery);

        profile.TotalLines = lines;
        profile.StartTime = profile.Metrics.SelectMany(m => m.Points).Select(p => p.Timestamp).DefaultIfEmpty().Min();
        profile.EndTime = profile.Metrics.SelectMany(m => m.Points).Select(p => p.Timestamp).DefaultIfEmpty().Max();
        if (profile.StartTime == default) { profile.StartTime = null; profile.EndTime = null; }

        return profile;
    }
}
