namespace Callahan.Api.Models;

// How a tournament's taper runs, from program v4's "Tapers" table. A tournament
// with no taper has no profile at all (TaperDays and TaperProfile are null
// together): v4's smaller tournaments get no taper, just the gym session before
// them cut, and that needs nothing from the app.
public enum TaperProfile
{
    // 4-5 days, volume down ~30%, loads and jumps kept. A qualifier, not the peak.
    Regionals = 1,
    // The final 2 weeks: volume down ~30% by cutting accessories, then a final
    // week of two half-volume gym sessions and one short field session.
    Nationals = 2,
}
