using TimesheetCandidateTask.Wpf.ViewModels;

namespace TimesheetCandidateTask.Wpf;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow(TimesheetViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
