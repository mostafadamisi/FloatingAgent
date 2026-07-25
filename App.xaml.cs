using System.IO;
using System.Windows;
using FloatingAgent.Services;

namespace FloatingAgent;

public partial class App : Application
{
    public static BlazorBridge Bridge { get; } = new();
    public static ScreenAutomationService ScreenAutomation { get; } = new();
    public static OpenCodeApiClient? ApiClient { get; set; }

    private static readonly string LogPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "crash.log");

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        InitializeApiClient();
    }

    private void InitializeApiClient()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var config = System.Text.Json.JsonSerializer.Deserialize<Models.AppConfig>(json);
                if (config != null)
                {
                    ApiClient = new OpenCodeApiClient(config.OpenCodeApiUrl, config.ApiKey, config.Model);
                    Bridge.Initialize(ApiClient, ScreenAutomation);
                }
            }
            catch { }
        }
    }

    private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog("DispatcherUnhandledException", e.Exception);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            WriteCrashLog("AppDomain.UnhandledException", ex);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (e.Exception != null)
            WriteCrashLog("TaskScheduler.UnobservedTaskException", e.Exception);
    }

    private static void WriteCrashLog(string source, Exception ex)
    {
        try
        {
            var entry = $"""
                --- Crash at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---
                Source: {source}
                Exception: {ex.GetType().FullName}
                Message: {ex.Message}
                Stack Trace:
                {ex}

                """;
            File.AppendAllText(LogPath, entry);
        }
        catch { }
    }
}
