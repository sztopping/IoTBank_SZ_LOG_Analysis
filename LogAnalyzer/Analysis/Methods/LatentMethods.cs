using LogAnalyzer.Models;

namespace LogAnalyzer.Analysis.Methods;

/// <summary>
/// KANO 模型（用户满意度分类）。本质是问卷量表方法，
/// 仅当数据包含「功能 + 正向/反向满意度评分」形态时自适应触发。
/// log 客观参数（RSSI/温度/电压等）不含该形态，默认不调用。
/// </summary>
public class KanoMethod : IAnalysisMethod
{
    public string MethodKey => "kano";
    public LocalizedText DisplayName => LocalizedText.Of("KANO 模型", "KANO モデル", "KANO model");

    private static readonly string[] ScoreKeys = { "satisfaction", "positive_score", "negative_score", "functional", "dysfunctional" };

    public bool CanApply(IReadOnlyList<DeviceProfile> devices)
        => devices.Any(d => d.Metrics.Any(m => ScoreKeys.Any(k => m.Key.Contains(k, StringComparison.OrdinalIgnoreCase))));

    public IEnumerable<AnalysisSection> Analyze(IReadOnlyList<DeviceProfile> devices)
    {
        // 预留：数据形态匹配时在此实现 KANO 分类（魅力/期望/必备/无差异/反向属性）
        yield break;
    }
}

/// <summary>
/// 结构方程模型 / 中介效应。本质是潜变量方法，
/// 仅当数据含「显变量 + 潜变量结构」形态时自适应触发。
/// </summary>
public class SemMediationMethod : IAnalysisMethod
{
    public string MethodKey => "sem_mediation";
    public LocalizedText DisplayName => LocalizedText.Of("结构方程 / 中介效应", "構造方程式 / 媒介効果", "SEM / Mediation");

    private static readonly string[] LatentKeys = { "latent", "mediation", "independent_var", "mediator", "dependent_var" };

    public bool CanApply(IReadOnlyList<DeviceProfile> devices)
        => devices.Any(d => d.Metrics.Any(m => LatentKeys.Any(k => m.Key.Contains(k, StringComparison.OrdinalIgnoreCase))));

    public IEnumerable<AnalysisSection> Analyze(IReadOnlyList<DeviceProfile> devices)
    {
        yield break;
    }
}
