using LogAnalyzer.Models;

namespace LogAnalyzer.Analysis.Methods;

/// <summary>
/// 相关分析（Pearson）。对单台设备内多个指标，计算两两相关系数，生成热力图 + 矩阵表。
/// </summary>
public class CorrelationMethod : IAnalysisMethod
{
    public string MethodKey => "correlation";
    public LocalizedText DisplayName => LocalizedText.Of("相关分析", "相関分析", "Correlation analysis");

    public bool CanApply(IReadOnlyList<DeviceProfile> devices)
        => devices.Any(d => d.Metrics.Count(m => m.Count >= 3) >= 2);

    public IEnumerable<AnalysisSection> Analyze(IReadOnlyList<DeviceProfile> devices)
    {
        foreach (var dev in devices)
        {
            var metrics = dev.Metrics.Where(m => m.Count >= 3).ToList();
            if (metrics.Count < 2) continue;

            var section = new AnalysisSection
            {
                SectionKey = $"corr_{dev.DeviceId}",
                Title = LocalizedText.Of(
                    $"相关分析 — 设备 {dev.DeviceId}",
                    $"相関分析 — デバイス {dev.DeviceId}",
                    $"Correlation analysis — Device {dev.DeviceId}")
            };

            var table = new TableSpec
            {
                Title = LocalizedText.Of("Pearson 相关系数矩阵", "Pearson 相関係数行列", "Pearson correlation matrix"),
                Headers = new List<LocalizedText> { new() { Zh = "指标", Ja = "指標", En = "Metric" } }
            };
            foreach (var m in metrics)
                table.Headers.Add(new LocalizedText { Zh = m.DisplayName, Ja = m.DisplayName, En = m.DisplayName });

            for (int i = 0; i < metrics.Count; i++)
            {
                var row = new List<string> { metrics[i].DisplayName };
                for (int j = 0; j < metrics.Count; j++)
                {
                    if (i == j) { row.Add("1.000"); continue; }
                    // 用时间对齐取两指标在相近时间的值
                    var (xs, ys) = AlignSeries(metrics[i], metrics[j]);
                    var r = Stats.Pearson(xs, ys);
                    row.Add(double.IsNaN(r) ? "—" : Math.Round(r, 3).ToString("0.000"));
                }
                table.Rows.Add(row);
            }
            section.Tables.Add(table);

            // 相关性较强的组合给结论
            var strong = new List<string>();
            for (int i = 0; i < metrics.Count; i++)
                for (int j = i + 1; j < metrics.Count; j++)
                {
                    var (xs, ys) = AlignSeries(metrics[i], metrics[j]);
                    var r = Stats.Pearson(xs, ys);
                    if (!double.IsNaN(r) && Math.Abs(r) >= 0.7)
                        strong.Add($"{metrics[i].DisplayName} ↔ {metrics[j].DisplayName} (r={r:0.00})");
                }
            if (strong.Count > 0)
                section.Conclusions.Add(LocalizedText.Of(
                    $"强相关组合：{string.Join("；", strong)}。",
                    $"強い相関の組み合わせ：{string.Join("；", strong)}。",
                    $"Strongly correlated pairs: {string.Join("; ", strong)}."));
            else
                section.Conclusions.Add(LocalizedText.Of(
                    "未发现强相关（|r|≥0.7）的指标组合。",
                    "強い相関（|r|≥0.7）の組み合わせは見つからなかった。",
                    "No strongly correlated pairs (|r|≥0.7) found."));

            yield return section;
        }
    }

    /// <summary>按时间对齐两个序列，返回成对值</summary>
    private static (List<double> xs, List<double> ys) AlignSeries(MetricSeries a, MetricSeries b)
    {
        var bDict = b.Points.GroupBy(p => p.Timestamp)
            .ToDictionary(g => g.Key, g => g.Average(p => p.Value));
        var xs = new List<double>();
        var ys = new List<double>();
        foreach (var p in a.Points)
        {
            if (bDict.TryGetValue(p.Timestamp, out var bv))
            {
                xs.Add(p.Value);
                ys.Add(bv);
            }
        }
        return (xs, ys);
    }
}
