using System.Windows;

namespace LogAnalyzer;

public partial class ChangeLogWindow : Window
{
    public ChangeLogWindow()
    {
        InitializeComponent();
        RunVersion.Text = ChangeLog.CurrentVersion;
        HistoryList.ItemsSource = ChangeLog.History;
    }
}
