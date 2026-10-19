using LogAnalyzer.Models;

namespace LogAnalyzer.Parsers;

/// <summary>
/// 日志解析器插件接口。每个机型一个实现。
/// </summary>
public interface ILogParser
{
    /// <summary>机型标识（如 "B203"、"B504"、"B903"）</summary>
    string Model { get; }

    /// <summary>
    /// 判断当前文件是否属于本机型。
    /// 依据文件名关键字 + 内容特征双重判断。
    /// </summary>
    bool Detect(string fileName, string sampleText);

    /// <summary>
    /// 提取设备唯一识别码（优先 IMEI，其次 MAC，再 SN）。
    /// 返回 null 表示未能识别。
    /// </summary>
    (string id, DeviceIdType type)? ExtractDeviceId(string text);

    /// <summary>
    /// 解析完整设备档案：固定信息 + 指标时间序列。
    /// </summary>
    DeviceProfile Parse(string fileName, string text);
}

/// <summary>
/// 解析器通用工具：唯一识别码提取链（IMEI → MAC → SN）。
/// </summary>
public static class DeviceIdExtractor
{
    // IMEI：15 位数字，常见于 "IMEI:xxx" / "IMEI=xxx" / "imei: xxx"
    private static readonly System.Text.RegularExpressions.Regex ImeiRegex = new(
        @"(?:IMEI|imei)\s*[:=]\s*(\d{14,16})",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    // 某些机型把 MAC 以 12 位十六进制写进 "IMEI:" 字段（如 B705: IMEI: 3844BE3E7250）
    // 需识别为 MAC 而非 IMEI
    private static readonly System.Text.RegularExpressions.Regex ImeiHexMacRegex = new(
        @"(?i)(?:IMEI|imei)\s*[:=]\s*([0-9A-F]{12})\b",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    // 纯 IMEI 15 位数字兜底（前后非数字）
    private static readonly System.Text.RegularExpressions.Regex ImeiBareRegex = new(
        @"(?<!\d)(\d{15})(?!\d)",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    // MAC 地址：AA:BB:CC:DD:EE:FF 或 AA-BB-... 或 AABBCCDDEEFF（12位十六进制）
    private static readonly System.Text.RegularExpressions.Regex MacRegex = new(
        @"(?i)\b([0-9A-F]{2}[:-][0-9A-F]{2}[:-][0-9A-F]{2}[:-][0-9A-F]{2}[:-][0-9A-F]{2}[:-][0-9A-F]{2})\b",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex MacBareRegex = new(
        @"(?i)(?:MAC|mac)\s*[:=]\s*([0-9A-F]{12})\b",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    // SN：常见 "SN:xxx" / "GETSN=xxx" / "S/N:xxx"
    private static readonly System.Text.RegularExpressions.Regex SnRegex = new(
        @"(?i)(?:GETSN|SN|S/N)\s*[:=]\s*([A-Za-z0-9\-_]{4,32})",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>按 IMEI → MAC → SN 优先级链提取识别码。</summary>
    public static (string id, DeviceIdType type)? Extract(string text)
    {
        var imei = ExtractImei(text);
        if (imei != null) return imei.Value;

        var mac = ExtractMac(text);
        if (mac != null) return mac.Value;

        var sn = ExtractSn(text);
        if (sn != null) return sn.Value;

        return null;
    }

    public static (string id, DeviceIdType type)? ExtractImei(string text)
    {
        // 先排除 "IMEI:" 后跟 12 位十六进制的情况（那是 MAC）
        var hexMac = ImeiHexMacRegex.Match(text);
        if (hexMac.Success)
            return (FormatMac(hexMac.Groups[1].Value), DeviceIdType.Mac);

        var m = ImeiRegex.Match(text);
        if (m.Success)
        {
            var v = m.Groups[1].Value;
            if (v.Length >= 14 && v.Length <= 16) return (v, DeviceIdType.Imei);
        }
        m = ImeiBareRegex.Match(text);
        if (m.Success)
        {
            var v = m.Groups[1].Value;
            // 15 位数字需要通过 Luhn 校验才可信，避免误抓时间戳等
            if (IsValidImei(v)) return (v, DeviceIdType.Imei);
        }
        return null;
    }

    public static (string id, DeviceIdType type)? ExtractMac(string text)
    {
        var m = MacBareRegex.Match(text);
        if (m.Success)
        {
            var v = m.Groups[1].Value;
            return (FormatMac(v), DeviceIdType.Mac);
        }
        m = MacRegex.Match(text);
        if (m.Success)
        {
            var v = m.Groups[1].Value;
            return (v.ToUpperInvariant(), DeviceIdType.Mac);
        }
        return null;
    }

    public static (string id, DeviceIdType type)? ExtractSn(string text)
    {
        var m = SnRegex.Match(text);
        if (m.Success)
        {
            var v = m.Groups[1].Value.Trim();
            if (v.Length >= 4 && !v.Equals("invalid", StringComparison.OrdinalIgnoreCase)
                && !v.Equals("null", StringComparison.OrdinalIgnoreCase))
                return (v, DeviceIdType.Sn);
        }
        return null;
    }

    private static string FormatMac(string hex)
    {
        if (hex.Length != 12) return hex.ToUpperInvariant();
        var parts = new string[6];
        for (int i = 0; i < 6; i++) parts[i] = hex.Substring(i * 2, 2);
        return string.Join(":", parts).ToUpperInvariant();
    }

    /// <summary>Luhn 校验 IMEI（15 位数字）</summary>
    public static bool IsValidImei(string imei)
    {
        if (imei.Length != 15 || !imei.All(char.IsDigit)) return false;
        int sum = 0;
        for (int i = 0; i < 15; i++)
        {
            int d = imei[i] - '0';
            if (i % 2 == 1) // 从 0 计数，第 2、4…位翻倍
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
        }
        return sum % 10 == 0;
    }
}
