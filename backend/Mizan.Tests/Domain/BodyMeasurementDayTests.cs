using FluentAssertions;
using Mizan.Domain.Body;
using Mizan.Domain.Entities;
using Xunit;

namespace Mizan.Tests.BodyRules;

public class BodyMeasurementDayTests
{
    public static TheoryData<decimal?, decimal?, decimal?, decimal?, decimal?, decimal?> Writes => new()
    {
        // stored weight, stored waist, written weight, written waist, weight after, waist after
        { 82.0m, null, 81.6m, null, 81.6m, null },   // a later weight replaces the earlier one
        { 82.0m, 90m, null, 89m, 82.0m, 89m },       // a waist-only write keeps the weight
        { null, 90m, 81.6m, null, 81.6m, 90m },      // a weight joins the day's waist
        { 82.0m, 90m, null, null, 82.0m, 90m },      // a write with no values changes nothing
    };

    [Theory]
    [MemberData(nameof(Writes))]
    public void A_later_write_the_same_day_updates_only_the_values_it_carries(
        decimal? storedWeight, decimal? storedWaist, decimal? weight, decimal? waist, decimal? weightAfter, decimal? waistAfter)
    {
        var day = new BodyMeasurement { WeightKg = storedWeight, WaistCm = storedWaist };

        BodyMeasurementDay.Apply(day, new BodyMeasurementValues(WeightKg: weight, WaistCm: waist), notes: null);

        day.WeightKg.Should().Be(weightAfter);
        day.WaistCm.Should().Be(waistAfter);
    }

    [Theory]
    [InlineData("Morning", null, "Morning")]
    [InlineData("Morning", "  ", "Morning")]
    [InlineData("Morning", "After the run", "After the run")]
    [InlineData(null, "After the run", "After the run")]
    public void A_note_replaces_the_days_note_only_when_one_is_written(string? stored, string? written, string? after)
    {
        var day = new BodyMeasurement { Notes = stored };

        BodyMeasurementDay.Apply(day, new BodyMeasurementValues(), written);

        day.Notes.Should().Be(after);
    }
}
