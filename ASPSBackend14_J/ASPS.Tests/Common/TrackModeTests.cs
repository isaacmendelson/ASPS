using Common.Enums;
using FluentAssertions;
using Xunit;

namespace ASPS.Tests.Common;

/// <summary>
/// Unit tests for TrackMode enum
/// </summary>
public class TrackModeTests
{
    [Fact]
    public void TrackMode_None_ShouldBeZero()
    {
        ((int)TrackMode.None).Should().Be(0);
    }

    [Fact]
    public void TrackMode_Surf_ShouldBeOne()
    {
        ((int)TrackMode.Surf).Should().Be(1);
    }

    [Fact]
    public void TrackMode_Click_ShouldBeTwo()
    {
        ((int)TrackMode.Click).Should().Be(2);
    }

    [Theory]
    [InlineData(0, TrackMode.None)]
    [InlineData(1, TrackMode.Surf)]
    [InlineData(2, TrackMode.Click)]
    public void TrackMode_CastFromInt_ShouldReturnCorrectValue(int value, TrackMode expected)
    {
        var result = (TrackMode)value;
        result.Should().Be(expected);
    }

    [Fact]
    public void TrackMode_AllValues_ShouldBeUnique()
    {
        var values = Enum.GetValues<TrackMode>();
        var intValues = values.Select(v => (int)v).ToList();
        
        intValues.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void SetTrackMode_ProtectiveActionType_ShouldExist()
    {
        // Verify SetTrackMode was added to ProtectiveActionType
        var setTrackMode = ProtectiveActionType.SetTrackMode;
        ((int)setTrackMode).Should().Be(9);
    }
}
