using System.Text.RegularExpressions;
using LogAnalyzer.Models;

namespace LogAnalyzer.Parsers;

/// <summary>
/// B203 常温测试上位机日志解析器。
/// 格式：[2026-10-11 13:48:56] [RX/TX/SYS] 内容
/// AT 指令型：^DEVINFO@... / ^DEV@... / ^DEV@LTERF / WIFISUM / SCAN_MAC / ESP32 JSON
/// </summary>
public class B203Parser : ILogParser
{
    public string Model => "B203";

    private static readonly Regex TsRx = new(@"^\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\] \[(?:RX|TX|SYS)\]", RegexOptions.Compiled);
    private static readonly Regex DevInfo = new(@"\^DEVINFO@(.*)$", RegexOptions.Compiled);
    private static readonly Regex DevKv = new(@"\^DEV@([A-Za-z0-9]+)=([^\r\n;]*)?", RegexOptions.Compiled);
    private static readonly Regex EspWifi = new(@"\[ESP32-WiFi\] 扫描结果.*?Rssi[^\d]*(-?\d+)", RegexOptions.Compiled);
    private static readonly Regex EspBle = new(@"\[ESP32-蓝牙\] 扫描结果.*?Rssi[^\d]*(-?\d+)", RegexOptions.Compiled);

    public bool Detect(string fileName, string sampleText)
    {
        if (fileName.Contains("B203", StringComparison.OrdinalIgnoreCase)) return true;
        return sampleText.Contains("^DEVINFO@", StringComparison.OrdinalIgnoreCase)
            && sampleText.Contains("WIFISTNMAC", StringComparison.OrdinalIgnoreCase);
    }

    public (string id, DeviceIdType type)? ExtractDeviceId(string text)
    {
        // B203 固定有 IMEI
        var imei = DeviceIdExtractor.ExtractImei(text);
        if (imei != null) return imei;
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

        var lteRf = new MetricSeries { Key = "LTE_RSSI", DisplayName = "LTE 信号强度", Unit = "dBm" };
        var wifiRssi = new MetricSeries { Key = "WIFI_RSSI", DisplayName = "WiFi 信号强度", Unit = "dBm" };
        var bleRssi = new MetricSeries { Key = "BLE_RSSI", DisplayName = "蓝牙信号强度", Unit = "dBm" };
        var espWifi = new MetricSeries { Key = "ESP32_WIFI_RSSI", DisplayName = "ESP32 WiFi 信号", Unit = "dBm" };
        var espBle = new MetricSeries { Key = "ESP32_BLE_RSSI", DisplayName = "ESP32 蓝牙信号", Unit = "dBm" };

        long lines = 0;
        DateTime? cur = null;
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in text.Split('\n'))
        {
            lines++;
            var line = raw.TrimEnd('\r', '\n');
            var tsM = TsRx.Match(line);
            if (tsM.Success)
                cur = DateTime.TryParse(tsM.Groups[1].Value, out var dt) ? dt : cur;

            // 固定信息：DEVINFO 一整行
            var diM = DevInfo.Match(line);
            if (diM.Success)
            {
                foreach (var seg in diM.Groups[1].Value.Split(';'))
                {
                    var idx = seg.IndexOf('=');
                    if (idx <= 0) continue;
                    var k = seg.Substring(0, idx).Trim();
                    var v = seg.Substring(idx + 1).Trim();
                    if (v.Length > 0 && !fields.ContainsKey(k)) fields[k] = v;
                }
                continue;
            }

            // WiFi 扫描结果：^DEV@WIFISUM=2;RSSI=MAC_-68&...（先于 DevKv，避免被当固定字段）
            if (line.Contains("WIFISUM=", StringComparison.OrdinalIgnoreCase) && cur != null)
            {
                var vals = ExtractRssiValues(line);
                if (vals.Count > 0)
                    wifiRssi.Points.Add(new MetricPoint { Timestamp = cur.Value, Value = vals.Average() });
                continue;
            }

            // BLE 扫描结果：^DEV@SCAN_MAC=MAC_-83&...
            if (line.Contains("SCAN_MAC=", StringComparison.OrdinalIgnoreCase) && cur != null)
            {
                var vals = ExtractRssiValues(line);
                if (vals.Count > 0)
                    bleRssi.Points.Add(new MetricPoint { Timestamp = cur.Value, Value = vals.Average() });
                continue;
            }

            // 固定信息：DEV 键值
            var dvM = DevKv.Match(line);
            if (dvM.Success)
            {
                var k = dvM.Groups[1].Value.Trim();
                var v = dvM.Groups[2].Value.Trim();
                if (v.Length > 0 && !fields.ContainsKey(k)) fields[k] = v;
                // LTERF 是时序指标
                if (k.Equals("LTERF", StringComparison.OrdinalIgnoreCase)
                    && cur != null && double.TryParse(v, out var lv))
                    lteRf.Points.Add(new MetricPoint { Timestamp = cur.Value, Value = lv });
                continue;
            }

            // ESP32 WiFi 扫描结果 JSON
            var ewM = EspWifi.Match(line);
            if (ewM.Success && cur != null && double.TryParse(ewM.Groups[1].Value, out var ewv))
                espWifi.Points.Add(new MetricPoint { Timestamp = cur.Value, Value = ewv });

            // ESP32 蓝牙扫描结果 JSON
            var ebM = EspBle.Match(line);
            if (ebM.Success && cur != null && double.TryParse(ebM.Groups[1].Value, out var ebv))
                espBle.Points.Add(new MetricPoint { Timestamp = cur.Value, Value = ebv });
        }

        // 组装固定字段
        foreach (var kv in fields)
            profile.Fields.Add(new DeviceField { Key = kv.Key, Value = kv.Value, DisplayName = kv.Key });

        // 组装指标（丢弃空序列）
        if (lteRf.Count > 0) profile.Metrics.Add(lteRf);
        if (wifiRssi.Count > 0) profile.Metrics.Add(wifiRssi);
        if (bleRssi.Count > 0) profile.Metrics.Add(bleRssi);
        if (espWifi.Count > 0) profile.Metrics.Add(espWifi);
        if (espBle.Count > 0) profile.Metrics.Add(espBle);

        profile.TotalLines = lines;
        profile.StartTime = profile.Metrics.SelectMany(m => m.Points).Select(p => p.Timestamp).DefaultIfEmpty().Min();
        profile.EndTime = profile.Metrics.SelectMany(m => m.Points).Select(p => p.Timestamp).DefaultIfEmpty().Max();
        if (profile.StartTime == default) { profile.StartTime = null; profile.EndTime = null; }

        return profile;
    }

    /// <summary>从 WIFISUM/SCAN_MAC 行提取所有 RSSI 数值（MAC_-70&... 格式）</summary>
    private static List<double> ExtractRssiValues(string line)
    {
        var result = new List<double>();
        // 匹配 _-70 或 _-70.5 形式
        foreach (Match m in Regex.Matches(line, @"_(-?\d+(?:\.\d+)?)"))
            if (double.TryParse(m.Groups[1].Value, out var v))
                result.Add(v);
        return result;
    }
}
