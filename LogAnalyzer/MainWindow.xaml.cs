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
    private string? _requirement; // 用户输入的分析需求

    public MainWindow()
    {
        InitializeComponent();
        Title = $"Log 分析工具 {ChangeLog.CurrentVersion}";
        _ = InitWebViewAsync();
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
        using (var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "选择日志文件夹" })
        {
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                Import(new[] { dlg.SelectedPath });
        }
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            Import((string[])e.Data.GetData(System.Windows.DataFormats.FileDrop));
    }

    private async void Import(IEnumerable<string> paths)
    {
        try
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

            // 弹出需求输入对话框，上位机根据需求内容自行判断分析方法
            var reqDlg = new RequirementDialog { Owner = this };
            if (reqDlg.ShowDialog() == true)
            {
                _requirement = reqDlg.Requirement;
                GenerateReport();
            }
        }
        catch (Exception ex)
        {
            LogException(ex);
            MessageBox.Show("导入日志时发生错误：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void LogException(Exception ex)
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Import\n{ex}\n\n");
        }
        catch { }
    }

    private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (ChkSelectAll.IsChecked == true) DeviceList.SelectAll();
        else DeviceList.UnselectAll();
    }

    private void BtnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        // 已有需求则直接生成；否则再次弹出需求对话框
        if (_requirement == null)
        {
            var reqDlg = new RequirementDialog { Owner = this };
            if (reqDlg.ShowDialog() == true)
                _requirement = reqDlg.Requirement;
        }
        GenerateReport();
    }

    private void GenerateReport()
    {
        var selected = DeviceList.SelectedItems.Cast<DeviceProfile>().ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("请先勾选至少一台设备。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var report = _engine.Run(selected, _requirement);
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
