using System.Text.RegularExpressions;
using LogAnalyzer.Models;

namespace LogAnalyzer.Parsers;

/// <summary>
/// B903 音频测试日志解析器。
/// 格式：$Test Start / 声道 LED (L): ... / [通过]/[异常] 判定行
/// 固定信息：IMEI、测试结论；指标：THD、SNR、底噪、灵敏度。
/// </summary>
public class AudioParser : ILogParser
{
    public string Model => "B903";

    private static readonly Regex Imei = new(@"IMEI\s*:\s*(\d{14,16})", RegexOptions.Compiled);
    private static readonly Regex StartTime = new(@"Current Time:\s*(\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2})", RegexOptions.Compiled);
    private static readonly Regex Channel = new(@"声道\s+(\w+)\s*\((\w+)\):\s*1KHz电平\s*(-?\d+(?:\.\d+)?)dBFS", RegexOptions.Compiled);
    private static readonly Regex Thd = new(@"THD\s+(\d+(?:\.\d+)?)%", RegexOptions.Compiled);
    private static readonly Regex Noise = new(@"底噪\s*(-?\d+(?:\.\d+)?)dBFS", RegexOptions.Compiled);
    private static readonly Regex Snr = new(@"SNR\s+(\d+(?:\.\d+)?)dB", RegexOptions.Compiled);

    public bool Detect(string fileName, string sampleText)
    {
        if (fileName.Contains("Audio", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("B903", StringComparison.OrdinalIgnoreCase)) return true;
        return sampleText.Contains("$Test Start", StringComparison.OrdinalIgnoreCase)
            || sampleText.Contains("声道 LED", StringComparison.OrdinalIgnoreCase);
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

        DateTime baseTime = DateTime.Now;
        var stM = StartTime.Match(text);
        if (stM.Success && DateTime.TryParse(stM.Groups[1].Value.Replace('/', '-'), out var st))
            baseTime = st;

        var thd = new MetricSeries { Key = "THD", DisplayName = "总谐波失真", Unit = "%", UpperLimit = 5.0 };
        var snr = new MetricSeries { Key = "SNR", DisplayName = "信噪比", Unit = "dB", LowerLimit = 35.0 };
        var noise = new MetricSeries { Key = "NOISE_FLOOR", DisplayName = "本底噪声", Unit = "dBFS", UpperLimit = -65.0 };
        var sens = new MetricSeries { Key = "SENSITIVITY", DisplayName = "灵敏度", Unit = "dBFS", LowerLimit = -32.0, UpperLimit = -25.0 };

        long lines = 0;
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in text.Split('\n'))
        {
            lines++;
            var line = raw.TrimEnd('\r', '\n');

            // 测试结论
            var concl = Regex.Match(line, @"Audio Test (PASS|FAIL)");
            if (concl.Success && !fields.ContainsKey("RESULT")) fields["RESULT"] = concl.Groups[1].Value;

            var chM = Channel.Match(line);
            if (chM.Success)
            {
                var ch = chM.Groups[2].Value; // L / R
                var ts = baseTime;

                var thdM = Thd.Match(line);
                if (thdM.Success && double.TryParse(thdM.Groups[1].Value, out var thdV))
                    thd.Points.Add(new MetricPoint { Timestamp = ts, Value = thdV });

                var noiseM = Noise.Match(line);
                if (noiseM.Success && double.TryParse(noiseM.Groups[1].Value, out var noiseV))
                    noise.Points.Add(new MetricPoint { Timestamp = ts, Value = noiseV });

                var snrM = Snr.Match(line);
                if (snrM.Success && double.TryParse(snrM.Groups[1].Value, out var snrV))
                    snr.Points.Add(new MetricPoint { Timestamp = ts, Value = snrV });

                var sensM = chM.Groups[3].Value;
                if (double.TryParse(sensM, out var sensV))
                    sens.Points.Add(new MetricPoint { Timestamp = ts, Value = sensV });
            }
        }

        if (id != null && id.Value.type == DeviceIdType.Imei)
            if (!fields.ContainsKey("IMEI")) fields["IMEI"] = id.Value.id;

        foreach (var kv in fields)
            profile.Fields.Add(new DeviceField { Key = kv.Key, Value = kv.Value, DisplayName = kv.Key });

        if (thd.Count > 0) profile.Metrics.Add(thd);
        if (snr.Count > 0) profile.Metrics.Add(snr);
        if (noise.Count > 0) profile.Metrics.Add(noise);
        if (sens.Count > 0) profile.Metrics.Add(sens);

        profile.TotalLines = lines;
        profile.StartTime = profile.Metrics.SelectMany(m => m.Points).Select(p => p.Timestamp).DefaultIfEmpty().Min();
        profile.EndTime = profile.Metrics.SelectMany(m => m.Points).Select(p => p.Timestamp).DefaultIfEmpty().Max();
        if (profile.StartTime == default) { profile.StartTime = null; profile.EndTime = null; }

        return profile;
    }
}
