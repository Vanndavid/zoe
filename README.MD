# Zoe — AI Personal Mentor

Zoe is a Windows desktop AI mentor that observes your digital behavior, compares it against your goals, and intervenes at the right moment with evidence-based coaching.

Unlike a chatbot or to-do app, Zoe runs in the background, watches what you actually do, and coaches you when you drift from your goals.

---

## Table of Contents

- [Installation Guide](#installation-guide)
  - [Requirements](#requirements)
  - [Step 1: Install .NET 8 SDK](#step-1-install-net-8-sdk)
  - [Step 2: Get the code](#step-2-get-the-code)
  - [Step 3: Build the solution](#step-3-build-the-solution)
  - [Step 4: Run Zoe (development mode)](#step-4-run-zoe-development-mode)
  - [Step 5: Install as a standalone app](#step-5-install-as-a-standalone-app)
  - [Step 6: First-time setup](#step-6-first-time-setup)
  - [Where your data is stored](#where-your-data-is-stored)
  - [Troubleshooting](#troubleshooting)
- [Architecture](#architecture)
- [Solution Structure](#solution-structure)
- [Development](#development)
- [Privacy](#privacy)
- [Roadmap](#roadmap)

---

## Installation Guide

There is no `.msi` or `.exe` installer yet. You install Zoe by building it from source on Windows, or by publishing a standalone executable you can run anywhere.

### Requirements

| Requirement | Details |
|-------------|---------|
| **OS** | Windows 10 or Windows 11 (required for the UI and activity monitoring) |
| **.NET 8 SDK** | Required to build. [Download here](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **Git** | Optional, for cloning the repository |
| **Admin rights** | Not required |
| **OpenAI API key** | Not required yet (offline coaching works out of the box) |

> **Note:** The core libraries can be built and tested on Linux/macOS, but the WPF app and live window/idle monitors only work on Windows.

---

### Step 1: Install .NET 8 SDK

1. Go to https://dotnet.microsoft.com/download/dotnet/8.0
2. Download **.NET SDK 8.0** (not just the Runtime)
3. Run the installer and follow the prompts
4. Open a **new** PowerShell or Command Prompt window and verify:

```powershell
dotnet --version
```

You should see `8.0.x`. If `dotnet` is not recognized, restart your terminal or reboot.

---

### Step 2: Get the code

**Option A — Clone with Git (recommended)**

```powershell
git clone https://github.com/Vanndavid/zoe.git
cd zoe
git checkout cursor/zoe-foundation-9e5e
```

**Option B — Download ZIP**

1. Open https://github.com/Vanndavid/zoe
2. Switch to branch `cursor/zoe-foundation-9e5e`
3. Click **Code → Download ZIP**
4. Extract the ZIP and open a terminal in that folder

---

### Step 3: Build the solution

From the repository root (the folder containing `Zoe.sln`):

```powershell
dotnet restore
dotnet build
```

Optional — verify everything works:

```powershell
dotnet test
```

You should see **11 tests passed**.

---

### Step 4: Run Zoe (development mode)

**Desktop app (recommended)** — timeline, goals, settings, live monitoring:

```powershell
dotnet run --project src\Zoe.UI\Zoe.UI.csproj
```

**Console harness** — tests the full pipeline without a UI (events, context, memory, coaching, export):

```powershell
dotnet run --project src\Zoe.Host\Zoe.Host.csproj
```

---

### Step 5: Install as a standalone app

To create a folder with `Zoe.exe` you can double-click, pin to Start, or copy anywhere:

```powershell
dotnet publish src\Zoe.UI\Zoe.UI.csproj -c Release -r win-x64 --self-contained true -o .\publish\Zoe
```

This produces a self-contained app in `publish\Zoe\` — no separate .NET install needed on that machine.

**Run it:**

```powershell
.\publish\Zoe\Zoe.exe
```

**Optional — create a desktop shortcut**

1. Right-click `publish\Zoe\Zoe.exe`
2. Choose **Show more options → Create shortcut**
3. Drag the shortcut to your Desktop or pin to Start

**Optional — smaller build (requires .NET 8 Runtime on the machine)**

```powershell
dotnet publish src\Zoe.UI\Zoe.UI.csproj -c Release -r win-x64 --self-contained false -o .\publish\Zoe
```

---

### Step 6: First-time setup

When Zoe opens for the first time:

1. Go to the **Settings** tab
2. Check **Enable monitoring**
3. Optionally check **Auto-start with Windows** (registry wiring coming soon — checkbox saves the preference)
4. Paste your **Life Profile** — tell Zoe how you live and what you want. Example:

   ```
   I am a software engineer seeking promotion. I work 8 hours on software,
   30–60 min learning, 30–60 min interview prep, 30 min PTE practice,
   and enjoy gaming/movies. I want 5–10 min exercise daily.
   ```

5. Click **Save Settings**
6. Click **Start Monitoring** in the header
7. Switch between a few apps (e.g. VS Code → Chrome → back)
8. Open the **Timeline** tab — you should see `WindowChanged` and `IdleStarted`/`IdleEnded` events

**Goals tab** — goals can be added programmatically for now; a full add/edit UI is a planned follow-up.

---

### Where your data is stored

| Item | Location |
|------|----------|
| Activity database | `zoe.db` in the folder where Zoe runs (same folder as `Zoe.exe` when published) |
| Exported events | Path you choose when using export (console harness writes to temp by default) |
| Cloud sync | None — everything stays on your machine |

To back up your data, copy `zoe.db` from the app folder.

To reset all data, delete `zoe.db` and restart Zoe (it will create a fresh database).

---

### Troubleshooting

| Problem | Solution |
|---------|----------|
| `'dotnet' is not recognized` | Install .NET 8 SDK and open a **new** terminal |
| Build fails on WPF / `net8.0-windows` | You must build on Windows with the full SDK |
| Timeline is empty | Click **Start Monitoring**, then switch between apps |
| App closes immediately | Run from terminal to see errors: `dotnet run --project src\Zoe.UI\Zoe.UI.csproj` |
| `zoe.db` locked | Close all running Zoe instances |
| Coaching feels generic | Fill in your Life Profile in Settings and save |
| Want real AI responses | OpenAI integration is not wired yet — uses offline fallback for now |

---

## Architecture

```
Sensors → Event Bus → Event Store (SQLite) → Context Engine → Rule Engine → Decision Engine → AI Engine → UI
```

Built with Clean Architecture + Event-Driven design on .NET 8.

**Key principles:**
- Everything is an event; current state is derived from history
- AI only sees **context**, never raw events
- Rules fire before the LLM (faster, cheaper, predictable)
- All data stored locally by default

---

## Solution Structure

```
Zoe.sln
src/
  Zoe.Domain/           Entities, enums, value objects
  Zoe.Application/      Service interfaces and business logic
  Zoe.Infrastructure/   SQLite, EF Core, event bus, DI
  Zoe.Windows/          WindowMonitor, IdleMonitor (Windows)
  Zoe.AI/               Prompt builder, coach service, LLM client
  Zoe.UI/               WPF dashboard (Windows only)
  Zoe.Host/             Console harness for dev testing
tests/
  Zoe.Tests/            Unit and integration tests (11 tests)
```

---

## Development

**Build and test (Linux or Windows):**

```bash
dotnet restore
dotnet build
dotnet test
```

**Run console harness:**

```bash
dotnet run --project src/Zoe.Host/Zoe.Host.csproj
```

**Run WPF app (Windows only):**

```bash
dotnet run --project src/Zoe.UI/Zoe.UI.csproj
```

### Implemented phases

| Phase | Status | Components |
|-------|--------|------------|
| 1. Foundation | Done | Event bus, SQLite store, domain model, tests |
| 2. Sensors | Done | WindowMonitor, IdleMonitor, MonitoringService |
| 3. UI Shell | Done | WPF app with timeline, goals, settings |
| 4. Context | Done | ContextService, TimelineService, StatisticsService |
| 5. Rules + Decisions | Done | RuleEngine, DecisionEngine with evidence |
| 6. AI Coaching | Done | CoachService, PromptBuilder, offline LLM fallback |
| 7. Memory | Done | MemoryService with pattern extraction |
| 8. MVP Polish | Done | Weekly summaries, data export, notifications stub |

### Event schema

Every activity event uses: `EventId`, `Timestamp`, `Source`, `Type`, `Payload`, `Confidence`, `SessionId`, `CorrelationId`, `Metadata`.

Example `WindowChanged` payload:

```json
{
  "application": "Visual Studio",
  "windowTitle": "AuthenticationService.cs"
}
```

---

## Privacy

- All data is stored locally in SQLite (`zoe.db`)
- No cloud sync in the current version
- Export and delete capabilities are included in the codebase
- You own your data

---

## Roadmap

- [ ] OpenAI / local LLM integration (replace offline fallback)
- [ ] System tray icon and minimize-to-tray
- [ ] Auto-start with Windows (registry / Task Scheduler)
- [ ] Windows toast notifications for interventions
- [ ] Goal add/edit UI in the dashboard
- [ ] Proper Windows installer (`.msi` or MSIX)

---

## License

Private project — for personal use.
