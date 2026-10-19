using LogAnalyzer.Analysis.Methods;
using LogAnalyzer.Models;

namespace LogAnalyzer.Analysis;

/// <summary>
/// 分析引擎：注册所有统计方法，遍历后按数据形态自适应调用（CanApply）。
/// </summary>
public class AnalysisEngine
{
    private readonly List<IAnalysisMethod> _methods;

    public AnalysisEngine()
    {
        _methods = new List<IAnalysisMethod>
        {
            new DescriptiveStatsMethod(),
            new CorrelationMethod(),
            new TrendMethod(),
            // 问卷/量表型方法：仅在数据含主观评分形态时自适应触发，log 客观参数默认不触发
            new KanoMethod(),
            new SemMediationMethod(),
        };
    }

    public IReadOnlyList<IAnalysisMethod> Methods => _methods;

    /// <summary>
    /// 对设备列表执行分析，返回报告数据（含所有适用章节）。
    /// </summary>
    public ReportData Run(IReadOnlyList<DeviceProfile> devices)
    {
        var report = new ReportData { Devices = devices.ToList() };
        foreach (var method in _methods)
        {
            if (!method.CanApply(devices)) continue;
            report.Sections.AddRange(method.Analyze(devices));
        }
        return report;
    }
}
