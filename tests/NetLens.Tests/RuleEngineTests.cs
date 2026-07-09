using FluentAssertions;
using NetLens.Domain.Entities;
using NetLens.Domain.Model;
using NetLens.Domain.Rules;
using Xunit;

namespace NetLens.Tests;

public class RuleEngineTests
{
    // Helper: builds a WirelessSnapshot with defaults and lets tests override specific fields
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
        double packetLossPercent = 0,
        double jitterMs = 3)
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
            ssid: "CorpNet",
            bssid: new MacAddress("AA:BB:CC:DD:EE:FF"),
            physicalType: "802.11ax (Wi-Fi 6)",
            gatewayLatency: gatewayTimeout ? Latency.Timeout : new Latency(gatewayMs),
            dnsLatency: dnsTimeout ? Latency.Timeout : new Latency(dnsMs),
            internetLatency: new Latency(internetMs),
            packetLoss: new PacketLossRate(packetLossPercent),
            jitter: new Jitter(jitterMs),
            localIp: new IPAddressValue("192.168.1.50"),
            gatewayIp: new IPAddressValue("192.168.1.1"),
            dnsIp: new IPAddressValue("8.8.8.8"),
            adapterMac: new MacAddress("11:22:33:44:55:66"),
            cpuUsagePercent: 10,
            ramUsagePercent: 45
        );
    }

    // ──────────────────────────────────────────
    // LowRSSIRule
    // ──────────────────────────────────────────

    [Theory]
    [InlineData(-60)]  // Excellent signal
    [InlineData(-74)]  // Just above warning threshold
    public void LowRSSIRule_WithAcceptableSignal_ShouldNotFire(int rssi)
    {
        var rule = new LowRSSIRule();
        var snapshot = BuildSnapshot(rssiDbm: rssi);
        rule.Evaluate(snapshot).Should().BeNull();
    }

    [Fact]
    public void LowRSSIRule_WithWeakSignal_ShouldFireAsWarning()
    {
        var rule = new LowRSSIRule();
        var snapshot = BuildSnapshot(rssiDbm: -78);
        var result = rule.Evaluate(snapshot);

        result.Should().NotBeNull();
        result!.RuleCode.Should().Be("LOW_RSSI");
        result.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void LowRSSIRule_WithCriticalSignal_ShouldFireAsCritical()
    {
        var rule = new LowRSSIRule();
        var snapshot = BuildSnapshot(rssiDbm: -90);
        var result = rule.Evaluate(snapshot);

        result.Should().NotBeNull();
        result!.Severity.Should().Be(DiagnosticSeverity.Critical);
        result.Evidence.Should().ContainKey("RSSI");
    }

    // ──────────────────────────────────────────
    // HighPacketLossRule
    // ──────────────────────────────────────────

    [Fact]
    public void HighPacketLossRule_WithZeroLoss_ShouldNotFire()
    {
        var rule = new HighPacketLossRule();
        var snapshot = BuildSnapshot(packetLossPercent: 0);
        rule.Evaluate(snapshot).Should().BeNull();
    }

    [Fact]
    public void HighPacketLossRule_With5PercentLoss_ShouldFireAsWarning()
    {
        var rule = new HighPacketLossRule();
        var snapshot = BuildSnapshot(packetLossPercent: 5);
        var result = rule.Evaluate(snapshot);

        result.Should().NotBeNull();
        result!.RuleCode.Should().Be("HIGH_PACKET_LOSS");
        result.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void HighPacketLossRule_With15PercentLoss_ShouldFireAsCritical()
    {
        var rule = new HighPacketLossRule();
        var snapshot = BuildSnapshot(packetLossPercent: 15);
        var result = rule.Evaluate(snapshot);

        result.Should().NotBeNull();
        result!.Severity.Should().Be(DiagnosticSeverity.Critical);
    }

    // ──────────────────────────────────────────
    // GatewayLatencyRule
    // ──────────────────────────────────────────

    [Fact]
    public void GatewayLatencyRule_WithLowLatency_ShouldNotFire()
    {
        var rule = new GatewayLatencyRule();
        var snapshot = BuildSnapshot(gatewayMs: 5);
        rule.Evaluate(snapshot).Should().BeNull();
    }

    [Fact]
    public void GatewayLatencyRule_WithTimeout_ShouldFireAsCritical()
    {
        var rule = new GatewayLatencyRule();
        var snapshot = BuildSnapshot(gatewayTimeout: true);
        var result = rule.Evaluate(snapshot);

        result.Should().NotBeNull();
        result!.Severity.Should().Be(DiagnosticSeverity.Critical);
        result.Evidence["GatewayLatency"].Should().Be("Timeout");
    }

    [Fact]
    public void GatewayLatencyRule_WithHighLatency_ShouldFireAsWarning()
    {
        var rule = new GatewayLatencyRule();
        var snapshot = BuildSnapshot(gatewayMs: 60);
        var result = rule.Evaluate(snapshot);

        result.Should().NotBeNull();
        result!.RuleCode.Should().Be("HIGH_GATEWAY_LATENCY");
        result.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    // ──────────────────────────────────────────
    // DnsLatencyRule
    // ──────────────────────────────────────────

    [Fact]
    public void DnsLatencyRule_WithTimeout_ShouldFireAsCritical()
    {
        var rule = new DnsLatencyRule();
        var snapshot = BuildSnapshot(dnsTimeout: true);
        var result = rule.Evaluate(snapshot);

        result.Should().NotBeNull();
        result!.Severity.Should().Be(DiagnosticSeverity.Critical);
    }

    // ──────────────────────────────────────────
    // HighJitterRule
    // ──────────────────────────────────────────

    [Fact]
    public void HighJitterRule_WithLowJitter_ShouldNotFire()
    {
        var rule = new HighJitterRule();
        var snapshot = BuildSnapshot(jitterMs: 3);
        rule.Evaluate(snapshot).Should().BeNull();
    }

    [Fact]
    public void HighJitterRule_WithHighJitter_ShouldFireAsWarning()
    {
        var rule = new HighJitterRule();
        var snapshot = BuildSnapshot(jitterMs: 25);
        var result = rule.Evaluate(snapshot);

        result.Should().NotBeNull();
        result!.RuleCode.Should().Be("HIGH_JITTER");
    }

    // ──────────────────────────────────────────
    // DiagnosticSession lifecycle
    // ──────────────────────────────────────────

    [Fact]
    public void DiagnosticSession_ShouldStartAndRecordSnapshots()
    {
        var session = new DiagnosticSession();
        session.Start();

        session.State.Should().Be(DiagnosticSessionState.Monitoring);
        session.Timeline.Should().ContainSingle(e => e.EventCode == "SESSION_STARTED");

        var snapshot = BuildSnapshot();
        session.RecordSnapshot(snapshot);
        session.Ledger.Should().ContainSingle();
        session.LatestSnapshot.Should().Be(snapshot);
    }

    [Fact]
    public void DiagnosticSession_End_ShouldTransitionState()
    {
        var session = new DiagnosticSession();
        session.Start();
        session.End();

        session.State.Should().Be(DiagnosticSessionState.Ended);
        session.EndedAt.Should().NotBeNull();
        session.Timeline.Should().Contain(e => e.EventCode == "SESSION_ENDED");
    }
}
