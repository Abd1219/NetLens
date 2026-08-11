using NetLens.Network.Wifi;
using Xunit;

namespace NetLens.Tests;

public class WifiChannelTests
{
    [Theory]
    [InlineData(2412, 1)]
    [InlineData(2437, 6)]
    [InlineData(2462, 11)]
    [InlineData(2472, 13)]
    [InlineData(2484, 14)]
    public void CalculateChannelFromFrequency_2_4GHz_ReturnsCorrectChannel(int freqMhz, int expectedChannel)
    {
        var channel = WlanApi.CalculateChannelFromFrequencyMhz(freqMhz);
        Assert.Equal(expectedChannel, channel);
    }

    [Theory]
    [InlineData(5180, 36)]
    [InlineData(5200, 40)]
    [InlineData(5500, 100)]
    [InlineData(5745, 149)]
    [InlineData(5825, 165)]
    public void CalculateChannelFromFrequency_5GHz_ReturnsCorrectChannel(int freqMhz, int expectedChannel)
    {
        var channel = WlanApi.CalculateChannelFromFrequencyMhz(freqMhz);
        Assert.Equal(expectedChannel, channel);
    }

    [Theory]
    [InlineData(5955, 1)]
    [InlineData(6115, 33)]
    public void CalculateChannelFromFrequency_6GHz_ReturnsCorrectChannel(int freqMhz, int expectedChannel)
    {
        var channel = WlanApi.CalculateChannelFromFrequencyMhz(freqMhz);
        Assert.Equal(expectedChannel, channel);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1000, 0)]
    public void CalculateChannelFromFrequency_InvalidFreq_ReturnsZero(int freqMhz, int expectedChannel)
    {
        var channel = WlanApi.CalculateChannelFromFrequencyMhz(freqMhz);
        Assert.Equal(expectedChannel, channel);
    }
}
