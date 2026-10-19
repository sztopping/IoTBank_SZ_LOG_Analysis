using System.Text;
using System.Windows;
using LogAnalyzer.Services;

namespace LogAnalyzer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // .NET Framework 4.8 默认支持 GBK 等代码页编码，无需 RegisterProvider
        // 加载 AI 解析配置（ai_config.json）
        AiConfig.Load();
        base.OnStartup(e);
    }
}
