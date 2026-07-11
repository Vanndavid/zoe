using Zoe.Domain.Entities;

namespace Zoe.AI.Interfaces;

public interface ILlmClient
{
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}
