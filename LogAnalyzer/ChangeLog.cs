namespace LogAnalyzer;

/// <summary>
/// 一条版本变更记录
/// </summary>
public class ChangeEntry
{
    public string Version { get; set; } = "";
    public string Date { get; set; } = "";
    public List<string> Items { get; set; } = new();
}

/// <summary>
/// 版本变更记录（ChangeLog）。
/// 每次变更：CurrentVersion 按 0.0.1 递增，并在 History 头部新增一条记录。
/// </summary>
public static class ChangeLog
{
    /// <summary>当前版本号（每次变更必须递增）</summary>
    public const string CurrentVersion = "V0.1.5";

    /// <summary>变更历史（新的在前）</summary>
    public static readonly List<ChangeEntry> History = new()
    {
        new ChangeEntry
        {
            Version = "V0.1.5",
            Date = "2026-10-19",
            Items = new List<string>
            {
                "修复：导入日志后程序崩溃退出的问题——根因是 32 位进程（net48 默认 Prefer32Bit）解析大日志内存受限，且 WebView2Loader.dll 位数与进程位数不匹配导致 BadImageFormatException。",
                "强制 64 位编译（PlatformTarget=x64 + Prefer32Bit=false），消除 32 位内存限制。",
                "发布脚本自动修正 WebView2Loader.dll 为 x64 版本（NuGet 默认复制 x86）。",
                "新增全局异常捕获 + error.log 日志 + 导入 try-catch，异常不再导致进程直接退出，并可定位问题。",
            }
        },
        new ChangeEntry
        {
            Version = "V0.1.4",
            Date = "2026-10-19",
            Items = new List<string>
            {
                "迁移到 .NET Framework 4.8：利用 Win10/11 系统自带运行时，发布体积从 69MB 缩减至约 3.4MB（exe 仅 1.2MB），拷贝到任意 Win10+ 电脑直接运行、无需安装 .NET 运行时。",
                "语言版本保持 latest（保留全部 C# 9 语法），新增 System.Text.Json 依赖与 System.Net.Http/System.Windows.Forms 引用。",
                "GBK 解码改用 .NET Framework 原生代码页（无需 CodePagesEncodingProvider）。",
                "文件夹选择对话框改用 WinForms FolderBrowserDialog（.NET Framework 无 OpenFolderDialog）。",
            }
        },
        new ChangeEntry
        {
            Version = "V0.1.3",
            Date = "2026-10-19",
            Items = new List<string>
            {
                "新增「需求驱动分析」：上传日志后弹出对话框，用户输入分析需求（如「分析温度趋势并对比设备信号强度」），上位机根据需求内容自行判断需要调用哪些分析方法，生成专业分析报告。",
                "新增「需求解析器」：本地关键词匹配（趋势/变化→时间序列趋势；相关/关联→相关分析；对比/统计/分布→描述统计），需求为空或未命中时按数据形态全自动分析。",
                "报告首页新增「需求解析摘要」，展示用户需求与匹配的分析方法。",
            }
        },
        new ChangeEntry
        {
            Version = "V0.1.2",
            Date = "2026-10-19",
            Items = new List<string>
            {
                "新增「解析优先级模式」：本地优先解析（预定义解析器 + 通用智能解析器）；本地解析不完整时，若联网则自动调用免费 AI 工具（DeepSeek / Kimi / 智谱GLM 等 OpenAI 兼容接口，可配多个依次尝试）增强解析。",
                "新增「离线降级」：电脑未联网时弹窗提示「仅输出本地解析报告」，不再尝试联网 AI。",
                "新增「AI 配置」：程序目录 ai_config.json 可配置免费 AI 提供商（名称/地址/密钥/模型），未配置密钥时自动跳过 AI 增强。",
                "新增「本地解析质量评估」：有识别码且有指标/字段即视为本地解析完整，否则触发 AI 增强。",
            }
        },
        new ChangeEntry
        {
            Version = "V0.1.1",
            Date = "2026-10-19",
            Items = new List<string>
            {
                "新增「通用智能解析器」（兜底）：对未预定义的新机型，读入日志内容后自行规划解析方案——自动探测时间戳格式、自动识别「字段名=数值+单位」键值模式、自动区分固定信息与时序指标、自动推断单位，识别码走 IMEI/MAC/SN 提取链。",
                "新增「联网检测 + 弹窗提示」：程序启动时检测联网，离线弹窗告知（当前解析/统计/报告均为本地实现、离线可用；需联网功能预留提示）。",
                "新增「自包含单文件发布」：publish.ps1 生成 LogAnalyzer_{版本}.exe（含 .NET 运行时），拷贝到任意 Win10+ 电脑直接运行，无需安装运行时。",
                "新增菜单栏「文件/帮助」：选择文件/文件夹、导出报告、退出、操作说明(SOP)、版本变更记录。",
            }
        },
        new ChangeEntry
        {
            Version = "V0.1.0",
            Date = "2026-10-19",
            Items = new List<string>
            {
                "初始版本（P1 骨架）：搭建 .NET 8 WPF 项目，分层架构（Models/Parsers/Analysis/Report/Services）。",
                "导入层：支持拖拽导入单个/多个同类型日志文件或整个文件夹，自动递归枚举 *.txt/*.log。",
                "机型自动识别：内置 B203 / B504 / B509ES / B705/B706 / 音频 B903 五个解析器，按文件名 + 内容特征双重判型。",
                "唯一识别码提取链：IMEI（含 Luhn 校验）→ MAC → SN 优先级；识别「IMEI 字段存 12 位十六进制」时自动归为 MAC。",
                "固定设备信息提取：SW/HW/ICCID/MDM/MAC/SN/SDK/UniqueID 等按机型差异化提取。",
                "时序参数解析：RSSI（LTE/WiFi/BLE/ESP32）、温湿度、震动/看门狗计数、电池电压、音频 THD/SNR/底噪/灵敏度。",
                "自适应统计引擎：IAnalysisMethod 接口 + CanApply 适用性判断；内置描述性统计、相关分析（Pearson）、时间序列趋势；KANO/SEM/中介效应预留并自适应触发。",
                "多设备箱线图对比：相同指标在多台设备间自动生成 boxplot 分布对比。",
                "HTML 报告：单文件离线报告，内嵌 ECharts 5；dataZoom 拖动缩放、axisPointer 十字虚线指示、tooltip 悬浮高亮、Y 轴纵向名称、图表上方标题。",
                "中日英三语：按钮即时切换，图表标题/坐标轴/指标名/结论随语言切换。",
                "同识别码多文件自动合并为同一设备的多段数据。",
            }
        }
    };

    /// <summary>获取当前版本号（去掉 V 前缀返回纯版本，如 0.1.0）</summary>
    public static string CurrentVersionPlain => CurrentVersion.TrimStart('V', 'v');
}
