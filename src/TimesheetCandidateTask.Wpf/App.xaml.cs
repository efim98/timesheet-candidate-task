using System.Net.Http;
using TimesheetCandidateTask.Wpf.Application;
using TimesheetCandidateTask.Wpf.Configuration;
using TimesheetCandidateTask.Wpf.Infrastructure;
using TimesheetCandidateTask.Wpf.ViewModels;

namespace TimesheetCandidateTask.Wpf;

public partial class App : System.Windows.Application
{
    public static AppSettings Settings { get; private set; } = new();
    private HttpClient? _httpClient;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        Settings = SettingsLoader.Load();

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5),
            BaseAddress = new Uri(Settings.ApiUrl)
        };
        ITimeSheetApiService apiService = new TimeSheetApiService(_httpClient);
        var workspace = new TimesheetWorkspace(apiService, Settings.RetailId);
        var viewModel = new TimesheetViewModel(workspace);
        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _httpClient?.Dispose();
        base.OnExit(e);
    }
}
