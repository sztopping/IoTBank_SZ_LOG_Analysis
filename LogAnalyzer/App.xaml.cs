using System;
using System.IO;
using System.Threading;
using System.Windows;
using LogAnalyzer.Services;

namespace LogAnalyzer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 单实例限制：防止多开导致多个相同窗口
        _singleInstanceMutex = new Mutex(true, "LogAnalyzer_SingleInstance_Mutex", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("程序已在运行中，请勿重复打开。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // 全局异常捕获：防止未处理异常导致进程直接退出，并记录到 error.log
        DispatcherUnhandledException += (s, args) =>
        {
            LogException("DispatcherUnhandledException", args.Exception);
            MessageBox.Show("程序发生错误（详见 error.log）：\n" + args.Exception.Message,
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true; // 防止崩溃退出
        };
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            LogException("AppDomain.UnhandledException", args.ExceptionObject as Exception);
        };
        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            LogException("UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        // .NET Framework 4.8 默认支持 GBK 等代码页编码，无需 RegisterProvider
        // 加载 AI 解析配置（ai_config.json）
        AiConfig.Load();
        base.OnStartup(e);
    }

    private static void LogException(string source, Exception? ex)
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}\n{ex}\n\n");
        }
        catch
        {
            // 日志写入失败时忽略
        }
    }
}
