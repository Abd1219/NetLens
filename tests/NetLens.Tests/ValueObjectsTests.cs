using FluentAssertions;
using NetLens.Domain.Model;
using Xunit;

namespace NetLens.Tests;

public class ValueObjectsTests
{
    [Theory]
    [InlineData(-100)]
    [InlineData(-50)]
    [InlineData(0)]
    public void RSSI_WithValidRange_ShouldInitializeCorrectly(int val)
    {
        var rssi = new RSSI(val);
        rssi.Value.Should().Be(val);
    }

    [Theory]
    [InlineData(-101)]
    [InlineData(1)]
    public void RSSI_WithInvalidRange_ShouldThrowException(int val)
    {
        var act = () => new RSSI(val);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PhyRate_WithNegativeValue_ShouldThrowException()
    {
        var act = () => new PhyRate(-1.5);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Channel_WithInvalidValue_ShouldThrowException(int val)
    {
        var act = () => new Channel(val);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("00:1A:2B:3C:4D:5E", "00:1A:2B:3C:4D:5E")]
    [InlineData("00-1a-2b-3c-4d-5e", "00:1A:2B:3C:4D:5E")]
    [InlineData("001a2b3c4d5e", "00:1A:2B:3C:4D:5E")]
    public void MacAddress_WithValidFormats_ShouldNormalizeAndInitialize(string input, string expected)
    {
        var mac = new MacAddress(input);
        mac.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("00:1A:2B:3C:4D")]
    [InlineData("invalid-mac")]
    [InlineData("")]
    public void MacAddress_WithInvalidFormat_ShouldThrowException(string input)
    {
        var act = () => new MacAddress(input);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("192.168.1.1")]
    [InlineData("2001:db8::1")]
    public void IPAddressValue_WithValidFormat_ShouldInitialize(string ip)
    {
        var ipVal = new IPAddressValue(ip);
        ipVal.Value.Should().Be(ip);
    }

    [Theory]
    [InlineData("256.256.256.256")]
    [InlineData("invalid-ip")]
    public void IPAddressValue_WithInvalidFormat_ShouldThrowException(string ip)
    {
        var act = () => new IPAddressValue(ip);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(100, HealthScoreCategory.Excellent)]
    [InlineData(90, HealthScoreCategory.Excellent)]
    [InlineData(89, HealthScoreCategory.Good)]
    [InlineData(70, HealthScoreCategory.Good)]
    [InlineData(69, HealthScoreCategory.Fair)]
    [InlineData(50, HealthScoreCategory.Fair)]
    [InlineData(49, HealthScoreCategory.Poor)]
    [InlineData(30, HealthScoreCategory.Poor)]
    [InlineData(29, HealthScoreCategory.Critical)]
    [InlineData(0, HealthScoreCategory.Critical)]
    public void HealthScoreValue_ShouldCategorizeCorrectly(int score, HealthScoreCategory expectedCategory)
    {
        var health = new HealthScoreValue(score);
        health.Value.Should().Be(score);
        health.Category.Should().Be(expectedCategory);
    }
}
