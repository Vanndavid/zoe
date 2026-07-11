namespace Zoe.Domain.ValueObjects;

public sealed class EventMetadata
{
    public IReadOnlyDictionary<string, string> Tags { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static EventMetadata Empty { get; } = new();

    public static EventMetadata FromDictionary(IDictionary<string, string> tags) =>
        new() { Tags = new Dictionary<string, string>(tags, StringComparer.OrdinalIgnoreCase) };

    public string? Get(string key) =>
        Tags.TryGetValue(key, out var value) ? value : null;
}
