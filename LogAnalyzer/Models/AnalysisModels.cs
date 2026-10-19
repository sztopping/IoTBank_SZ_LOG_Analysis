namespace LogAnalyzer.Models;

/// <summary>
/// 三语文案（中文 / 日文 / 英文）
/// </summary>
public class LocalizedText
{
    public string Zh { get; set; } = "";
    public string Ja { get; set; } = "";
    public string En { get; set; } = "";

    public static LocalizedText Of(string zh, string ja, string en) => new() { Zh = zh, Ja = ja, En = en };

    /// <summary>取指定语言文案</summary>
    public string Get(string lang) => lang switch
    {
        "ja" => string.IsNullOrEmpty(Ja) ? Zh : Ja,
        "en" => string.IsNullOrEmpty(En) ? Zh : En,
        _ => Zh
    };
}

/// <summary>坐标轴规范</summary>
public class AxisSpec
{
    public LocalizedText Name { get; set; } = new();
    public string Unit { get; set; } = "";
    /// <summary>轴类型：value / category / time</summary>
    public string Type { get; set; } = "value";
}

/// <summary>图表系列</summary>
public class SeriesSpec
{
    public string Name { get; set; } = "";
    /// <summary>数据点：[x, y] 对（x 可为时间字符串或数值）</summary>
    public List<object[]> Data { get; set; } = new();
    public string Type { get; set; } = "line";
}

/// <summary>图表规范（一个完整 ECharts 图）</summary>
public class ChartSpec
{
    public string ChartId { get; set; } = "";
    public string Type { get; set; } = "line";
    public LocalizedText Title { get; set; } = new();
    public AxisSpec XAxis { get; set; } = new();
    public AxisSpec YAxis { get; set; } = new();
    public List<SeriesSpec> Series { get; set; } = new();
    /// <summary>分类轴的类目列表（boxplot/bar 等需要）</summary>
    public List<string>? XCategories { get; set; }
    /// <summary>附加的 ECharts 选项（JSON 对象字符串，如标记线限值）</summary>
    public string? ExtraOption { get; set; }
}

/// <summary>数据表</summary>
public class TableSpec
{
    public LocalizedText Title { get; set; } = new();
    public List<LocalizedText> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
}

/// <summary>分析章节（一个统计方法产出一个或多个章节）</summary>
public class AnalysisSection
{
    public string SectionKey { get; set; } = "";
    public LocalizedText Title { get; set; } = new();
    public List<LocalizedText> Conclusions { get; set; } = new();
    public List<ChartSpec> Charts { get; set; } = new();
    public List<TableSpec> Tables { get; set; } = new();
}

/// <summary>完整分析报告数据（设备 + 章节）</summary>
public class ReportData
{
    public List<DeviceProfile> Devices { get; set; } = new();
    public List<AnalysisSection> Sections { get; set; } = new();
    public string GeneratedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}
