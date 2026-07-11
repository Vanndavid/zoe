using Zoe.Domain.Entities;

namespace Zoe.Application.Interfaces;

public interface IGoalRepository
{
    Task<IReadOnlyList<Goal>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Goal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Goal goal, CancellationToken cancellationToken = default);

    Task UpdateAsync(Goal goal, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
