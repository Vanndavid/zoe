using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;

namespace Zoe.Infrastructure.Persistence;

public sealed class InMemoryGoalRepository : IGoalRepository
{
    private readonly List<Goal> _goals = [];

    public Task<IReadOnlyList<Goal>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Goal>>(_goals.ToList());

    public Task<Goal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_goals.FirstOrDefault(g => g.Id == id));

    public Task AddAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        _goals.Add(goal);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        var index = _goals.FindIndex(g => g.Id == goal.Id);
        if (index >= 0)
        {
            _goals[index] = goal;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _goals.RemoveAll(g => g.Id == id);
        return Task.CompletedTask;
    }
}
