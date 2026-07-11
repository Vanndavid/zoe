using Zoe.Domain.Entities;

namespace Zoe.Application.Interfaces;

public interface ISettingsRepository
{
    Task<UserSettings?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default);
}
