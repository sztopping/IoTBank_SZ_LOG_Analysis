using System.IO;
using System.Windows;
using System.Windows.Controls;
using LogAnalyzer.Analysis;
using LogAnalyzer.Models;
using LogAnalyzer.Report;
using LogAnalyzer.Services;

namespace LogAnalyzer;

public partial class MainWindow : Window
{
    private readonly LogImportService _importService = new();
    private readonly AnalysisEngine _engine = new();
    private readonly ReportGenerator _reportGenerator = new();

    private List<DeviceProfile> _devices = new();
    private string? _lastReportHtml;

    public MainWindow()
    {
        InitializeComponent();
        Title = $"Log 分析工具 {ChangeLog.CurrentVersion}";
        _ = InitWebViewAsync();
    }

    /// <summary>
    /// 窗口加载后异步检测联网。当前核心功能（解析/统计/报告）均为本地实现、离线可用；
    /// 此检测为「需联网功能」的前置提示预留。若离线，弹窗告知。
    /// </summary>
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var online = await NetworkHelper.IsOnlineAsync();
        if (!online)
        {
            MessageBox.Show(
                "当前电脑未联网。\n\n本工具的日志解析、统计分析与报告生成均为本地实现，离线即可使用。\n若后续启用需要联网的功能（如 AI 联网智能解析），会先提示您连接网络。",
                "联网提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async Task InitWebViewAsync()
    {
        try
        {
            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment
                .CreateAsync(userDataFolder: Path.Combine(AppContext.BaseDirectory, "WebView2Data"));
            await WebView.EnsureCoreWebView2Async(env);
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        }
        catch
        {
            // WebView2 运行时未安装时静默降级，预览区显示占位文本
        }
    }

    private void BtnOpenFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择日志文件",
            Filter = "日志文件 (*.txt;*.log)|*.txt;*.log|所有文件 (*.*)|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog() == true)
            Import(dlg.FileNames);
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "选择日志文件夹" };
        if (dlg.ShowDialog() == true)
            Import(new[] { dlg.FolderName });
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            Import((string[])e.Data.GetData(DataFormats.FileDrop));
    }

    private async void Import(IEnumerable<string> paths)
    {
        var result = await _importService.ImportAsync(paths);
        var profiles = result.Devices;

        if (profiles.Count == 0)
        {
            MessageBox.Show("未识别到可解析的日志（或机型暂未支持）。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _devices = profiles;
        DeviceList.ItemsSource = _devices;
        DeviceList.SelectAll();
        ChkSelectAll.IsChecked = true;
        TxtDeviceCount.Text = $"已导入 {_devices.Count} 台设备";

        // 解析提示（如「未联网，仅出本地解析报告」）
        if (result.Messages.Count > 0)
        {
            MessageBox.Show(string.Join("\n\n", result.Messages), "解析提示",
                MessageBoxButton.OK,
                result.AiNeededButOffline ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
    }

    private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (ChkSelectAll.IsChecked == true) DeviceList.SelectAll();
        else DeviceList.UnselectAll();
    }

    private void BtnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        var selected = DeviceList.SelectedItems.Cast<DeviceProfile>().ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("请先勾选至少一台设备。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var report = _engine.Run(selected);
        _lastReportHtml = _reportGenerator.Generate(report);

        TxtPlaceholder.Visibility = Visibility.Collapsed;
        BtnSaveReport.IsEnabled = true;
        MenuExport.IsEnabled = true;
        ShowReport(_lastReportHtml);
    }

    private void BtnSaveReport_Click(object sender, RoutedEventArgs e)
    {
        if (_lastReportHtml == null) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出 HTML 报告",
            Filter = "HTML 报告 (*.html)|*.html",
            FileName = $"日志分析报告_{DateTime.Now:yyyyMMdd_HHmmss}.html"
        };
        if (dlg.ShowDialog() == true)
        {
            File.WriteAllText(dlg.FileName, _lastReportHtml, System.Text.Encoding.UTF8);
            MessageBox.Show($"报告已保存到：{dlg.FileName}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();

    private void MenuChangeLog_Click(object sender, RoutedEventArgs e)
    {
        new ChangeLogWindow { Owner = this }.ShowDialog();
    }

    private void MenuSop_Click(object sender, RoutedEventArgs e)
    {
        var sopPath = Path.Combine(AppContext.BaseDirectory, "docs", "SOP_操作说明.html");
        if (!File.Exists(sopPath))
        {
            MessageBox.Show("未找到操作说明文件（docs/SOP_操作说明.html）。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sopPath) { UseShellExecute = true });
        }
        catch
        {
            MessageBox.Show("无法打开操作说明文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ShowReport(string html)
    {
        try
        {
            if (WebView.CoreWebView2 != null)
                WebView.NavigateToString(html);
        }
        catch
        {
            // 预览不可用时忽略
        }
    }
}
