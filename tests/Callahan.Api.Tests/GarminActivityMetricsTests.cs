using Callahan.Api.Models;
using Callahan.Api.Services;

namespace Callahan.Api.Tests;

public class GarminActivityMetricsTests
{
    // Trimmed from a real Garmin activity summary (ultimate_disc). Load and the
    // training-effect scores come through full-precision.
    private const string RealBlob = """
        {"activityId":123,"activityName":"Ultimate",
         "distance":3538.0,"duration":2743.0,"averageHR":136.0,
         "activityTrainingLoad":79.3528289794922,
         "aerobicTrainingEffect":2.29999995231628,
         "anaerobicTrainingEffect":2.20000004768372,
         "trainingEffectLabel":"SPEED"}
        """;

    [Fact]
    public void Parse_RealBlob_RoundsLoadToWhole_AndEffectsToOneDp()
    {
        var p = GarminActivityMetrics.Parse(RealBlob);

        Assert.Equal(79m, p.TrainingLoad);
        Assert.Equal(2.3m, p.AerobicTrainingEffect);
        Assert.Equal(2.2m, p.AnaerobicTrainingEffect);
        Assert.Equal("SPEED", p.TrainingEffectLabel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]        // valid JSON, not an object
    [InlineData("42")]
    public void Parse_UnusableInput_IsAllNull(string? raw)
    {
        Assert.Equal(GarminActivityMetrics.Empty, GarminActivityMetrics.Parse(raw));
    }

    [Fact]
    public void Parse_MissingFields_AreNull_NotZero()
    {
        var p = GarminActivityMetrics.Parse("""{"activityId":1,"averageHR":127.0}""");

        Assert.Null(p.TrainingLoad);
        Assert.Null(p.AerobicTrainingEffect);
        Assert.Null(p.AnaerobicTrainingEffect);
        Assert.Null(p.TrainingEffectLabel);
    }

    [Fact]
    public void Parse_NullJsonValues_AreTreatedAsAbsent()
    {
        var p = GarminActivityMetrics.Parse(
            """{"activityTrainingLoad":null,"aerobicTrainingEffect":null,"trainingEffectLabel":null}""");

        Assert.Equal(GarminActivityMetrics.Empty, p);
    }

    [Fact]
    public void Parse_EmptyLabelString_IsNull()
    {
        var p = GarminActivityMetrics.Parse("""{"activityTrainingLoad":40.0,"trainingEffectLabel":""}""");

        Assert.Equal(40m, p.TrainingLoad);
        Assert.Null(p.TrainingEffectLabel);
    }

    [Fact]
    public void Parse_ZeroValues_AreKept()
    {
        // The tiny Throws session in the real data: real 0.0 anaerobic effect,
        // "UNKNOWN" label - both are data, not absence.
        var p = GarminActivityMetrics.Parse(
            """{"activityTrainingLoad":4.85,"aerobicTrainingEffect":0.4,"anaerobicTrainingEffect":0.0,"trainingEffectLabel":"UNKNOWN"}""");

        Assert.Equal(5m, p.TrainingLoad);
        Assert.Equal(0.4m, p.AerobicTrainingEffect);
        Assert.Equal(0.0m, p.AnaerobicTrainingEffect);
        Assert.Equal("UNKNOWN", p.TrainingEffectLabel);
    }

    [Fact]
    public void Apply_WritesParsedValues_AndClearsOnEmpty()
    {
        var activity = new Activity();

        GarminActivityMetrics.Apply(activity, RealBlob);
        Assert.Equal(79m, activity.ActivityTrainingLoad);
        Assert.Equal("SPEED", activity.TrainingEffectLabel);

        // A later re-parse of a blob without the fields must null them, not
        // leave stale values behind.
        GarminActivityMetrics.Apply(activity, """{"activityId":1}""");
        Assert.Null(activity.ActivityTrainingLoad);
        Assert.Null(activity.AerobicTrainingEffect);
        Assert.Null(activity.AnaerobicTrainingEffect);
        Assert.Null(activity.TrainingEffectLabel);
    }
}
