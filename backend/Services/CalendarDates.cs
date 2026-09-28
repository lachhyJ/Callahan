namespace Callahan.Api.Services;

// The app's calendar rules, in one place. These were copied into five
// controllers and services; each copy was identical, which is the only reason
// they hadn't drifted yet.
public static class CalendarDates
{
    // Weeks start on Monday, matching the frontend (dateUtils.startOfWeek) and
    // the calendar grid.
    public static DateOnly MondayOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public static DateOnly MonthOf(DateOnly date) => new(date.Year, date.Month, 1);

    // The first month of a window of `months` calendar months ending with the
    // current one - so months: 3 in September starts on 1 July.
    public static DateOnly WindowStart(DateOnly today, int months) => MonthOf(today).AddMonths(-(months - 1));

    // Before 3am a session still belongs to the previous day's training - you
    // lift at midnight, and 00:55 vs 01:05 shouldn't decide the date. Mirrors
    // trainingDayIso in the frontend's dateUtils.js.
    public const int TrainingDayCutoffHour = 3;

    public static DateOnly TrainingDay(DateTime localNow)
    {
        var day = DateOnly.FromDateTime(localNow);
        return localNow.Hour < TrainingDayCutoffHour ? day.AddDays(-1) : day;
    }
}
