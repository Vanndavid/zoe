using Zoe.Application.Interfaces;

namespace Zoe.UI.ViewModels;

public sealed class SettingsViewModel
{
    private readonly ISettingsRepository _settingsRepository;

    public SettingsViewModel(ISettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    public async Task SaveAsync(bool monitoringEnabled, bool autoStart, string lifeProfile)
    {
        var current = await _settingsRepository.GetAsync() ?? new Domain.Entities.UserSettings();
        await _settingsRepository.SaveAsync(new Domain.Entities.UserSettings
        {
            Id = current.Id,
            MonitoringEnabled = monitoringEnabled,
            AutoStartWithWindows = autoStart,
            LifeProfile = lifeProfile,
            WorkDayStart = current.WorkDayStart,
            WorkDayEnd = current.WorkDayEnd,
            InterventionCooldownMinutes = current.InterventionCooldownMinutes,
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }
}
