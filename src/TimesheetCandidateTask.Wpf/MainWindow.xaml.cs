using TimesheetCandidateTask.Wpf.Application;
using TimesheetCandidateTask.Wpf.ViewModels;

namespace TimesheetCandidateTask.Wpf;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new TimesheetViewModel(new TimesheetWorkspace());
    }
}
