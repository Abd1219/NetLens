using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NetLens.Application.Abstractions;
using NetLens.Application.Services;
using NetLens.Domain.Entities;
using NetLens.Domain.Events;
using NetLens.Domain.Model;
using NetLens.Domain.Rules;
using Xunit;

namespace NetLens.Tests;

public class DiagnosticEnginev03Tests
{
    private static WirelessSnapshot BuildSnapshot(
        int rssiDbm = -60,
        double txMbps = 150,
        double rxMbps = 130,
        int channel = 6,
        int freqMhz = 2437,
        double gatewayMs = 5,
        bool gatewayTimeout = false,
        double dnsMs = 10,
        bool dnsTimeout = false,
        double internetMs = 15,
        bool internetTimeout = false,
        double packetLossPercent = 0,
        double jitterMs = 3,
        WifiBand band = WifiBand.Band2_4GHz)
    {
        var rssi = new RSSI(rssiDbm);
        return new WirelessSnapshot(
            capturedAt: DateTimeOffset.UtcNow,
            rssi: rssi,
            txRate: new PhyRate(txMbps),
            rxRate: new PhyRate(rxMbps),
            channel: new Channel(channel),
            frequency: new Frequency(freqMhz),
            signalQuality: SignalQuality.FromRssi(rssi),
            band: band,
            ssid: "CorpNet",
            bssid: new MacAddress("AA:BB:CC:DD:EE:FF"),
            physicalType: "802.11ax (Wi-Fi 6)",
            securityType: WifiSecurityType.Wpa2Personal,
            connectionState: WifiConnectionState.Connected,
            gatewayLatency: gatewayTimeout ? Latency.Timeout : new Latency(gatewayMs),
            dnsLatency: dnsTimeout ? Latency.Timeout : new Latency(dnsMs),
            internetLatency: internetTimeout ? Latency.Timeout : new Latency(internetMs),
            packetLoss: new PacketLossRate(packetLossPercent),
            jitter: new Jitter(jitterMs),
            adapterName: "Intel AX201",
            adapterManufacturer: "Unavailable",
            adapterMac: new MacAddress("11:22:33:44:55:66"),
            localIp: new IPAddressValue("192.168.1.50"),
            gatewayIp: new IPAddressValue("192.168.1.1"),
            dnsIp: new IPAddressValue("8.8.8.8"),
            ipv6: null,
            dhcpServer: "192.168.1.1",
            linkSpeedMbps: txMbps,
            cpuUsagePercent: 10,
            ramUsagePercent: 45
        );
    }

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

    // ──────────────────────────────────────────
    // DiagnosticConfidence Tests
    // ──────────────────────────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Confidence_Constructor_InvalidValue_ShouldThrow(int invalidValue)
    {
        Action act = () => _ = new DiagnosticConfidence(invalidValue);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(10, DiagnosticConfidenceLevel.Insufficient)]
    [InlineData(35, DiagnosticConfidenceLevel.Low)]
    [InlineData(65, DiagnosticConfidenceLevel.Medium)]
    [InlineData(85, DiagnosticConfidenceLevel.High)]
    [InlineData(97, DiagnosticConfidenceLevel.VeryHigh)]
    public void Confidence_Levels_ShouldMapCorrectly(int value, DiagnosticConfidenceLevel expectedLevel)
    {
        var confidence = new DiagnosticConfidence(value);
        confidence.Level.Should().Be(expectedLevel);
    }

    // ──────────────────────────────────────────
    // LowPhyRateRule Context-Aware Tests
    // ──────────────────────────────────────────

    [Fact]
    public void LowPhyRateRule_GoodRateOn24GHz_ShouldNotFire()
    {
        var rule = new LowPhyRateRule();
        var snapshot = BuildSnapshot(txMbps: 72, rxMbps: 72, band: WifiBand.Band2_4GHz, rssiDbm: -60);
        rule.Evaluate(snapshot).Should().BeNull();
    }

    [Fact]
    public void LowPhyRateRule_LowRateExplainedByLowRSSI_ShouldFireAsInfoWithLowConfidence()
    {
        var rule = new LowPhyRateRule();
        // 5 GHz with 20 Mbps rate but RSSI is -82 (weak signal explains low PHY rate)
        var snapshot = BuildSnapshot(txMbps: 20, rxMbps: 20, band: WifiBand.Band5GHz, rssiDbm: -82);
        var result = rule.Evaluate(snapshot);

        result.Should().NotBeNull();
        result!.Severity.Should().Be(DiagnosticSeverity.Info);
        result.Confidence.Level.Should().Be(DiagnosticConfidenceLevel.Low);
        result.Title.Should().Contain("Explained by Low RSSI");
    }

    [Fact]
    public void LowPhyRateRule_LowRateWithGoodRSSI_ShouldFireAsCriticalOrWarning()
    {
        var rule = new LowPhyRateRule();
        // 5 GHz with 30 Mbps (below 54 Mbps critical threshold) despite -55 dBm RSSI
        var snapshot = BuildSnapshot(txMbps: 30, rxMbps: 30, band: WifiBand.Band5GHz, rssiDbm: -55);
        var result = rule.Evaluate(snapshot);

        result.Should().NotBeNull();
        result!.Severity.Should().Be(DiagnosticSeverity.Critical);
        result.Confidence.Level.Should().Be(DiagnosticConfidenceLevel.High);
    }

    // ──────────────────────────────────────────
    // InternetLatencyRule Tests
    // ──────────────────────────────────────────

    [Fact]
    public void InternetLatencyRule_GoodLatency_ShouldNotFire()
    {
        var rule = new InternetLatencyRule();
        var snapshot = BuildSnapshot(internetMs: 25);
        rule.Evaluate(snapshot).Should().BeNull();
    }

    [Fact]
    public void InternetLatencyRule_HighInternetLatency_HealthyGateway_ShouldFireHighConfidence()
    {
        var rule = new InternetLatencyRule();
        var snapshot = BuildSnapshot(internetMs: 250, gatewayMs: 5);
        var result = rule.Evaluate(snapshot);

        result.Should().NotBeNull();
        result!.RuleCode.Should().Be("HIGH_INTERNET_LATENCY");
        result.Severity.Should().Be(DiagnosticSeverity.Critical);
        result.Confidence.Level.Should().Be(DiagnosticConfidenceLevel.High);
    }

    // ──────────────────────────────────────────
    // Correlation Rules Tests
    // ──────────────────────────────────────────

    [Fact]
    public void SignalDegradationRule_WithLowRSSI_LowPhyRate_PacketLoss_ShouldFireVeryHighConfidence()
    {
        var rule = new SignalDegradationRule();
        var snapshot = BuildSnapshot(rssiDbm: -88, txMbps: 15, rxMbps: 15, packetLossPercent: 12, band: WifiBand.Band5GHz);

        var atomicResults = new List<DiagnosticResult>
        {
            new LowRSSIRule().Evaluate(snapshot)!,
            new LowPhyRateRule().Evaluate(snapshot)!,
            new HighPacketLossRule().Evaluate(snapshot)!
        }.Where(r => r != null).ToList();

        var result = rule.Evaluate(snapshot, atomicResults);

        result.Should().NotBeNull();
        result!.RuleCode.Should().Be("SIGNAL_DEGRADATION");
        result.Category.Should().Be(DiagnosticCategory.Correlation);
        result.Confidence.Level.Should().Be(DiagnosticConfidenceLevel.VeryHigh);
    }

    [Fact]
    public void ConnectivityFullLossRule_WhenGatewayAndDnsTimeout_ShouldFireCertain()
    {
        var rule = new ConnectivityFullLossRule();
        var snapshot = BuildSnapshot(gatewayTimeout: true, dnsTimeout: true);

        var result = rule.Evaluate(snapshot, new List<DiagnosticResult>());

        result.Should().NotBeNull();
        result!.RuleCode.Should().Be("CONNECTIVITY_FULL_LOSS");
        result.Severity.Should().Be(DiagnosticSeverity.Critical);
        result.Confidence.Value.Should().Be(100);
    }

    [Fact]
    public void ConnectivityPartialRule_WhenGatewayResponsive_AndDnsTimeouts_ShouldFire()
    {
        var rule = new ConnectivityPartialRule();
        var snapshot = BuildSnapshot(gatewayMs: 5, dnsTimeout: true);

        var result = rule.Evaluate(snapshot, new List<DiagnosticResult>());

        result.Should().NotBeNull();
        result!.RuleCode.Should().Be("CONNECTIVITY_PARTIAL");
        result.Severity.Should().Be(DiagnosticSeverity.Critical);
    }

    // ──────────────────────────────────────────
    // DiagnosticService Conflict Suppression Tests
    // ──────────────────────────────────────────

    [Fact]
    public void DiagnosticService_FullLoss_ShouldSuppressAtomicGatewayAndDnsAlerts()
    {
        var atomicRules = new IDiagnosticRule[]
        {
            new GatewayLatencyRule(),
            new DnsLatencyRule(),
            new HighPacketLossRule()
        };

        var correlationRules = new ICorrelationRule[]
        {
            new ConnectivityFullLossRule()
        };

        var ruleEngine = new RuleEngine(atomicRules);
        var eventBus = new TestEventBus();
        var service = new DiagnosticService(ruleEngine, correlationRules, eventBus, NullLogger<DiagnosticService>.Instance);

        var snapshot = BuildSnapshot(gatewayTimeout: true, dnsTimeout: true);
        var results = service.AnalyzeSnapshot(snapshot);

        // CONNECTIVITY_FULL_LOSS should be present, atomic HIGH_GATEWAY_LATENCY & DNS_SLOW should be suppressed
        results.Should().ContainSingle(r => r.RuleCode == "CONNECTIVITY_FULL_LOSS");
        results.Should().NotContain(r => r.RuleCode == "HIGH_GATEWAY_LATENCY");
        results.Should().NotContain(r => r.RuleCode == "DNS_SLOW");
    }

    [Fact]
    public void DiagnosticService_SignalDegradation_ShouldSuppressAtomicLowRssiAndLowPhyRate()
    {
        var atomicRules = new IDiagnosticRule[]
        {
            new LowRSSIRule(),
            new LowPhyRateRule(),
            new HighPacketLossRule()
        };

        var correlationRules = new ICorrelationRule[]
        {
            new SignalDegradationRule()
        };

        var ruleEngine = new RuleEngine(atomicRules);
        var eventBus = new TestEventBus();
        var service = new DiagnosticService(ruleEngine, correlationRules, eventBus, NullLogger<DiagnosticService>.Instance);

        var snapshot = BuildSnapshot(rssiDbm: -88, txMbps: 15, rxMbps: 15, packetLossPercent: 12, band: WifiBand.Band5GHz);
        var results = service.AnalyzeSnapshot(snapshot);

        results.Should().ContainSingle(r => r.RuleCode == "SIGNAL_DEGRADATION");
        results.Should().NotContain(r => r.RuleCode == "LOW_RSSI");
        results.Should().NotContain(r => r.RuleCode == "LOW_PHY_RATE");
    }
}
