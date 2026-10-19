using LogAnalyzer.Models;

namespace LogAnalyzer.Analysis.Methods;

/// <summary>
/// 时间序列趋势分析。对每个指标的时序做线性回归，判断上升/下降/稳定趋势，并给出异常点。
/// </summary>
public class TrendMethod : IAnalysisMethod
{
    public string MethodKey => "trend";
    public LocalizedText DisplayName => LocalizedText.Of("时间序列趋势", "時系列トレンド", "Time-series trend");

    public bool CanApply(IReadOnlyList<DeviceProfile> devices)
        => devices.Any(d => d.Metrics.Any(m => m.Count >= 10));

    public IEnumerable<AnalysisSection> Analyze(IReadOnlyList<DeviceProfile> devices)
    {
        foreach (var dev in devices)
        {
            var metrics = dev.Metrics.Where(m => m.Count >= 10).ToList();
            if (metrics.Count == 0) continue;

            var section = new AnalysisSection
            {
                SectionKey = $"trend_{dev.DeviceId}",
                Title = LocalizedText.Of(
                    $"时间序列趋势 — 设备 {dev.DeviceId}",
                    $"時系列トレンド — デバイス {dev.DeviceId}",
                    $"Time-series trend — Device {dev.DeviceId}")
            };

            var table = new TableSpec
            {
                Title = LocalizedText.Of("趋势判定", "トレンド判定", "Trend assessment"),
                Headers = new List<LocalizedText>
                {
                    LocalizedText.Of("指标", "指標", "Metric"),
                    LocalizedText.Of("斜率", "傾き", "Slope"),
                    LocalizedText.Of("R²", "R²", "R²"),
                    LocalizedText.Of("趋势", "トレンド", "Trend"),
                }
            };

            foreach (var metric in metrics)
            {
                var pts = metric.Points.OrderBy(p => p.Timestamp).ToList();
                var x = pts.Select((p, i) => (double)i).ToList();
                var y = pts.Select(p => p.Value).ToList();
                var (slope, _, r2) = Stats.LinearRegression(x, y);

                string trend;
                if (Math.Abs(slope) < Math.Max(0.001, Stats.StdDev(y) * 0.0005))
                    trend = LocalizedText.Of("稳定", "安定", "Stable").Zh;
                else if (slope > 0)
                    trend = LocalizedText.Of("上升", "上昇", "Rising").Zh;
                else
                    trend = LocalizedText.Of("下降", "下降", "Falling").Zh;

                table.Rows.Add(new List<string>
                {
                    metric.DisplayName,
                    Math.Round(slope, 5).ToString("0.00000"),
                    Math.Round(r2, 3).ToString("0.000"),
                    trend
                });

                // 带趋势线的图表
                var seriesData = pts.Select(p => new object[] { p.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), Math.Round(p.Value, 4) }).ToList();
                section.Charts.Add(new ChartSpec
                {
                    ChartId = $"trend_{dev.DeviceId}_{metric.Key}",
                    Type = "line",
                    Title = LocalizedText.Of(
                        $"{metric.DisplayName} 趋势 — {dev.DeviceId}",
                        $"{metric.DisplayName} トレンド — {dev.DeviceId}",
                        $"{metric.DisplayName} trend — {dev.DeviceId}"),
                    XAxis = new AxisSpec { Type = "time", Name = LocalizedText.Of("时间", "時間", "Time") },
                    YAxis = new AxisSpec { Type = "value", Name = new LocalizedText { Zh = metric.DisplayName, Ja = metric.DisplayName, En = metric.DisplayName }, Unit = metric.Unit },
                    Series = new List<SeriesSpec> { new SeriesSpec { Name = metric.DisplayName, Type = "line", Data = seriesData } }
                });
            }

            section.Tables.Add(table);
            yield return section;
        }
    }
}
