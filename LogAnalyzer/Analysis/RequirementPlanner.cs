using LogAnalyzer.Models;

namespace LogAnalyzer.Analysis;

/// <summary>
/// 需求解析器：根据用户输入的分析需求文本，自行判断需要调用哪些分析方法。
/// 采用「本地关键词匹配」为主（离线可靠），匹配规则：
///   趋势/变化/随时间 → 时间序列趋势
///   相关/关联/影响   → 相关分析
///   对比/比较/差异   → 描述统计（含多设备对比）
///   统计/均值/分布   → 描述统计
/// 需求为空或未命中任何关键词 → 返回 null，表示全自动（运行所有适用方法）。
/// </summary>
public static class RequirementPlanner
{
    private static readonly (string key, string[] words)[] Rules =
    {
        ("trend", new[] { "趋势", "变化", "随时间", "走势", "上升", "下降", "漂移", "递增", "递减", "稳定" }),
        ("correlation", new[] { "相关", "关联", "影响", "关系", "相关系数", "耦合" }),
        ("descriptive", new[] { "对比", "比较", "差异", "哪个", "谁更", "设备间", "统计", "平均", "均值", "分布", "波动", "极值", "中位数", "标准差", "描述", "离散" }),
    };

    /// <summary>
    /// 解析需求，返回应调用的方法 key 集合；返回 null 表示全自动。
    /// </summary>
    public static List<string>? Plan(string? requirement)
    {
        if (string.IsNullOrWhiteSpace(requirement)) return null;

        var keys = new List<string>();
        foreach (var (key, words) in Rules)
        {
            if (words.Any(w => requirement.Contains(w, StringComparison.OrdinalIgnoreCase)))
                if (!keys.Contains(key)) keys.Add(key);
        }

        // 命中「对比/统计」但未命中趋势/相关时，额外给描述统计补时序图，已含在 descriptive 内
        return keys.Count == 0 ? null : keys;
    }

    /// <summary>生成需求分析的结果摘要（用于报告首页展示用户需求）</summary>
    public static string Summarize(string? requirement, List<string>? keys)
    {
        if (string.IsNullOrWhiteSpace(requirement))
            return "未指定分析需求，按数据形态自动匹配全部适用分析方法。";

        var methodNames = new List<string>();
        if (keys != null)
        {
            foreach (var k in keys)
                methodNames.Add(k switch
                {
                    "trend" => "时间序列趋势",
                    "correlation" => "相关分析",
                    "descriptive" => "描述性统计",
                    _ => k
                });
        }
        return methodNames.Count > 0
            ? $"分析需求：「{requirement}」→ 匹配方法：{string.Join("、", methodNames)}。"
            : $"分析需求：「{requirement}」未命中特定方法，按数据形态自动匹配全部适用分析方法。";
    }
}
