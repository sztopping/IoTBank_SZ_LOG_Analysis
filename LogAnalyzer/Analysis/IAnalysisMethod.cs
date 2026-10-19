using LogAnalyzer.Models;

namespace LogAnalyzer.Analysis;

/// <summary>
/// 统计分析方法接口。每个方法自带适用性判断（CanApply），
/// 引擎遍历所有方法，按 log 数据形态自动决定调用哪些。
/// </summary>
public interface IAnalysisMethod
{
    /// <summary>方法稳定标识</summary>
    string MethodKey { get; }

    /// <summary>方法名（三语，用于报告章节标题）</summary>
    LocalizedText DisplayName { get; }

    /// <summary>判断当前数据集是否适用本方法</summary>
    bool CanApply(IReadOnlyList<DeviceProfile> devices);

    /// <summary>执行分析，返回一个或多个章节</summary>
    IEnumerable<AnalysisSection> Analyze(IReadOnlyList<DeviceProfile> devices);
}
