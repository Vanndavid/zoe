# Zoe Roadmap

Long-range plan, written October 2026 at commit `1639927`. A visual version is published as a private claude.ai artifact: https://claude.ai/artifact/3EstbNiniqmXEJQ3iEKyLf

**North star:** each day, week and quarter, Zoe answers *"Am I spending my life on what I said matters?"* with evidence, and helps close the gap.

## Decisions

- **Who it's for:** one person on their own Windows PC. No installer, onboarding, multi-user or cross-platform work.
- **Core job:** mentor first. Daily and weekly reflection, goal planning and conversation lead; real-time nudges come second.
- **AI:** Claude through the cloud API (official `Anthropic` NuGet package), behind a privacy boundary around everything that leaves the machine.

## Where Zoe stands

**Works:** sensors → event bus → SQLite → context → rules → decision → delivery → toast, with 30 tests. The decide/deliver split (`InterventionDeliveryService`), the hosted loop (`CoachingLoopService`) and persisted cooldowns are solid foundations.

**Faked or missing:**
- "Goal alignment" ignores goals. It comes from hard-coded app lists, copied with small differences into `ContextService`, `RuleEngine`, `MemoryService` and `StatisticsService`.
- `RuleBasedLlmClient` returns one of three canned strings.
- Memory is three heuristics in `MemoryService`. `Memory.ReferenceCount` and `LastReferencedAt` are never used.
- `StatisticsService` uses the UTC day and assumes 8 hours minus idle as active time.
- There is no way to answer Zoe, reflect or chat. `EventType.UserReflection` and `FocusSession` are unused.
- Goals can be listed in the UI but not created.
- The database path is relative to the launch folder, logs go only to the console (a WinExe has none), the schema uses `EnsureCreated` plus hand-written SQL, and there is no tray icon, working auto-start or CI.

## Target architecture

1. **Activity spans.** Compact `WindowChanged` and `Idle*` events into spans (start, end, app, title, category, goal). Stats, context, memory and Claude's tools all read spans, replacing three separate duration calculations.
2. **One `IActivityClassifier`.** App and title rules (browser title → site) stored in SQLite, editable, linked to goals, and learning from corrections. Replaces the four hard-coded lists.
3. **Mentor layer in `Zoe.AI`.** A `ClaudeLlmClient` behind the existing `ILlmClient` for nudges, daily reflection, weekly review, a chat agent with tools, and a memory distiller. `RuleBasedLlmClient` stays as the offline fallback.
4. **Privacy boundary.** One `OutboundRedactor` in front of every request: exclusion lists for apps and title patterns, email and number scrubbing, an in-app log of what was sent, and the API key in Windows Credential Manager.

**Ground rule:** deterministic rules decide *when* Zoe interrupts. Claude only decides *what to say*.

## Stages

Each stage leaves Zoe usable every day. Sizes are upper estimates for part-time work.

### Stage 1: Live with it (≈2–3 weeks)
- Database, logs and backups in `%LOCALAPPDATA%\Zoe`, with a Serilog file sink.
- EF Core migrations, starting from a baseline of the current schema (replaces `EnsurePersonalStateTablesAsync`).
- Tray icon, single instance, start minimized, and auto-start (HKCU Run) wired to `AutoStartWithWindows`.
- Local-day stats in `StatisticsService`, with real active time.
- GitHub Actions CI that builds and tests on `windows-latest`.

**Done when:** Zoe runs all day for two weeks across restarts without opening a terminal.

### Stage 2: A truthful picture of the day (≈3–4 weeks)
- Spans projection, built incrementally and rebuildable from raw events.
- `IActivityClassifier` with editable categories; the four hard-coded lists removed.
- Goal create, edit and delete in the UI, and a richer `Goal`: why it matters, life area, horizon, weekly-hours target.
- A Today view: the day as spans, with hours per goal against target.

**Done when:** Zoe's version of the day matches memory of it, and "hours on goal X this week" is trustworthy.

### Stage 3: Zoe gets a voice (≈2–3 weeks)
- `ClaudeLlmClient` on the `Anthropic` NuGet package, model `claude-opus-5-5`, effort set per job (low for nudges, high for the weekly review).
- Prompt caching on the stable prefix: instructions, life profile, goals.
- `OutboundRedactor`, the "sent to Claude" log, key storage, and a usage ledger from `response.Usage` with a monthly cap.
- Daily and weekly prompts rewritten around spans and goals.

**Done when:** the daily reflection reads like it came from someone who watched the day, and the sent log never shows excluded data.

### Stage 4: Mentor rituals (≈4–6 weeks) — the heart of it
- Morning check-in: yesterday in three lines, then "What matters today?" The answer becomes the day's intention.
- Evening reflection: Claude drafts from the data and the intention, then asks one or two questions. Answers are saved as `UserReflection`, and commitments are extracted.
- Sunday review: goals against time, patterns, commitments kept, and one change for next week.
- `RitualSchedulerService` (built like `CoachingLoopService`), a Journal view, and a `Commitment` entity Zoe follows up on.

**Done when:** the evening reflection happens on most workdays for a month without forcing it.

### Stage 5: Conversation and long-term memory (≈4–6 weeks)
- Chat tab: Claude with `BetaToolRunner` and tools `get_time_by_goal`, `get_spans`, `get_reflections`, `search_journal`, `get_goals`, `get_commitments`, and `propose_goal_update` (applies only after confirmation).
- Memory v2: Claude distills structured memories weekly from reflections, chat and stats. Each has a statement, evidence and a confidence; all are viewable, editable and deletable, and they decay over time. Replaces the heuristics in `MemoryService`.

**Done when:** "Why do my Tuesdays fall apart?" gets an answer that cites the data and the journal.

### Stage 6: Nudges that serve the mentor (≈2–3 weeks)
- Nudges refer to the day's intention and goals instead of a list of distracting apps.
- Toast buttons: *Back to it*, *This is work* (reclassifies), *Snooze 1h*, each recorded as a feedback event.
- Effectiveness metric: return to goal-aligned work within 10 minutes. Feeds the weekly review and the thresholds.
- Work hours, cooldown and quiet hours editable in Settings.

**Done when:** nudges are fewer, the return-to-goal rate beats the week-1 baseline, and "This is work" presses keep falling.

### Stage 7: Long horizon (months 6–12, ongoing)
- Monthly and quarterly life reviews: goal progress, life-area balance, trends.
- Calendar context so meetings explain gaps. A URL-level browser extension only if title-based classification proves too coarse.
- Data lifecycle: compact old raw events into spans, full export and import, encrypted backups.
- A replay and eval harness in `Zoe.Host` that runs a recorded week through the classifier, rules and prompts.

## Why this order

- **Truthful data before Claude:** a model on top of wrong numbers writes confident nonsense.
- **Rituals before chat:** rituals produce the journal that chat and memory depend on.
- **Nudges last:** they improve once there are daily intentions to refer to.

## Running cost

Rough estimates at Opus 5.5 rates ($4 per million input tokens, $20 per million output), to be replaced by the Stage 3 ledger:

| Job | Per use |
|---|---|
| Nudge | ≈ $0.01 |
| Daily reflection | ≈ $0.05 |
| Weekly review | ≈ $0.20 |
| Chat conversation | ≈ $0.10–0.30 |
| **Monthly total** | **≈ $5–15** |

## Existing code to build on

- `src/Zoe.Infrastructure/Services/CoachingLoopService.cs`: hosted-loop pattern for the ritual scheduler.
- `src/Zoe.Application/Services/InterventionDeliveryService.cs`: decide/deliver split for rituals and nudges.
- `src/Zoe.AI/Interfaces/ILlmClient.cs`: the seam where Claude plugs in.
- `tests/Zoe.Tests/TestDatabase.cs`: SQLite test fixture.
- `src/Zoe.Infrastructure/Services/DataExportService.cs`: extend to export new tables.
