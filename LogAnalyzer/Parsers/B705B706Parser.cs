using System.Text.RegularExpressions;
using LogAnalyzer.Models;

namespace LogAnalyzer.Parsers;

/// <summary>
/// B705/B706 ATA 测试日志解析器（Python 上位机 INFO 日志 + AT 指令型）。
/// 格式：2026-07-27 14:35:53,184 [INFO] 内容
/// 识别码：^DEV@GETMAC 返回的设备 MAC（38:44:BE:3E:... 前缀，非 BLE 广播 MAC）。
/// </summary>
public class B705B706Parser : ILogParser
{
    public string Model => "B705/B706";

    private static readonly Regex Ts = new(@"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}),\d+\s+\[(?:INFO|WARNING|ERROR)\]", RegexOptions.Compiled);
    private static readonly Regex DevMac = new(@"\^DEV@GETMAC=([0-9A-Fa-f:]+)", RegexOptions.Compiled);
    private static readonly Regex DevKv = new(@"\^DEV@([A-Za-z0-9]+)=([^\r\n;]*)?", RegexOptions.Compiled);
    private static readonly Regex SnImei = new(@"SN:\s*([^\s,]+),\s*IMEI:\s*([0-9A-Fa-f]+)", RegexOptions.Compiled);

    public bool Detect(string fileName, string sampleText)
    {
        if (fileName.Contains("B705", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("B706", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("ATA", StringComparison.OrdinalIgnoreCase)) return true;
        return sampleText.Contains("^DEV@GETMAC", StringComparison.OrdinalIgnoreCase)
            && sampleText.Contains("SN:", StringComparison.OrdinalIgnoreCase);
    }

    public (string id, DeviceIdType type)? ExtractDeviceId(string text)
    {
        // 优先设备自身 MAC（GETMAC）
        var m = DevMac.Match(text);
        if (m.Success)
        {
            var mac = m.Groups[1].Value.Split(',')[0].Trim();
            if (mac.Length >= 12) return (mac.ToUpperInvariant(), DeviceIdType.Mac);
        }
        // 其次 SN/IMEI 对（IMEI 字段是 12 位 hex = MAC）
        var si = SnImei.Match(text);
        if (si.Success)
            return (DeviceIdExtractor.ExtractImei("IMEI:" + si.Groups[2].Value) ?? (si.Groups[1].Value, DeviceIdType.Sn));
        return DeviceIdExtractor.Extract(text);
    }

    public DeviceProfile Parse(string fileName, string text)
    {
        var profile = new DeviceProfile { Model = Model };
        profile.SourceFiles.Add(fileName);

        var id = ExtractDeviceId(text);
        if (id != null)
        {
            profile.DeviceId = id.Value.id;
            profile.IdType = id.Value.type;
        }

        var bleRssi = new MetricSeries { Key = "BLE_RSSI", DisplayName = "蓝牙信号强度", Unit = "dBm" };
        var wifiRssi = new MetricSeries { Key = "WIFI_RSSI", DisplayName = "WiFi 信号强度", Unit = "dBm" };

        long lines = 0;
        DateTime? cur = null;
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in text.Split('\n'))
        {
            lines++;
            var line = raw.TrimEnd('\r', '\n');

            var tsM = Ts.Match(line);
            if (tsM.Success)
                cur = DateTime.TryParse(tsM.Groups[1].Value, out var dt) ? dt : cur;

            // SN/IMEI 行 → 固定字段
            var si = SnImei.Match(line);
            if (si.Success)
            {
                if (!fields.ContainsKey("SN")) fields["SN"] = si.Groups[1].Value;
                if (!fields.ContainsKey("IMEI")) fields["IMEI"] = si.Groups[2].Value;
            }

            // DEV 键值（SW/HW 等）
            var dvM = DevKv.Match(line);
            if (dvM.Success)
            {
                var k = dvM.Groups[1].Value.Trim();
                var v = dvM.Groups[2].Value.Trim();
                if (v.Length > 0 && !fields.ContainsKey(k)) fields[k] = v;
            }

            // 固定：GETMAC 行
            var dmM = DevMac.Match(line);
            if (dmM.Success && !fields.ContainsKey("MAC"))
                fields["MAC"] = dmM.Groups[1].Value.Split(',')[0].Trim();

            if (cur != null)
            {
                // BLE 扫描
                if (line.Contains("SCAN_MAC=", StringComparison.OrdinalIgnoreCase))
                {
                    var vals = ExtractRssiValues(line);
                    if (vals.Count > 0)
                        bleRssi.Points.Add(new MetricPoint { Timestamp = cur.Value, Value = vals.Average() });
                }
                // WiFi 扫描
                else if (line.Contains("WIFISUM=", StringComparison.OrdinalIgnoreCase))
                {
                    var vals = ExtractRssiValues(line);
                    if (vals.Count > 0)
                        wifiRssi.Points.Add(new MetricPoint { Timestamp = cur.Value, Value = vals.Average() });
                }
            }
        }

        foreach (var kv in fields)
            profile.Fields.Add(new DeviceField { Key = kv.Key, Value = kv.Value, DisplayName = kv.Key });

        if (bleRssi.Count > 0) profile.Metrics.Add(bleRssi);
        if (wifiRssi.Count > 0) profile.Metrics.Add(wifiRssi);

        profile.TotalLines = lines;
        profile.StartTime = profile.Metrics.SelectMany(m => m.Points).Select(p => p.Timestamp).DefaultIfEmpty().Min();
        profile.EndTime = profile.Metrics.SelectMany(m => m.Points).Select(p => p.Timestamp).DefaultIfEmpty().Max();
        if (profile.StartTime == default) { profile.StartTime = null; profile.EndTime = null; }

        return profile;
    }

    private static List<double> ExtractRssiValues(string line)
    {
        var result = new List<double>();
        foreach (Match m in Regex.Matches(line, @"_(-?\d+(?:\.\d+)?)"))
            if (double.TryParse(m.Groups[1].Value, out var v))
                result.Add(v);
        return result;
    }
}
