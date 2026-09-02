namespace Callahan.Api.Models;

// One record per navigation or tracked interaction, so questions about how the
// app is actually used ("is /history ever reached, and how", "are Streaks and
// Trends opened in the same sitting") can be answered from data rather than
// recollection. Single-user, self-hosted, never leaves the NAS.
public class UsageEvent
{
    public int Id { get; set; }

    public DateTime OccurredAt { get; set; }

    // "route" for a navigation, "action" for a tracked interaction.
    public string Kind { get; set; } = "route";

    // Route pattern, not the literal URL: /exercises/:id rather than
    // /exercises/17, so visits aggregate instead of splitting per record.
    public string Path { get; set; } = "";

    // The route navigated away from — the interesting half for navigation
    // questions, since it says how a screen is actually reached. Null on the
    // first event after a cold open.
    public string? FromPath { get; set; }

    // Time spent on FromPath before this event. Null when there's no previous
    // route, or when the tab was hidden for the interval.
    public int? DwellMs { get; set; }

    // For Kind == "action": what was tapped (e.g. "quick-link", "calendar-gutter")
    // and which one (e.g. "/trends", "2026-08-31").
    public string? Action { get; set; }
    public string? Detail { get; set; }

    // How long the backend process had been up when this landed. A deploy
    // restarts the container, so a small value means the event is probably
    // post-deploy verification rather than real use. Recorded rather than
    // filtered on: the cutoff is an analysis-time decision and "10 minutes" is
    // a guess worth being able to revise without having thrown data away.
    public int BackendUptimeSeconds { get; set; }
}
