using Zoe.Domain.Entities;

namespace Zoe.Application.Interfaces;

public interface IContextService
{
    Task<Context> GetCurrentContextAsync(CancellationToken cancellationToken = default);
}

public interface ITimelineService
{
    Task<IReadOnlyList<ActivityEvent>> GetTimelineAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}

public interface IStatisticsService
{
    Task<DailySummary> GetDailySummaryAsync(DateOnly date, CancellationToken cancellationToken = default);

    Task<WeeklySummary> GetWeeklySummaryAsync(DateOnly weekStart, CancellationToken cancellationToken = default);
}

public interface IMemoryService
{
    Task<IReadOnlyList<Memory>> GetRelevantMemoriesAsync(int limit = 10, CancellationToken cancellationToken = default);

    Task ExtractMemoriesFromHistoryAsync(CancellationToken cancellationToken = default);
}

public interface ICoachService
{
    Task<Intervention?> EvaluateInterventionAsync(CancellationToken cancellationToken = default);

    Task<string> GenerateDailyReflectionAsync(DateOnly date, CancellationToken cancellationToken = default);

    Task<string> GenerateWeeklyReviewAsync(DateOnly weekStart, CancellationToken cancellationToken = default);
}

public interface IInterventionDeliveryService
{
    /// <summary>
    /// Asks the coach whether to intervene and, if so, shows the intervention and records
    /// an InterventionTriggered event. Returns null when no intervention was warranted.
    /// </summary>
    Task<Intervention?> EvaluateAndDeliverAsync(CancellationToken cancellationToken = default);
}

public interface IRuleEngine
{
    RuleEvaluationResult Evaluate(Context context, UserSettings settings);
}

public interface IDecisionEngine
{
    Task<Intervention?> DecideAsync(
        Context context,
        RuleEvaluationResult ruleResult,
        UserSettings settings,
        CancellationToken cancellationToken = default);
}

public sealed class RuleEvaluationResult
{
    public double DistractionScore { get; init; }

    public bool ShouldSuppressIntervention { get; init; }

    public IReadOnlyList<string> TriggeredRules { get; init; } = [];

    public IReadOnlyList<string> Evidence { get; init; } = [];
}
