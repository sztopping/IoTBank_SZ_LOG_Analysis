#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""生成《全代码全功能说明》文档：架构说明 + 全部源码，据此可完整复制工具。"""
import os

ROOT = r"D:\C#\LOG分析工具\LogAnalyzer"
OUT = os.path.join(ROOT, "docs", "全代码全功能说明.md")

# 每个源文件的说明（按相对路径）
DESC = {
    "LogAnalyzer.csproj": "项目文件：.NET 8 WPF 项目，引用 WebView2、System.Text.Encoding.CodePages（GBK 解码）、内嵌 ECharts 资源。",
    "app.manifest": "应用清单：DPI 感知 + 权限级别。",
    "App.xaml": "应用程序入口 XAML（资源合并等）。",
    "App.xaml.cs": "应用程序入口：注册 GBK 编码提供程序。",
    "AssemblyInfo.cs": "程序集信息。",
    "MainWindow.xaml": "主窗口界面：菜单栏（文件/帮助）+ 左侧设备面板 + 右侧 WebView2 报告预览，支持拖拽。",
    "MainWindow.xaml.cs": "主窗口逻辑：导入/设备选择/生成报告/导出/菜单事件/版本号标题。",
    "ChangeLog.cs": "版本变更记录数据：CurrentVersion 常量 + History 列表，每次变更在此新增条目并递增版本。",
    "ChangeLogWindow.xaml": "版本变更记录窗口界面。",
    "ChangeLogWindow.xaml.cs": "版本变更记录窗口逻辑。",
    "Models/DeviceModels.cs": "核心数据模型：设备识别码类型、固定字段、指标时间序列、设备档案。",
    "Models/AnalysisModels.cs": "分析模型：三语文案 LocalizedText、坐标轴/图表/表格规范、分析章节、报告数据。",
    "Parsers/ILogParser.cs": "解析器接口 ILogParser + 识别码提取链（IMEI/MAC/SN，含 Luhn 校验、hex-MAC 识别）。",
    "Parsers/ModelDetector.cs": "机型识别器：管理所有解析器，按文件名+内容路由；编码自动探测（UTF-8/GBK）。",
    "Parsers/B203Parser.cs": "B203 解析器：AT 指令型，LTE/WiFi/BLE/ESP32 RSSI + DEVINFO 固定信息。",
    "Parsers/B504Parser.cs": "B504 解析器：温湿度/震动/看门狗时序 + Fs.* 配置字段。",
    "Parsers/B509EsParser.cs": "B509ES 解析器：nRF 固件日志，电池电压时序 + SW/SDK/OS/UniqueID。",
    "Parsers/B705B706Parser.cs": "B705/B706 解析器：Python 上位机日志，MAC 识别码 + BLE/WiFi RSSI。",
    "Parsers/AudioParser.cs": "音频 B903 解析器：THD/SNR/底噪/灵敏度 + 测试结论。",
    "Parsers/UniversalSmartParser.cs": "通用智能解析器（兜底）：读内容自行规划解析——自动探测时间戳、识别键值模式、区分固定/时序、推断单位。",
    "Analysis/Stats.cs": "统计数学：均值/标准差/分位数/Pearson/线性回归/描述统计/偏度峰度。",
    "Analysis/IAnalysisMethod.cs": "分析方法接口：CanApply 自适应 + Analyze。",
    "Analysis/AnalysisEngine.cs": "分析引擎：注册所有方法，遍历按数据形态自适应调用。",
    "Analysis/Methods/DescriptiveStatsMethod.cs": "描述性统计方法：统计表 + 时序图 + 多设备箱线图。",
    "Analysis/Methods/CorrelationMethod.cs": "相关分析方法：Pearson 相关系数矩阵。",
    "Analysis/Methods/TrendMethod.cs": "时间序列趋势方法：线性回归斜率/R² + 趋势判定。",
    "Analysis/Methods/LatentMethods.cs": "KANO/SEM/中介效应（问卷型，自适应触发，log 默认不触发）。",
    "Report/ReportGenerator.cs": "HTML 报告生成器：内嵌 ECharts + 三语 + dataZoom/axisPointer/tooltip/nameRotate。",
    "Services/LogImportService.cs": "导入服务：本地优先解析 + 质量评估 + 联网 AI 增强 + 离线降级 + 按识别码合并。",
    "Services/AiConfig.cs": "AI 解析配置：读取 ai_config.json 的免费 AI 提供商列表（OpenAI 兼容）。",
    "Services/AiParserService.cs": "AI 增强解析：联网调用免费 AI 解析日志，返回结构化设备档案。",
    "Services/NetworkHelper.cs": "联网检测工具：判断电脑是否可访问互联网。",
}

# 需要收录的源文件（相对路径），排除 bin/obj/docs 等
SRC_DIRS = ["Models", "Parsers", "Analysis", "Report", "Services", "Analysis/Methods"]
SRC_FILES = [
    "LogAnalyzer.csproj", "app.manifest", "App.xaml", "App.xaml.cs", "AssemblyInfo.cs",
    "MainWindow.xaml", "MainWindow.xaml.cs", "ChangeLog.cs", "ChangeLogWindow.xaml", "ChangeLogWindow.xaml.cs",
]

def collect_files():
    files = list(SRC_FILES)
    for d in SRC_DIRS:
        full = os.path.join(ROOT, d)
        if os.path.isdir(full):
            for f in sorted(os.listdir(full)):
                if f.endswith((".cs", ".xaml")):
                    rel = os.path.join(d, f).replace("\\", "/")
                    files.append(rel)
    return files

def build():
    lines = []
    a = lines.append
    a("# Log 分析工具 · 全代码全功能说明")
    a("")
    a("> 版本：V0.1.0（2026-10-19）　本文档包含完整架构说明与全部源码，依据本文内容可完整复制本工具。")
    a("")
    a("---")
    a("")
    a("## 一、项目概述")
    a("")
    a("本工具为 .NET 8 WPF 桌面应用，用于解析各机型（B203/B504/B509ES/B705/B706/音频 B903）测试上位机导出的日志，提取设备唯一识别码与固定信息，对返回参数做自适应统计分析，并生成可交互 HTML 报告（内嵌 ECharts、中日英三语）。")
    a("")
    a("### 技术栈")
    a("")
    a("- 框架：.NET 8 / WPF（Windows）")
    a("- 图表：ECharts 5.5.1（作为嵌入资源内嵌进报告，离线可用）")
    a("- 报告预览：Microsoft.Web.WebView2")
    a("- 编码：System.Text.Encoding.CodePages（GBK 解码）")
    a("- 统计：自研轻量统计库（无第三方重依赖）")
    a("")
    a("### 分层架构")
    a("")
    a("```")
    a("导入层 (Services/LogImportService)        文件/文件夹 → 解析 → 按识别码合并")
    a("解析层 (Parsers/*)                       机型识别 + 插件化解析器 + 识别码提取链")
    a("数据层 (Models/*)                        设备档案 + 指标时序 + 分析模型（三语文案/图表规范）")
    a("分析层 (Analysis/*)                      自适应统计引擎（描述统计/相关/趋势/箱线图…）")
    a("报告层 (Report/ReportGenerator)          HTML + ECharts + 三语 + 交互图表")
    a("```")
    a("")
    a("### 目录结构")
    a("")
    a("```")
    a("LogAnalyzer/")
    a("├─ LogAnalyzer.csproj")
    a("├─ app.manifest")
    a("├─ App.xaml / App.xaml.cs")
    a("├─ MainWindow.xaml / .cs         主界面")
    a("├─ ChangeLog.cs                  版本记录")
    a("├─ ChangeLogWindow.xaml / .cs    版本记录窗口")
    a("├─ Models/                       数据模型")
    a("├─ Parsers/                      解析器")
    a("├─ Analysis/                     统计引擎")
    a("│   └─ Methods/                  分析方法")
    a("├─ Report/                       报告生成")
    a("│   └─ assets/echarts.min.js      ECharts（嵌入资源）")
    a("├─ Services/                     导入服务")
    a("└─ docs/                         SOP 与本文档")
    a("```")
    a("")
    a("### 构建步骤")
    a("")
    a("1. 安装 .NET 8 SDK（或更高）。")
    a("2. 下载 ECharts：`curl -o Report/assets/echarts.min.js https://cdn.jsdelivr.net/npm/echarts@5.5.1/dist/echarts.min.js`")
    a("3. 执行 `dotnet build` 或 `dotnet publish -c Release`。")
    a("")
    a("### 关键设计")
    a("")
    a("- **识别码提取链**：IMEI（含 Luhn 校验）→ MAC → SN；识别「IMEI 字段为 12 位 hex」时归为 MAC（B705 场景）。")
    a("- **解析器插件**：`ILogParser` 接口，每机型一个实现，新增机型不改主流程。")
    a("- **自适应统计**：`IAnalysisMethod.CanApply` 判断数据形态，自动决定调用哪些方法。")
    a("- **三语**：`LocalizedText{Zh,Ja,En}` 三语文案贯穿图表标题/坐标轴/结论，前端 `switchLang` 切换。")
    a("")
    a("---")
    a("")
    a("## 二、完整源码")
    a("")

    files = collect_files()
    for rel in files:
        full = os.path.join(ROOT, rel)
        if not os.path.isfile(full):
            continue
        with open(full, "r", encoding="utf-8-sig") as fp:
            code = fp.read()
        a(f"### {rel}")
        a("")
        a(f"**说明：** {DESC.get(rel, '')}")
        a("")
        ext = "xml" if rel.endswith((".xaml", ".csproj", ".manifest")) else "csharp"
        a(f"```{ext}")
        a(code.rstrip())
        a("```")
        a("")
    a("---")
    a("")
    a("## 三、扩展指南：新增一个机型解析器")
    a("")
    a("1. 新建 `Parsers/XxxParser.cs`，实现 `ILogParser` 接口（`Detect` / `ExtractDeviceId` / `Parse`）。")
    a("2. 在 `ModelDetector` 构造函数的 `_parsers` 列表里注册该解析器。")
    a("3. 在 `ChangeLog` 里递增版本并新增变更条目；同步更新 `docs/SOP_操作说明.html` 与本文档。")
    a("")
    with open(OUT, "w", encoding="utf-8") as fp:
        fp.write("\n".join(lines))
    print(f"已生成：{OUT} ({len(lines)} 行)")

if __name__ == "__main__":
    build()
