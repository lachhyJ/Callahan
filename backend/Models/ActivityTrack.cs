namespace Callahan.Api.Models;

// The per-second GPS/speed stream for one activity, 1:1 with Activity. Only
// pulled for Ultimate activities (see garmin_sync.py). Kept in its own entity,
// never a column on Activity, so a ~100 KB blob is never loaded incidentally -
// only an explicit .Include(a => a.Track) touches it.
//
// FieldGeometry reads this to decide on/off-field; raw lat/lon is stored (not
// projected coordinates) so POST /api/activities/laps/reclassify can re-derive
// the per-game field frame with no Garmin traffic.
public class ActivityTrack
{
    public int Id { get; set; }
    public int ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    // Absolute epoch ms of the first sample. The stream's per-sample clock and
    // ActivityLap.StartTimeGmt have different origins, so both sides store
    // absolute time and C# just subtracts - no shared-origin convention to get
    // wrong.
    public long StartEpochMs { get; set; }

    // Denormalised so ActivityDto can report "has a track, how big" without
    // loading SamplesJson.
    public int SampleCount { get; set; }
    public decimal? MedianSpacingSec { get; set; }

    // {"t":[int seconds from StartEpochMs],"lat":[6dp],"lon":[6dp],"spd":[2dp m/s]}
    // - the same shape the sync PUTs and the fixtures carry.
    public string SamplesJson { get; set; } = "";
}
