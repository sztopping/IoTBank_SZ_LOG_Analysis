using System.IO;
using LogAnalyzer.Models;

namespace LogAnalyzer.Parsers;

/// <summary>
/// 机型识别器：管理所有解析器，按文件名 + 内容特征路由到对应解析器。
/// 返回未匹配时的通用兜底解析器。
/// </summary>
public class ModelDetector
{
    private readonly List<ILogParser> _parsers;
    private readonly ILogParser _smartParser;

    public ModelDetector()
    {
        _smartParser = new UniversalSmartParser();
        _parsers = new List<ILogParser>
        {
            new B203Parser(),
            new B504Parser(),
            new B509EsParser(),
            new B705B706Parser(),
            new AudioParser(),
        };
    }

    public IReadOnlyList<ILogParser> Parsers => _parsers;

    /// <summary>
    /// 识别文件机型，返回对应解析器；预定义机型均不匹配时，返回通用智能解析器（读内容自行规划）。
    /// </summary>
    public ILogParser Detect(string fileName, string sampleText)
    {
        foreach (var p in _parsers)
            if (p.Detect(fileName, sampleText))
                return p;
        return _smartParser;
    }

    /// <summary>
    /// 解析单个文件为设备档案（智能解析器兜底，总可解析）。
    /// </summary>
    public DeviceProfile? ParseFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        string text;
        try
        {
            // 优先 UTF-8，失败回退系统默认编码（兼容 GBK）
            text = ReadAllTextAuto(filePath);
        }
        catch
        {
            return null;
        }

        var parser = Detect(fileName, text.Length > 4000 ? text.Substring(0, 4000) : text);
        var profile = parser.Parse(fileName, text);
        return profile;
    }

    /// <summary>
    /// 读取文本文件，自动探测编码（UTF-8 / GBK）。
    /// </summary>
    public static string ReadAllTextAuto(string path)
    {
        var bytes = File.ReadAllBytes(path);
        // 去掉 UTF-8 BOM
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        // 尝试 UTF-8 严格解码
        try
        {
            var utf8 = new System.Text.UTF8Encoding(false, true);
            return utf8.GetString(bytes);
        }
        catch (System.Text.DecoderFallbackException)
        {
            // 回退 GBK（.NET Framework 4.8 默认支持代码页，无需 RegisterProvider）
            return System.Text.Encoding.GetEncoding("GBK").GetString(bytes);
        }
    }
}
