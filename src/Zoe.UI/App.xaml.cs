using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zoe.AI.DependencyInjection;
using Zoe.Infrastructure.DependencyInjection;
using Zoe.UI.ViewModels;
using Zoe.Windows.Services;

namespace Zoe.UI;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(builder =>
            {
                builder.SetBasePath(AppContext.BaseDirectory);
                builder.AddJsonFile("appsettings.json", optional: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddZoeLogging();
                services.AddZoeInfrastructure(context.Configuration);
                services.AddZoeAi();
                services.AddZoeWindowsMonitoring();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<TimelineViewModel>();
                services.AddSingleton<SettingsViewModel>();
            })
            .Build();

        await _host.Services.EnsureZoeDatabaseCreatedAsync();

        var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
        var mainWindow = new MainWindow { DataContext = mainViewModel };
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
