using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;

namespace Zoe.Infrastructure.Persistence;

public sealed class InMemorySettingsRepository : ISettingsRepository
{
    private UserSettings _settings = new();

    public Task<UserSettings?> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<UserSettings?>(_settings);

    public Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        _settings = settings;
        return Task.CompletedTask;
    }
}
