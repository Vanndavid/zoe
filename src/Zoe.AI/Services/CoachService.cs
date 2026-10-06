using Microsoft.Extensions.Logging;
using Zoe.AI.Interfaces;
using Zoe.AI.Prompts;
using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;

namespace Zoe.AI.Services;

public sealed class RuleBasedLlmClient : ILlmClient
{
    public Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        // Offline fallback when no API key is configured.
        var response = userPrompt.Contains("weekly", StringComparison.OrdinalIgnoreCase)
            ? "This week showed consistent focus periods with some distraction spikes. Your best days aligned with deep work blocks. Next week, try protecting your first 2 hours each morning for your highest-priority goal."
            : userPrompt.Contains("reflection", StringComparison.OrdinalIgnoreCase)
                ? "Today you maintained reasonable focus with identifiable distraction windows. Your productive apps dominated the timeline. Tomorrow, watch for early-afternoon drift and pre-commit to a 25-minute focus block after lunch."
                : "Based on your current activity, you're drifting from your stated goals. The evidence shows lower alignment right now — consider switching back to your primary task.";

        return Task.FromResult(response);
    }
}

public sealed class CoachService : ICoachService
{
    private readonly IContextService _contextService;
    private readonly IRuleEngine _ruleEngine;
    private readonly IDecisionEngine _decisionEngine;
    private readonly IStatisticsService _statisticsService;
    private readonly IMemoryService _memoryService;
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILlmClient _llmClient;
    private readonly ILogger<CoachService> _logger;

    public CoachService(
        IContextService contextService,
        IRuleEngine ruleEngine,
        IDecisionEngine decisionEngine,
        IStatisticsService statisticsService,
        IMemoryService memoryService,
        ISettingsRepository settingsRepository,
        ILlmClient llmClient,
        ILogger<CoachService> logger)
    {
        _contextService = contextService;
        _ruleEngine = ruleEngine;
        _decisionEngine = decisionEngine;
        _statisticsService = statisticsService;
        _memoryService = memoryService;
        _settingsRepository = settingsRepository;
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<Intervention?> EvaluateInterventionAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsRepository.GetAsync(cancellationToken) ?? new UserSettings();
        var context = await _contextService.GetCurrentContextAsync(cancellationToken);
        var ruleResult = _ruleEngine.Evaluate(context, settings);
        var decision = await _decisionEngine.DecideAsync(context, ruleResult, settings, cancellationToken);

        if (decision is null)
        {
            return null;
        }

        var memories = await _memoryService.GetRelevantMemoriesAsync(cancellationToken: cancellationToken);
        var aiMessage = await _llmClient.CompleteAsync(
            PromptBuilder.BuildCoachingSystemPrompt(settings),
            PromptBuilder.BuildInterventionPrompt(context, ruleResult, memories),
            cancellationToken);

        // Delivery (and recording it) is the caller's job — see IInterventionDeliveryService.
        var intervention = new Intervention
        {
            Action = decision.Action,
            Message = aiMessage,
            Evidence = decision.Evidence,
            Confidence = decision.Confidence,
            WasDelivered = false
        };

        _logger.LogInformation("Intervention generated: {Action}", intervention.Action);
        return intervention;
    }

    public async Task<string> GenerateDailyReflectionAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsRepository.GetAsync(cancellationToken) ?? new UserSettings();
        var summary = await _statisticsService.GetDailySummaryAsync(date, cancellationToken);

        return await _llmClient.CompleteAsync(
            PromptBuilder.BuildCoachingSystemPrompt(settings),
            PromptBuilder.BuildDailyReflectionPrompt(summary, settings),
            cancellationToken);
    }

    public async Task<string> GenerateWeeklyReviewAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsRepository.GetAsync(cancellationToken) ?? new UserSettings();
        var summary = await _statisticsService.GetWeeklySummaryAsync(weekStart, cancellationToken);

        return await _llmClient.CompleteAsync(
            PromptBuilder.BuildCoachingSystemPrompt(settings),
            PromptBuilder.BuildWeeklyReviewPrompt(summary, settings),
            cancellationToken);
    }
}
