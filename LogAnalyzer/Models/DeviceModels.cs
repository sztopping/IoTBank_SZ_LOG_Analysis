using System.Globalization;

namespace LogAnalyzer.Models;

/// <summary>
/// 设备唯一识别码的类型
/// </summary>
public enum DeviceIdType
{
    Imei,
    Mac,
    Sn,
    Custom
}

/// <summary>
/// 固定设备信息条目（如 SW 版本、HW 版本、ICCID、MDM 等）
/// </summary>
public class DeviceField
{
    /// <summary>字段键（英文稳定标识，用于 i18n 翻译），如 "SW"、"HW"、"ICCID"、"MAC"</summary>
    public string Key { get; set; } = "";

    /// <summary>字段原始值</summary>
    public string Value { get; set; } = "";

    /// <summary>字段显示名（中文），无内置翻译时回退用</summary>
    public string DisplayName { get; set; } = "";
}

/// <summary>
/// 一个数据点（带时间戳）
/// </summary>
public class MetricPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
}

/// <summary>
/// 某项指标的时间序列（如 RSSI、温度、湿度等）
/// </summary>
public class MetricSeries
{
    /// <summary>指标键（英文稳定标识，用于 i18n），如 "RSSI"、"TEMPERATURE"、"HUMIDITY"、"THD"</summary>
    public string Key { get; set; } = "";

    /// <summary>指标显示名（中文）</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>单位，如 "dBm"、"°C"、"%"、"dB"</summary>
    public string Unit { get; set; } = "";

    /// <summary>数据点</summary>
    public List<MetricPoint> Points { get; set; } = new();

    /// <summary>该指标是否有合格判定限值（用于 CPK 等）</summary>
    public double? UpperLimit { get; set; }
    public double? LowerLimit { get; set; }

    public int Count => Points.Count;
}

/// <summary>
/// 设备档案：解析一个（或一组同 IMEI）log 后得到
/// </summary>
public class DeviceProfile
{
    /// <summary>唯一识别码的值（IMEI/MAC/SN…）</summary>
    public string DeviceId { get; set; } = "";

    /// <summary>识别码类型</summary>
    public DeviceIdType IdType { get; set; }

    /// <summary>机型（B203/B504/B509ES/B705/B706/B903 等）</summary>
    public string Model { get; set; } = "";

    /// <summary>固定设备信息</summary>
    public List<DeviceField> Fields { get; set; } = new();

    /// <summary>指标时间序列</summary>
    public List<MetricSeries> Metrics { get; set; } = new();

    /// <summary>来源文件列表</summary>
    public List<string> SourceFiles { get; set; } = new();

    /// <summary>日志总行数</summary>
    public long TotalLines { get; set; }

    /// <summary>解析得到的时间范围</summary>
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    /// <summary>按 Key 查找固定字段值</summary>
    public string? GetField(string key)
    {
        return Fields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    /// <summary>按 Key 查找指标</summary>
    public MetricSeries? GetMetric(string key)
    {
        return Metrics.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
    }
}
