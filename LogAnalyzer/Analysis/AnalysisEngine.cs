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
    public ReportData Run(IReadOnlyList<DeviceProfile> devices, string? requirement = null)
    {
        var report = new ReportData { Devices = devices.ToList() };

        // 需求解析：根据用户输入的需求文本，自行判断调用哪些分析方法
        var plannedKeys = RequirementPlanner.Plan(requirement);
        report.Requirement = requirement;
        report.RequirementSummary = RequirementPlanner.Summarize(requirement, plannedKeys);

        foreach (var method in _methods)
        {
            // 指定了需求且当前方法不在计划内 → 跳过
            if (plannedKeys != null && !plannedKeys.Contains(method.MethodKey)) continue;
            if (!method.CanApply(devices)) continue;
            report.Sections.AddRange(method.Analyze(devices));
        }
        return report;
    }
}
