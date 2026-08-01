using Zoe.Domain.Entities;

namespace Zoe.Application.Interfaces;

public interface IMemoryRepository
{
    Task<IReadOnlyList<Memory>> GetRelevantAsync(
        int limit = 10,
        CancellationToken cancellationToken = default);

    Task ReplaceByCategoryAsync(
        string category,
        Memory memory,
        CancellationToken cancellationToken = default);
}
