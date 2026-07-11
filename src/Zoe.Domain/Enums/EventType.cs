namespace Zoe.Domain.Enums;

public enum EventType
{
    WindowChanged,
    IdleStarted,
    IdleEnded,
    GoalUpdated,
    GoalCompleted,
    FocusSessionStarted,
    FocusSessionEnded,
    InterventionTriggered,
    UserReflection,
    ScreenshotCaptured,
    ClipboardChanged,
    BrowserNavigation,
    ApplicationStarted,
    ApplicationClosed
}
