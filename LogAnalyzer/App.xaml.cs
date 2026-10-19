using System.Text;
using System.Windows;
using LogAnalyzer.Services;

namespace LogAnalyzer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // 注册编码提供程序，支持 GBK 解码
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        // 加载 AI 解析配置（ai_config.json）
        AiConfig.Load();
        base.OnStartup(e);
    }
}
