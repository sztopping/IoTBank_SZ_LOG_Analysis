using System.Text.RegularExpressions;
using LogAnalyzer.Models;

namespace LogAnalyzer.Parsers;

/// <summary>
/// 通用智能解析器（兜底）：对未预定义机型，读内容后自行规划解析方案。
/// 能力：
///  1. 自动探测时间戳格式（多种常见格式）；
///  2. 自动识别「字段名 = 数值 [单位]」键值模式；
///  3. 自动区分固定信息（值恒定/单次）与时序指标（值多次变化）；
///  4. 自动推断单位；
///  5. 唯一识别码走 IMEI/MAC/SN 提取链。
/// </summary>
public class UniversalSmartParser : ILogParser
{
    public string Model => "通用智能";

    // 时间戳候选（按覆盖率高者选用）
    private static readonly (Regex regex, Func<Match, DateTime?> parse)[] TsCandidates =
    {
        (new Regex(@"(\d{4})-(\d{2})-(\d{2})[ T](\d{2}):(\d{2}):(\d{2})", RegexOptions.Compiled), ParseYmdHms),
        (new Regex(@"(\d{4})/(\d{2})/(\d{2})[ T](\d{2}):(\d{2}):(\d{2})", RegexOptions.Compiled), ParseYmdHms),
        (new Regex(@"\[(\d{4})-(\d{2}):(\d{2}):(\d{2})-", RegexOptions.Compiled), ParseMddHms),
        (new Regex(@"^00>\s*\[(\d{4})-(\d{2}:\d{2}:\d{2})-", RegexOptions.Compiled), ParseMddHmsColon),
    };

    // 键值 + 数值 + 可选单位
    private static readonly Regex KvRegex = new(
        @"([A-Za-z_][A-Za-z0-9_.]*)\s*[:=]\s*(-?\d+(?:\.\d+)?)\s*([A-Za-zµ°%]{0,5})",
        RegexOptions.Compiled);

    public bool Detect(string fileName, string sampleText) => true; // 兜底，恒匹配

    public (string id, DeviceIdType type)? ExtractDeviceId(string text)
        => DeviceIdExtractor.Extract(text);

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

        // 1) 探测时间戳格式（在样本上测覆盖率）
        var sample = text.Length > 20000 ? text.Substring(0, 20000) : text;
        var tsRule = SelectTimestampRule(sample);

        // 2) 累积键值
        // key -> (出现次数, 最后值, 单位, 数值列表, 时间戳列表)
        var accum = new Dictionary<string, (int count, string lastVal, string unit, List<double> vals, List<DateTime?> times)>(StringComparer.OrdinalIgnoreCase);
        var metricOrder = new List<string>();

        long lines = 0;
        foreach (var raw in text.Split('\n'))
        {
            lines++;
            var line = raw.TrimEnd('\r', '\n');
            if (line.Length == 0) continue;

            DateTime? ts = tsRule != null ? tsRule.Value.parse(tsRule.Value.regex.Match(line)) : null;

            foreach (Match m in KvRegex.Matches(line))
            {
                var key = m.Groups[1].Value;
                var valStr = m.Groups[2].Value;
                var unit = m.Groups[3].Value;
                if (!double.TryParse(valStr, out var val)) continue;

                if (!accum.TryGetValue(key, out var e))
                {
                    e = (0, valStr, unit, new List<double>(), new List<DateTime?>());
                    metricOrder.Add(key);
                }
                e.count++;
                e.lastVal = valStr;
                if (e.unit.Length == 0 && unit.Length > 0) e.unit = unit;
                e.vals.Add(val);
                e.times.Add(ts);
                accum[key] = e;
            }
        }

        // 3) 分类：固定字段（值恒定/单次）vs 时序指标（值多次且变化）
        foreach (var key in metricOrder)
        {
            var e = accum[key];
            var distinct = e.vals.Distinct().Count();

            if (distinct <= 1)
            {
                // 固定字段
                if (!profile.Fields.Any(f => f.Key == key))
                    profile.Fields.Add(new DeviceField { Key = key, Value = e.lastVal, DisplayName = key });
            }
            else
            {
                // 时序指标
                var series = new MetricSeries
                {
                    Key = key.ToUpperInvariant(),
                    DisplayName = key,
                    Unit = e.unit,
                };
                for (int i = 0; i < e.vals.Count; i++)
                {
                    var t = e.times[i];
                    series.Points.Add(new MetricPoint
                    {
                        Timestamp = t ?? DateTime.MinValue,
                        Value = e.vals[i],
                    });
                }
                profile.Metrics.Add(series);
            }
        }

        // IMEI/MAC/SN 放入固定字段
        if (id != null)
        {
            var idKey = id.Value.type switch
            {
                DeviceIdType.Imei => "IMEI",
                DeviceIdType.Mac => "MAC",
                DeviceIdType.Sn => "SN",
                _ => "ID",
            };
            if (!profile.Fields.Any(f => f.Key == idKey))
                profile.Fields.Add(new DeviceField { Key = idKey, Value = id.Value.id, DisplayName = idKey });
        }

        profile.TotalLines = lines;
        profile.StartTime = profile.Metrics.SelectMany(m => m.Points).Select(p => p.Timestamp).Where(t => t != DateTime.MinValue).DefaultIfEmpty().Min();
        profile.EndTime = profile.Metrics.SelectMany(m => m.Points).Select(p => p.Timestamp).Where(t => t != DateTime.MinValue).DefaultIfEmpty().Max();
        if (profile.StartTime == default) { profile.StartTime = null; profile.EndTime = null; }

        return profile;
    }

    /// <summary>选择覆盖率最高的时间戳规则</summary>
    private static (Regex regex, Func<Match, DateTime?> parse)? SelectTimestampRule(string sample)
    {
        var lines = sample.Split('\n').Where(l => l.Trim().Length > 0).Take(200).ToList();
        if (lines.Count < 3) return null;

        (Regex regex, Func<Match, DateTime?> parse)? best = null;
        int bestScore = 0;
        foreach (var c in TsCandidates)
        {
            int hit = lines.Count(l => c.regex.IsMatch(l));
            if (hit > bestScore && hit * 2 >= lines.Count)
            {
                bestScore = hit;
                best = c;
            }
        }
        return best;
    }

    private static DateTime? ParseYmdHms(Match m)
    {
        if (DateTime.TryParse($"{m.Groups[1].Value}-{m.Groups[2].Value}-{m.Groups[3].Value} {m.Groups[4].Value}:{m.Groups[5].Value}:{m.Groups[6].Value}", out var dt))
            return dt;
        return null;
    }

    private static DateTime? ParseMddHms(Match m)
    {
        var mmdd = m.Groups[1].Value;
        var year = DateTime.Now.Year;
        if (int.TryParse(mmdd.Substring(0, 2), out var mo) &&
            int.TryParse(mmdd.Substring(2, 2), out var dy) &&
            DateTime.TryParse($"{year}-{mo:00}-{dy:00} {m.Groups[2].Value}:{m.Groups[3].Value}:{m.Groups[4].Value}", out var dt))
            return dt;
        return null;
    }

    private static DateTime? ParseMddHmsColon(Match m)
    {
        var mmdd = m.Groups[1].Value;
        var year = DateTime.Now.Year;
        var hms = m.Groups[2].Value; // HH:mm:ss
        if (int.TryParse(mmdd.Substring(0, 2), out var mo) &&
            int.TryParse(mmdd.Substring(2, 2), out var dy) &&
            DateTime.TryParse($"{year}-{mo:00}-{dy:00} {hms}", out var dt))
            return dt;
        return null;
    }
}
