namespace Callahan.Api.Models;

// Server-owned single-value settings (currently just "timezone"). Distinct from
// PlateCalcSetting, which is an opaque frontend blob store with a key whitelist.
public class AppSetting
{
    public required string Key { get; set; }
    public required string Value { get; set; }
}
