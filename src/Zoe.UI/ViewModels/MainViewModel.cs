using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;

namespace Zoe.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IMonitoringService _monitoringService;
    private readonly ITimelineService _timelineService;
    private readonly IGoalRepository _goalRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly TimelineViewModel _timelineViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly DispatcherTimer _timelineRefreshTimer;

    public MainViewModel(
        IMonitoringService monitoringService,
        ITimelineService timelineService,
        IGoalRepository goalRepository,
        ISettingsRepository settingsRepository,
        TimelineViewModel timelineViewModel,
        SettingsViewModel settingsViewModel)
    {
        _monitoringService = monitoringService;
        _timelineService = timelineService;
        _goalRepository = goalRepository;
        _settingsRepository = settingsRepository;
        _timelineViewModel = timelineViewModel;
        _settingsViewModel = settingsViewModel;

        TimelineItems = _timelineViewModel.Items;
        Goals = new ObservableCollection<Goal>();
        MonitoringEnabled = true;
        AutoStart = true;

        // Shows new activity as the monitors record it.
        _timelineRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timelineRefreshTimer.Tick += async (_, _) => await _timelineViewModel.LoadTodayAsync();

        _ = LoadAsync();
    }

    public ObservableCollection<TimelineItemViewModel> TimelineItems { get; }

    public ObservableCollection<Goal> Goals { get; }

    [ObservableProperty]
    private string _monitoringButtonText = "Start Monitoring";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _monitoringEnabled;

    [ObservableProperty]
    private bool _autoStart;

    [ObservableProperty]
    private string _lifeProfile = string.Empty;

    [RelayCommand]
    private async Task ToggleMonitoringAsync()
    {
        if (_monitoringService.IsRunning)
        {
            await _monitoringService.StopAsync();
            _timelineRefreshTimer.Stop();
            MonitoringButtonText = "Start Monitoring";
            StatusText = "Monitoring paused";
        }
        else
        {
            await _monitoringService.StartAsync();
            _timelineRefreshTimer.Start();
            MonitoringButtonText = "Stop Monitoring";
            StatusText = "Monitoring active";
        }

        await RefreshTimelineAsync();
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        await _settingsViewModel.SaveAsync(MonitoringEnabled, AutoStart, LifeProfile);
        StatusText = "Settings saved";
    }

    private async Task LoadAsync()
    {
        var settings = await _settingsRepository.GetAsync();
        if (settings is not null)
        {
            MonitoringEnabled = settings.MonitoringEnabled;
            AutoStart = settings.AutoStartWithWindows;
            LifeProfile = settings.LifeProfile;
        }

        var goals = await _goalRepository.GetAllAsync();
        Goals.Clear();
        foreach (var goal in goals)
        {
            Goals.Add(goal);
        }

        await RefreshTimelineAsync();
    }

    private async Task RefreshTimelineAsync()
    {
        await _timelineViewModel.LoadTodayAsync();
        StatusText = $"Timeline updated — {TimelineItems.Count} events today";
    }
}
