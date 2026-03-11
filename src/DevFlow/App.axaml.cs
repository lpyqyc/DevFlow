using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using DevFlow.Services;
using DevFlow.ViewModels;
using DevFlow.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DevFlow;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            
            var mainWindowViewModel = Services.GetRequiredService<MainWindowViewModel>();
            
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IDeviceRegistry, DeviceRegistry>();
        services.AddSingleton<IFlowExecutor, FlowExecutor>();
        services.AddTransient<MainWindowViewModel>();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
