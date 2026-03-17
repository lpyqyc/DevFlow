using Avalonia;
using System;
using DevFlow.Services;

namespace DevFlow;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var logService = new LogService();
        LogHelper.LogInfo("Program", "应用程序启动, 参数: {Args}", string.Join(", ", args));
        
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogHelper.LogError("Program", "应用程序异常退出: {Message}", ex.Message);
            logService.Error(ex, "应用程序异常退出");
            throw;
        }
        finally
        {
            LogHelper.LogInfo("Program", "应用程序退出");
            logService.Close();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
