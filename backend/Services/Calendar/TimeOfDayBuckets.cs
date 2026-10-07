namespace Callahan.Api.Services.Calendar;

// Training is planned in rough parts of the day, not clock times.
public enum TimeOfDay { Morning, Arvo, Evening }

// Maps an event's start to a training day and a part of the day, and back to the
// start time Callahan writes. The day boundary is the app's 3am training-day
// cutoff (CalendarDates.TrainingDayCutoffHour): an event starting at 01:00 belongs
// to the previous day's Evening, and Morning does not begin until 03:00.
public static class TimeOfDayBuckets
{
    public const int ArvoStartHour = 12;
    public const int EveningStartHour = 16;

    public static readonly TimeSpan DefaultDuration = TimeSpan.FromMinutes(90);

    public static (DateOnly Day, TimeOfDay Part) FromStart(DateTime localStart)
    {
        var day = CalendarDates.TrainingDay(localStart);
        var hour = localStart.Hour;
        var part = hour < CalendarDates.TrainingDayCutoffHour ? TimeOfDay.Evening
            : hour < ArvoStartHour ? TimeOfDay.Morning
            : hour < EveningStartHour ? TimeOfDay.Arvo
            : TimeOfDay.Evening;
        return (day, part);
    }

    // What Callahan writes for a slot placed in a part of the day: 07:00, 12:00, 18:00.
    public static DateTime DefaultStart(DateOnly day, TimeOfDay part) =>
        day.ToDateTime(part switch
        {
            TimeOfDay.Morning => new TimeOnly(7, 0),
            TimeOfDay.Arvo => new TimeOnly(ArvoStartHour, 0),
            _ => new TimeOnly(18, 0)
        });
}
