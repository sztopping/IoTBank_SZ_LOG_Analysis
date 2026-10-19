using System.Windows;

namespace LogAnalyzer;

public partial class RequirementDialog : Window
{
    /// <summary>用户输入的需求文本（跳过或取消时为 null/空）</summary>
    public string? Requirement { get; private set; }

    /// <summary>用户是否选择了「跳过」（全自动分析）</summary>
    public bool Skipped { get; private set; }

    public RequirementDialog()
    {
        InitializeComponent();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Requirement = TxtRequirement.Text?.Trim();
        Skipped = false;
        DialogResult = true;
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        Requirement = null;
        Skipped = true;
        DialogResult = true;
    }
}
