namespace Zoe.Domain.ValueObjects;

public sealed class EventPayload
{
    public IReadOnlyDictionary<string, string> Data { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static EventPayload Empty { get; } = new();

    public static EventPayload FromDictionary(IDictionary<string, string> data) =>
        new() { Data = new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase) };

    public string? Get(string key) =>
        Data.TryGetValue(key, out var value) ? value : null;

    public EventPayload With(string key, string value)
    {
        var copy = new Dictionary<string, string>(Data, StringComparer.OrdinalIgnoreCase)
        {
            [key] = value
        };

        return new EventPayload { Data = copy };
    }
}
