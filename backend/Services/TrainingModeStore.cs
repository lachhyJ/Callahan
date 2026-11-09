using Callahan.Api.Data;
using Callahan.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Services;

// The block of the program the athlete is in right now, as one AppSettings row. Flipped
// by hand from the Dashboard: block dates are approximate and move when the club
// confirms, and "any week without club is a no-club week", so a switch the athlete
// owns is simpler than dates that would be edited anyway.
//
// Defaults to NoClub: it is the only block that needs no club, and it is where the
// program starts before the first training.
public static class TrainingModeStore
{
    public const string SettingKey = "training-mode";
    public const TemplateBlock Default = TemplateBlock.NoClub;

    public static async Task<TemplateBlock> GetAsync(AppDbContext db)
    {
        var row = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == SettingKey);
        return row is not null && TryParse(row.Value, out var mode) ? mode : Default;
    }

    public static async Task SetAsync(AppDbContext db, TemplateBlock mode)
    {
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == SettingKey);
        if (row is null) db.AppSettings.Add(new AppSetting { Key = SettingKey, Value = mode.ToString() });
        else row.Value = mode.ToString();
        await db.SaveChangesAsync();
    }

    // Only the two real modes: "Any" is a template tag, not something the athlete can be in.
    public static bool TryParse(string? value, out TemplateBlock mode)
    {
        if (Enum.TryParse(value, ignoreCase: false, out TemplateBlock parsed)
            && parsed is TemplateBlock.InSeason or TemplateBlock.NoClub)
        {
            mode = parsed;
            return true;
        }

        mode = Default;
        return false;
    }
}
