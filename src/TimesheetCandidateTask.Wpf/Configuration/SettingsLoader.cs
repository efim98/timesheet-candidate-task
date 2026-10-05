using Microsoft.Extensions.Configuration;

namespace TimesheetCandidateTask.Wpf.Configuration;

public static class SettingsLoader
{
    public static AppSettings Load()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        return config.Get<AppSettings>()
            ?? throw new InvalidOperationException("Не удалось загрузить appsettings.json.");
    }
}