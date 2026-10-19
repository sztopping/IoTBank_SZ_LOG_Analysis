using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using LogAnalyzer.Models;

namespace LogAnalyzer.Report;

/// <summary>
/// HTML 报告生成器：生成单文件 HTML（内嵌 ECharts + 三语 i18n + 交互图表）。
/// 交互：dataZoom 拖动缩放 + axisPointer 十字虚线指示 + tooltip 悬浮高亮；
/// 图表上方标题、坐标轴名称/单位（Y 轴纵向）。
/// </summary>
public class ReportGenerator
{
    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>生成完整 HTML 报告</summary>
    public string Generate(ReportData report)
    {
        var echarts = LoadEcharts();
        var json = JsonSerializer.Serialize(report, JsonOpt);
        // 防止 JSON 中的 </script> 破坏 HTML
        json = json.Replace("</", "<\\/");

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"zh\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"<title>{report.Devices.Count} Devices · Log Analysis Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(Css);
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // 顶部工具栏
        sb.AppendLine("<header class=\"topbar\">");
        sb.AppendLine("  <div class=\"title lt\" data-zh=\"日志分析报告\" data-ja=\"ログ分析レポート\" data-en=\"Log Analysis Report\">日志分析报告</div>");
        sb.AppendLine("  <div class=\"lang-switch\">");
        sb.AppendLine("    <button class=\"lang-btn active\" data-lang=\"zh\" onclick=\"switchLang('zh')\">中文</button>");
        sb.AppendLine("    <button class=\"lang-btn\" data-lang=\"ja\" onclick=\"switchLang('ja')\">日本語</button>");
        sb.AppendLine("    <button class=\"lang-btn\" data-lang=\"en\" onclick=\"switchLang('en')\">English</button>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</header>");

        // 概览
        sb.AppendLine("<div class=\"container\">");
        sb.AppendLine("  <section class=\"card\">");
        sb.AppendLine($"    <h2 class=\"lt\" data-zh=\"分析概览\" data-ja=\"分析概要\" data-en=\"Overview\">分析概览</h2>");
        sb.AppendLine($"    <p class=\"meta lt\" data-zh=\"设备数：{report.Devices.Count}　生成时间：{report.GeneratedAt}\" data-ja=\"デバイス数：{report.Devices.Count}　生成時刻：{report.GeneratedAt}\" data-en=\"Devices: {report.Devices.Count}  Generated: {report.GeneratedAt}\">设备数：{report.Devices.Count}　生成时间：{report.GeneratedAt}</p>");
        if (!string.IsNullOrWhiteSpace(report.RequirementSummary))
            sb.AppendLine($"    <p class=\"conclusion\">{EscapeHtml(report.RequirementSummary)}</p>");
        sb.AppendLine(BuildDeviceCards(report));
        sb.AppendLine("  </section>");

        // 各分析章节
        foreach (var section in report.Sections)
        {
            sb.AppendLine("  <section class=\"card\">");
            sb.AppendLine($"    <h2 class=\"lt\" data-zh=\"{EscapeAttr(section.Title.Zh)}\" data-ja=\"{EscapeAttr(section.Title.Ja)}\" data-en=\"{EscapeAttr(section.Title.En)}\">{EscapeHtml(section.Title.Zh)}</h2>");

            // 结论
            foreach (var c in section.Conclusions)
                sb.AppendLine($"    <p class=\"conclusion lt\" data-zh=\"{EscapeAttr(c.Zh)}\" data-ja=\"{EscapeAttr(c.Ja)}\" data-en=\"{EscapeAttr(c.En)}\">{EscapeHtml(c.Zh)}</p>");

            // 表格
            foreach (var t in section.Tables)
            {
                sb.AppendLine($"    <h3 class=\"lt\" data-zh=\"{EscapeAttr(t.Title.Zh)}\" data-ja=\"{EscapeAttr(t.Title.Ja)}\" data-en=\"{EscapeAttr(t.Title.En)}\">{EscapeHtml(t.Title.Zh)}</h3>");
                sb.AppendLine("    <div class=\"table-wrap\"><table>");
                sb.AppendLine("      <thead><tr>");
                foreach (var h in t.Headers)
                    sb.AppendLine($"        <th class=\"lt\" data-zh=\"{EscapeAttr(h.Zh)}\" data-ja=\"{EscapeAttr(h.Ja)}\" data-en=\"{EscapeAttr(h.En)}\">{EscapeHtml(h.Zh)}</th>");
                sb.AppendLine("      </tr></thead><tbody>");
                foreach (var row in t.Rows)
                {
                    sb.AppendLine("      <tr>");
                    foreach (var cell in row)
                        sb.AppendLine($"        <td>{EscapeHtml(cell)}</td>");
                    sb.AppendLine("      </tr>");
                }
                sb.AppendLine("      </tbody></table></div>");
            }

            // 图表
            foreach (var chart in section.Charts)
            {
                sb.AppendLine("    <div class=\"chart-block\">");
                sb.AppendLine($"      <div class=\"chart-title lt\" data-zh=\"{EscapeAttr(chart.Title.Zh)}\" data-ja=\"{EscapeAttr(chart.Title.Ja)}\" data-en=\"{EscapeAttr(chart.Title.En)}\">{EscapeHtml(chart.Title.Zh)}</div>");
                sb.AppendLine($"      <div class=\"chart\" id=\"{EscapeAttr(chart.ChartId)}\"></div>");
                sb.AppendLine("    </div>");
            }

            sb.AppendLine("  </section>");
        }
        sb.AppendLine("</div>");

        sb.AppendLine("<script>");
        sb.AppendLine(echarts);
        sb.AppendLine("</script>");
        sb.AppendLine("<script>");
        sb.AppendLine($"const REPORT = {json};");
        sb.AppendLine(AppJs);
        sb.AppendLine("</script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>生成设备卡片</summary>
    private static string BuildDeviceCards(ReportData report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("    <div class=\"devices\">");
        foreach (var dev in report.Devices)
        {
            sb.AppendLine("      <div class=\"device-card\">");
            sb.AppendLine($"        <div class=\"device-head\"><span class=\"model\">{EscapeHtml(dev.Model)}</span><span class=\"devid\">{EscapeHtml(dev.DeviceId)}</span></div>");
            sb.AppendLine("        <div class=\"device-fields\">");
            foreach (var f in dev.Fields)
                sb.AppendLine($"          <div class=\"field\"><span class=\"fk\">{EscapeHtml(f.Key)}</span><span class=\"fv\">{EscapeHtml(f.Value)}</span></div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("      </div>");
        }
        sb.AppendLine("    </div>");
        return sb.ToString();
    }

    private static string LoadEcharts()
    {
        var asm = Assembly.GetExecutingAssembly();
        var resName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("echarts.min.js", StringComparison.OrdinalIgnoreCase));
        if (resName == null) return "// ECharts 资源未找到";
        using var stream = asm.GetManifestResourceStream(resName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string EscapeHtml(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string EscapeAttr(string s)
        => EscapeHtml(s).Replace("\"", "&quot;");

    private const string Css = @"
* { box-sizing: border-box; margin: 0; padding: 0; }
body { font-family: -apple-system, 'Segoe UI', 'Microsoft YaHei', 'Hiragino Sans', sans-serif; background: #f5f6f8; color: #1f2937; padding-bottom: 40px; }
.topbar { position: sticky; top: 0; z-index: 100; display: flex; justify-content: space-between; align-items: center; background: #ffffff; border-bottom: 1px solid #e5e7eb; padding: 14px 24px; }
.topbar .title { font-size: 18px; font-weight: 600; }
.lang-switch { display: flex; gap: 6px; }
.lang-btn { border: 1px solid #d1d5db; background: #fff; color: #374151; padding: 6px 14px; border-radius: 6px; cursor: pointer; font-size: 13px; }
.lang-btn.active { background: #2563eb; color: #fff; border-color: #2563eb; }
.container { max-width: 1200px; margin: 20px auto; padding: 0 20px; }
.card { background: #fff; border-radius: 12px; padding: 24px; margin-bottom: 20px; box-shadow: 0 1px 3px rgba(0,0,0,0.06); }
.card h2 { font-size: 16px; margin-bottom: 12px; padding-left: 10px; border-left: 4px solid #2563eb; }
.card h3 { font-size: 14px; margin: 16px 0 8px; color: #374151; }
.meta { color: #6b7280; font-size: 13px; margin-bottom: 16px; }
.conclusion { background: #eff6ff; border-left: 3px solid #3b82f6; padding: 10px 14px; border-radius: 4px; font-size: 13px; margin: 8px 0; line-height: 1.6; }
.devices { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 12px; }
.device-card { border: 1px solid #e5e7eb; border-radius: 8px; padding: 12px; }
.device-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }
.model { background: #eef2ff; color: #4338ca; padding: 2px 8px; border-radius: 4px; font-size: 12px; font-weight: 600; }
.devid { font-family: 'Consolas', monospace; font-size: 13px; color: #111827; }
.device-fields { display: flex; flex-direction: column; gap: 4px; }
.field { display: flex; font-size: 12px; }
.fk { color: #6b7280; width: 90px; flex-shrink: 0; font-family: 'Consolas', monospace; }
.fv { color: #1f2937; word-break: break-all; font-family: 'Consolas', monospace; }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: 13px; }
th, td { border: 1px solid #e5e7eb; padding: 8px 10px; text-align: left; }
th { background: #f9fafb; font-weight: 600; color: #374151; }
td { font-family: 'Consolas', monospace; color: #1f2937; }
tr:nth-child(even) td { background: #fafafa; }
.chart-block { margin: 18px 0; }
.chart-title { font-size: 14px; font-weight: 600; color: #111827; margin-bottom: 8px; text-align: center; }
.chart { width: 100%; height: 420px; }
";

    private const string AppJs = @"
let currentLang = 'zh';
const chartInstances = [];

function L(t) {
  if (!t) return '';
  if (typeof t === 'string') return t;
  return t[currentLang] || t.zh || t.Zh || '';
}

function axisName(axis) {
  const n = L(axis.name);
  if (axis.unit) return n + ' (' + axis.unit + ')';
  return n;
}

function buildOption(chart) {
  const isBox = chart.type === 'boxplot';
  const isBar = chart.type === 'bar';
  const option = {
    animation: false,
    textStyle: { fontFamily: '-apple-system, Segoe UI, Microsoft YaHei, sans-serif' },
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'cross', crossStyle: { color: '#6b7280' }, label: { backgroundColor: '#374151' } },
      backgroundColor: 'rgba(255,255,255,0.96)',
      borderColor: '#d1d5db',
      borderWidth: 1,
      textStyle: { color: '#111827' },
      confine: true
    },
    legend: { type: 'scroll', top: 4, left: 'center', icon: 'roundRect' },
    grid: { left: 72, right: 40, top: 56, bottom: 72 },
    dataZoom: [
      { type: 'inside', xAxisIndex: 0, filterMode: 'none' },
      { type: 'slider', xAxisIndex: 0, bottom: 12, height: 22, borderColor: '#d1d5db' }
    ],
    xAxis: {
      type: chart.xAxis.type,
      name: axisName(chart.xAxis),
      nameLocation: 'middle',
      nameGap: 42,
      nameTextStyle: { color: '#374151' },
      axisLabel: { color: '#6b7280' },
      ...(chart.xCategories ? { data: chart.xCategories } : {})
    },
    yAxis: {
      type: 'value',
      name: axisName(chart.yAxis),
      nameLocation: 'middle',
      nameGap: 52,
      nameRotate: 90,
      nameTextStyle: { color: '#374151' },
      axisLabel: { color: '#6b7280' },
      splitLine: { lineStyle: { color: '#f0f0f0' } }
    },
    series: chart.series.map(s => {
      if (isBox) {
        return {
          name: s.name, type: 'boxplot',
          data: s.data,
          itemStyle: { color: '#3b82f6', borderColor: '#2563eb' }
        };
      }
      if (isBar) {
        return { name: s.name, type: 'bar', data: s.data, barMaxWidth: 40 };
      }
      return {
        name: s.name, type: 'line', data: s.data,
        showSymbol: s.data.length <= 120, symbol: 'circle', symbolSize: 5,
        smooth: s.data.length > 30, lineStyle: { width: 2 }
      };
    })
  };
  return option;
}

function renderAll() {
  chartInstances.forEach(c => c.inst.dispose());
  chartInstances.length = 0;
  REPORT.sections.forEach(section => {
    (section.charts || []).forEach(chart => {
      const dom = document.getElementById(chart.chartId);
      if (!dom) return;
      const inst = echarts.init(dom);
      inst.setOption(buildOption(chart));
      chartInstances.push({ inst, chart });
    });
  });
}

function switchLang(lang) {
  currentLang = lang;
  document.querySelectorAll('.lang-btn').forEach(b => b.classList.toggle('active', b.dataset.lang === lang));
  document.querySelectorAll('.lt').forEach(el => {
    const v = el.dataset[lang];
    if (v !== undefined) el.textContent = v;
  });
  chartInstances.forEach(({ inst, chart }) => inst.setOption(buildOption(chart)));
}

window.addEventListener('resize', () => chartInstances.forEach(c => c.inst.resize()));
renderAll();
";
}
