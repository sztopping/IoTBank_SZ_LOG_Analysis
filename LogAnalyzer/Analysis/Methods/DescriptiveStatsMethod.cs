using LogAnalyzer.Models;

namespace LogAnalyzer.Analysis.Methods;

/// <summary>
/// 描述性统计 + 指标时序图（核心方法）。
/// 对每个设备的每个指标：生成描述统计表 + 时间序列折线图；
/// 多设备时追加箱线图对比。
/// </summary>
public class DescriptiveStatsMethod : IAnalysisMethod
{
    public string MethodKey => "descriptive";
    public LocalizedText DisplayName => LocalizedText.Of("描述性统计", "記述統計", "Descriptive statistics");

    public bool CanApply(IReadOnlyList<DeviceProfile> devices)
        => devices.Any(d => d.Metrics.Any(m => m.Count > 0));

    public IEnumerable<AnalysisSection> Analyze(IReadOnlyList<DeviceProfile> devices)
    {
        var deviceList = devices.Where(d => d.Metrics.Any(m => m.Count > 0)).ToList();
        if (deviceList.Count == 0) yield break;

        // 1) 每设备每指标：描述统计表
        foreach (var dev in deviceList)
        {
            var section = new AnalysisSection
            {
                SectionKey = $"descriptive_{dev.DeviceId}",
                Title = LocalizedText.Of(
                    $"描述性统计 — 设备 {dev.DeviceId}",
                    $"記述統計 — デバイス {dev.DeviceId}",
                    $"Descriptive statistics — Device {dev.DeviceId}")
            };

            // 描述统计表
            var table = new TableSpec
            {
                Title = LocalizedText.Of("指标描述统计", "指標の記述統計", "Metric descriptive statistics"),
                Headers = new List<LocalizedText>
                {
                    LocalizedText.Of("指标", "指標", "Metric"),
                    LocalizedText.Of("样本量", "サンプル数", "N"),
                    LocalizedText.Of("均值", "平均", "Mean"),
                    LocalizedText.Of("中位数", "中央値", "Median"),
                    LocalizedText.Of("标准差", "標準偏差", "Std"),
                    LocalizedText.Of("最小值", "最小値", "Min"),
                    LocalizedText.Of("最大值", "最大値", "Max"),
                    LocalizedText.Of("变异系数%", "変動係数%", "CV%"),
                }
            };

            foreach (var metric in dev.Metrics.Where(m => m.Count > 0))
            {
                var s = Stats.Describe(metric.Points.Select(p => p.Value));
                table.Rows.Add(new List<string>
                {
                    metric.DisplayName,
                    s.Count.ToString(),
                    Fmt(s.Mean), Fmt(s.Median), Fmt(s.StdDev), Fmt(s.Min), Fmt(s.Max), Fmt(s.Cv)
                });
            }
            section.Tables.Add(table);

            // 每指标时间序列折线图
            foreach (var metric in dev.Metrics.Where(m => m.Count > 0))
            {
                var chart = new ChartSpec
                {
                    ChartId = $"ts_{dev.DeviceId}_{metric.Key}",
                    Type = "line",
                    Title = LocalizedText.Of(
                        $"{metric.DisplayName} — {dev.DeviceId}",
                        $"{metric.DisplayName} — {dev.DeviceId}",
                        $"{metric.DisplayName} — {dev.DeviceId}"),
                    XAxis = new AxisSpec { Type = "time", Name = LocalizedText.Of("时间", "時間", "Time") },
                    YAxis = new AxisSpec { Type = "value", Name = new LocalizedText { Zh = metric.DisplayName, Ja = metric.DisplayName, En = metric.DisplayName }, Unit = metric.Unit },
                    Series = new List<SeriesSpec>
                    {
                        new SeriesSpec
                        {
                            Name = dev.DeviceId,
                            Type = "line",
                            Data = metric.Points.Select(p => new object[] { p.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), Math.Round(p.Value, 4) }).ToList()
                        }
                    }
                };
                section.Charts.Add(chart);
            }

            // 结论
            section.Conclusions.Add(BuildConclusion(dev));
            yield return section;
        }

        // 2) 多设备同指标箱线图对比
        if (deviceList.Count >= 2)
        {
            var commonMetrics = deviceList
                .SelectMany(d => d.Metrics)
                .GroupBy(m => m.Key)
                .Where(g => g.Select(m => m.Count).Distinct().Count() == 1 || g.Count() >= 2)
                .Where(g => g.Count() >= 2)
                .Select(g => g.Key)
                .ToList();

            foreach (var metricKey in commonMetrics)
            {
                var sampleMetric = deviceList.Select(d => d.GetMetric(metricKey)).First(m => m != null)!;
                var chart = new ChartSpec
                {
                    ChartId = $"box_{metricKey}",
                    Type = "boxplot",
                    Title = LocalizedText.Of(
                        $"{sampleMetric.DisplayName} 设备间对比",
                        $"{sampleMetric.DisplayName} デバイス間比較",
                        $"{sampleMetric.DisplayName} across devices"),
                    XAxis = new AxisSpec { Type = "category", Name = LocalizedText.Of("设备", "デバイス", "Device") },
                    YAxis = new AxisSpec { Type = "value", Name = new LocalizedText { Zh = sampleMetric.DisplayName, Ja = sampleMetric.DisplayName, En = sampleMetric.DisplayName }, Unit = sampleMetric.Unit },
                    XCategories = deviceList.Select(d => d.DeviceId).ToList(),
                    Series = new List<SeriesSpec>
                    {
                        new SeriesSpec
                        {
                            Name = sampleMetric.DisplayName,
                            Type = "boxplot",
                            Data = deviceList
                                .Select(d => d.GetMetric(metricKey))
                                .Where(m => m != null && m.Count >= 2)
                                .Select(m => BoxData(m!.Points.Select(p => p.Value)))
                                .ToList()
                        }
                    }
                };

                var section = new AnalysisSection
                {
                    SectionKey = $"box_{metricKey}",
                    Title = LocalizedText.Of(
                        $"{sampleMetric.DisplayName} 设备间分布对比",
                        $"{sampleMetric.DisplayName} デバイス間分布比較",
                        $"{sampleMetric.DisplayName} distribution comparison"),
                    Charts = { chart }
                };
                yield return section;
            }
        }
    }

    private static object[] BoxData(IEnumerable<double> values)
    {
        var v = values.OrderBy(x => x).ToList();
        double q1 = Stats.Quantile(v, 0.25), q3 = Stats.Quantile(v, 0.75);
        double med = Stats.Quantile(v, 0.5);
        return new object[] { v.Min(), q1, med, q3, v.Max() };
    }

    private static LocalizedText BuildConclusion(DeviceProfile dev)
    {
        var parts = new List<string>();
        foreach (var m in dev.Metrics.Where(m => m.Count > 0))
        {
            var s = Stats.Describe(m.Points.Select(p => p.Value));
            parts.Add($"{m.DisplayName}：均值 {Fmt(s.Mean)}{m.Unit}，标准差 {Fmt(s.StdDev)}，波动 {Fmt(s.Cv)}%");
        }
        var zh = string.Join("；", parts) + "。";
        return LocalizedText.Of(zh, zh, zh);
    }

    private static string Fmt(double v) => Math.Round(v, 4).ToString("0.####");
}
