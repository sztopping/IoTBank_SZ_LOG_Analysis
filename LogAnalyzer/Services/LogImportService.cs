using System.IO;
using LogAnalyzer.Models;
using LogAnalyzer.Parsers;

namespace LogAnalyzer.Services;

/// <summary>
/// 日志导入结果
/// </summary>
public class ImportResult
{
    public List<DeviceProfile> Devices { get; set; } = new();
    /// <summary>需要弹窗提示的信息（如「未联网，仅出本地解析报告」）</summary>
    public List<string> Messages { get; set; } = new();
    public bool AiUsed { get; set; }
    public bool AiNeededButOffline { get; set; }
}

/// <summary>
/// 日志导入服务：本地优先解析，本地不完整时联网调用免费 AI 增强；未联网则降级仅出本地报告。
/// </summary>
public class LogImportService
{
    private readonly ModelDetector _detector = new();
    private readonly AiParserService _ai = new();

    private static readonly string[] Extensions = { ".txt", ".log" };

    /// <summary>判断本地解析结果是否「完整」</summary>
    public static bool IsComplete(DeviceProfile p)
        => !string.IsNullOrEmpty(p.DeviceId) && (p.Metrics.Count > 0 || p.Fields.Count >= 2);

    /// <summary>
    /// 异步导入：本地优先 → 本地不完整则联网 AI 增强 → 未联网降级。
    /// </summary>
    public async Task<ImportResult> ImportAsync(IEnumerable<string> paths)
    {
        var result = new ImportResult();

        var files = new List<string>();
        foreach (var p in paths)
        {
            if (Directory.Exists(p)) files.AddRange(EnumerateLogFiles(p));
            else if (File.Exists(p) && Extensions.Contains(Path.GetExtension(p).ToLowerInvariant())) files.Add(p);
        }
        files = files.Distinct().ToList();

        var profiles = new List<DeviceProfile>();
        var incomplete = new List<(string file, DeviceProfile? local)>(); // 本地不完整的文件

        // 1) 本地解析
        foreach (var f in files)
        {
            var profile = _detector.ParseFile(f);
            if (profile == null) continue;
            if (IsComplete(profile))
                profiles.Add(profile);
            else
                incomplete.Add((f, profile));
        }

        // 2) 本地不完整的文件：联网则 AI 增强，未联网降级
        if (incomplete.Count > 0)
        {
            var online = await NetworkHelper.IsOnlineAsync();

            if (online && AiConfig.HasProvider)
            {
                foreach (var (file, local) in incomplete)
                {
                    string text;
                    try { text = ModelDetector.ReadAllTextAuto(file); }
                    catch { continue; }

                    var aiProfile = await _ai.AnalyzeAsync(Path.GetFileName(file), text);
                    if (aiProfile != null && IsComplete(aiProfile))
                    {
                        aiProfile.Model = $"{aiProfile.Model}(AI)";
                        profiles.Add(aiProfile);
                        result.AiUsed = true;
                        continue;
                    }
                    // AI 也失败，保留本地结果
                    if (local != null) profiles.Add(local);
                }
                if (result.AiUsed)
                    result.Messages.Add("部分日志已通过联网 AI 增强解析。");
            }
            else if (!online)
            {
                result.AiNeededButOffline = true;
                result.Messages.Add("电脑未联网，本次仅输出本地解析报告（未联网无法调用 AI 增强解析）。");
                foreach (var (_, local) in incomplete)
                    if (local != null) profiles.Add(local);
            }
            else // 联网但未配置 AI
            {
                result.Messages.Add("本地解析不完整，且未配置免费 AI 密钥（ai_config.json）；可配置后启用联网 AI 增强解析。");
                foreach (var (_, local) in incomplete)
                    if (local != null) profiles.Add(local);
            }
        }

        result.Devices = MergeByDeviceId(profiles);
        return result;
    }

    private static IEnumerable<string> EnumerateLogFiles(string dir)
    {
        foreach (var f in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
            if (Extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                yield return f;
    }

    private static List<DeviceProfile> MergeByDeviceId(List<DeviceProfile> profiles)
    {
        var dict = new Dictionary<string, DeviceProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in profiles)
        {
            var key = p.DeviceId;
            if (string.IsNullOrEmpty(key))
                key = p.SourceFiles.FirstOrDefault() ?? Guid.NewGuid().ToString();

            if (!dict.TryGetValue(key, out var existing)) { dict[key] = p; continue; }

            foreach (var f in p.Fields)
                if (!existing.Fields.Any(x => x.Key == f.Key)) existing.Fields.Add(f);
            foreach (var m in p.Metrics)
            {
                var target = existing.GetMetric(m.Key);
                if (target == null) { existing.Metrics.Add(m); continue; }
                target.Points.AddRange(m.Points);
                target.Points = target.Points.OrderBy(pt => pt.Timestamp).ToList();
            }
            existing.SourceFiles.AddRange(p.SourceFiles.Where(f => !existing.SourceFiles.Contains(f)));
            existing.TotalLines += p.TotalLines;
            if (p.StartTime != null && (existing.StartTime == null || p.StartTime < existing.StartTime)) existing.StartTime = p.StartTime;
            if (p.EndTime != null && (existing.EndTime == null || p.EndTime > existing.EndTime)) existing.EndTime = p.EndTime;
        }
        return dict.Values.ToList();
    }
}
