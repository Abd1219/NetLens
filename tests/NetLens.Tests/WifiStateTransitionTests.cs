using Microsoft.Extensions.Logging.Abstractions;
using NetLens.Application.Abstractions;
using NetLens.Domain.Entities;
using NetLens.Domain.Events;
using NetLens.Domain.Model;
using NetLens.Services;
using Xunit;

namespace NetLens.Tests;

public class WifiStateTransitionTests
{
    private class TestEventBus : IEventBus
    {
        public List<IDomainEvent> PublishedEvents { get; } = new();

        public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IDomainEvent
        {
            PublishedEvents.Add(@event);
            return ValueTask.CompletedTask;
        }

        public void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IDomainEvent { }
        public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IDomainEvent { }
    }

    private static WirelessSnapshot CreateSnapshot(string ssid = "OfficeWiFi", string bssid = "AA:BB:CC:DD:EE:11", string adapterName = "Intel AX201")
    {
        return new WirelessSnapshot(
            capturedAt: DateTimeOffset.UtcNow,
            rssi: new RSSI(-60),
            txRate: new PhyRate(300),
            rxRate: new PhyRate(300),
            channel: new Channel(36),
            frequency: new Frequency(5180),
            signalQuality: new SignalQuality(80),
            band: WifiBand.Band5GHz,
            ssid: ssid,
            bssid: new MacAddress(bssid),
            physicalType: "802.11ax (Wi-Fi 6)",
            securityType: WifiSecurityType.Wpa2Personal,
            connectionState: WifiConnectionState.Connected,
            gatewayLatency: new Latency(10),
            dnsLatency: new Latency(12),
            internetLatency: new Latency(25),
            packetLoss: new PacketLossRate(0),
            jitter: new Jitter(2),
            adapterName: adapterName,
            adapterManufacturer: "Unavailable",
            adapterMac: new MacAddress("11:22:33:44:55:66"),
            localIp: new IPAddressValue("192.168.1.100"),
            gatewayIp: new IPAddressValue("192.168.1.1"),
            dnsIp: new IPAddressValue("1.1.1.1"),
            ipv6: null,
            dhcpServer: "192.168.1.1",
            linkSpeedMbps: 300,
            cpuUsagePercent: 15.0,
            ramUsagePercent: 45.0);
    }

    [Fact]
    public async Task Transition_DisconnectedToConnected_EmitsOnlyWifiConnectedEvent()
    {
        var bus = new TestEventBus();
        var current = CreateSnapshot();

        await TelemetryBackgroundService.EvaluateStateTransitionsAsync(
            previous: null,
            current: current,
            eventBus: bus,
            logger: NullLogger.Instance,
            ct: CancellationToken.None);

        Assert.Single(bus.PublishedEvents);
        var evt = Assert.IsType<WifiConnectedEvent>(bus.PublishedEvents.Single());
        Assert.Equal("OfficeWiFi", evt.Ssid);
        Assert.Equal("AA:BB:CC:DD:EE:11", evt.Bssid);
    }

    [Fact]
    public async Task Transition_ConnectedToDisconnected_EmitsOnlyWifiDisconnectedEvent()
    {
        var bus = new TestEventBus();
        var previous = CreateSnapshot();

        await TelemetryBackgroundService.EvaluateStateTransitionsAsync(
            previous: previous,
            current: null,
            eventBus: bus,
            logger: NullLogger.Instance,
            ct: CancellationToken.None);

        Assert.Single(bus.PublishedEvents);
        var evt = Assert.IsType<WifiDisconnectedEvent>(bus.PublishedEvents.Single());
        Assert.Equal("Connection Lost", evt.Reason);
    }

    [Fact]
    public async Task Transition_SsidChanged_EmitsOnlySsidChangedEvent()
    {
        var bus = new TestEventBus();
        var previous = CreateSnapshot(ssid: "SSID_A");
        var current = CreateSnapshot(ssid: "SSID_B");

        await TelemetryBackgroundService.EvaluateStateTransitionsAsync(
            previous: previous,
            current: current,
            eventBus: bus,
            logger: NullLogger.Instance,
            ct: CancellationToken.None);

        Assert.Single(bus.PublishedEvents);
        var evt = Assert.IsType<SsidChangedEvent>(bus.PublishedEvents.Single());
        Assert.Equal("SSID_A", evt.OldSsid);
        Assert.Equal("SSID_B", evt.NewSsid);
    }

    [Fact]
    public async Task Transition_BssidChanged_EmitsOnlyBssidChangedEvent()
    {
        var bus = new TestEventBus();
        var previous = CreateSnapshot(bssid: "AA:BB:CC:DD:EE:11");
        var current = CreateSnapshot(bssid: "AA:BB:CC:DD:EE:22");

        await TelemetryBackgroundService.EvaluateStateTransitionsAsync(
            previous: previous,
            current: current,
            eventBus: bus,
            logger: NullLogger.Instance,
            ct: CancellationToken.None);

        Assert.Single(bus.PublishedEvents);
        var evt = Assert.IsType<BssidChangedEvent>(bus.PublishedEvents.Single());
        Assert.Equal("AA:BB:CC:DD:EE:11", evt.OldBssid);
        Assert.Equal("AA:BB:CC:DD:EE:22", evt.NewBssid);
    }

    [Fact]
    public async Task Transition_AdapterChanged_EmitsOnlyAdapterChangedEvent()
    {
        var bus = new TestEventBus();
        var previous = CreateSnapshot(adapterName: "Adapter_A");
        var current = CreateSnapshot(adapterName: "Adapter_B");

        await TelemetryBackgroundService.EvaluateStateTransitionsAsync(
            previous: previous,
            current: current,
            eventBus: bus,
            logger: NullLogger.Instance,
            ct: CancellationToken.None);

        Assert.Single(bus.PublishedEvents);
        var evt = Assert.IsType<AdapterChangedEvent>(bus.PublishedEvents.Single());
        Assert.Equal("Adapter_A", evt.OldAdapter);
        Assert.Equal("Adapter_B", evt.NewAdapter);
    }
}
