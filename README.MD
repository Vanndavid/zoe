# Zoe — AI Personal Mentor

Zoe is a Windows desktop AI mentor that observes your digital behavior, compares it against your goals, and intervenes at the right moment with evidence-based coaching.

## Architecture

```
Sensors → Event Bus → Event Store (SQLite) → Context Engine → Rule Engine → Decision Engine → AI Engine → UI
```

Built with Clean Architecture + Event-Driven design on .NET 8.

## Solution Structure

```
Zoe.sln
src/
  Zoe.Domain/           Entities, enums, value objects
  Zoe.Application/      Service interfaces and business logic
  Zoe.Infrastructure/   SQLite, EF Core, event bus, DI
  Zoe.Windows/          WindowMonitor, IdleMonitor (Windows)
  Zoe.AI/               Prompt builder, coach service, LLM client
  Zoe.UI/               WPF dashboard (Windows only — build on Windows)
  Zoe.Host/             Console harness for dev testing
tests/
  Zoe.Tests/            Unit and integration tests (30 tests)
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 (1809+) or 11 for the WPF UI, toast notifications, and live activity monitoring

## Build and Test (Linux or Windows)

```bash
dotnet restore
dotnet build
dotnet test
```

## Run the Console Harness

Publishes sample events and runs context, memory, coaching, and export pipelines:

```bash
dotnet run --project src/Zoe.Host/Zoe.Host.csproj
```

## Run the WPF App (Windows only)

```bash
dotnet run --project src/Zoe.UI/Zoe.UI.csproj
```

The WPF app provides a timeline view, goal management, settings (including life profile), and monitoring controls.

While monitoring is running, Zoe checks every minute whether to intervene and shows a Windows toast when it does. It stays quiet while you are idle, outside work hours (default 09:00–17:00), during deep work, and for 15 minutes after each intervention (max 3 per hour). The check interval is `Coaching:EvaluationIntervalSeconds` in `src/Zoe.UI/appsettings.json`.

## Implemented Phases

| Phase | Status | Components |
|-------|--------|------------|
| 1. Foundation | Done | Event bus, SQLite store, domain model, tests |
| 2. Sensors | Done | WindowMonitor, IdleMonitor, MonitoringService |
| 3. UI Shell | Done | WPF app with timeline, goals, settings (Windows) |
| 4. Context | Done | ContextService, TimelineService, StatisticsService |
| 5. Rules + Decisions | Done | RuleEngine, DecisionEngine with evidence |
| 6. AI Coaching | Done | CoachService, PromptBuilder, offline LLM fallback |
| 7. Memory | Done | MemoryService with pattern extraction (SQLite-backed) |
| 8. MVP Polish | Done | Weekly summaries, data export, notifications stub |
| 9. Personal state | Done | Goals, settings, and memories persisted in SQLite |
| 10. Coaching delivery | Done | Background coaching loop, Windows toasts, persisted cooldown |

## Event Schema

Every activity event follows a unified schema: `EventId`, `Timestamp`, `Source`, `Type`, `Payload`, `Confidence`, `SessionId`, `CorrelationId`, `Metadata`.

## Privacy

All data is stored locally in SQLite — activity events, goals, settings, and memories. Export and delete capabilities included. No cloud sync.

## Next Steps

- Connect OpenAI API for live AI coaching (replace `RuleBasedLlmClient`)
- Test WindowMonitor, IdleMonitor, and toast notifications on your Windows machine
- Add system tray icon and auto-start registry entry
- Let users edit work hours and intervention cooldown in Settings

## License

Private project — for personal use.
