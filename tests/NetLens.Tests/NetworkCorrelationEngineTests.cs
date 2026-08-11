using FluentAssertions;
using NetLens.Application.Correlation;
using NetLens.Domain.Correlation;
using NetLens.Domain.Entities;
using NetLens.Domain.Model;
using NetLens.Domain.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NetLens.Tests;

public class NetworkCorrelationEngineTests
{
    private readonly NetworkMetricsWindowBuilder _windowBuilder = new();
    private readonly NetworkCorrelationEngine _engine = new();

    private static WirelessSnapshot BuildSnapshot(
        DateTimeOffset capturedAt,
        int rssiDbm = -60,
        double txMbps = 150,
        double rxMbps = 130,
        double gatewayMs = 5,
        bool gatewayTimeout = false,
        double dnsMs = 10,
        bool dnsTimeout = false,
        double internetMs = 15,
        bool internetTimeout = false,
        double packetLossPercent = 0,
        double jitterMs = 3)
    {
        var rssi = new RSSI(rssiDbm);
        return new WirelessSnapshot(
            capturedAt: capturedAt,
            rssi: rssi,
            txRate: new PhyRate(txMbps),
            rxRate: new PhyRate(rxMbps),
            channel: new Channel(6),
            frequency: new Frequency(2437),
            signalQuality: SignalQuality.FromRssi(rssi),
            band: WifiBand.Band2_4GHz,
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
            adapterManufacturer: "Intel",
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

    // ── TEST 1: LAN stable + WAN stable ───────────────────────────────────
    [Fact]
    public void Test1_LanStable_WanStable_ShouldNotProduceCriticalCorrelations()
    {
        var snapshots = new List<WirelessSnapshot>();
        var startTime = DateTimeOffset.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            snapshots.Add(BuildSnapshot(
                capturedAt: startTime.AddSeconds(i * 3),
                rssiDbm: -55,
                gatewayMs: 4,
                internetMs: 25,
                packetLossPercent: 0,
                jitterMs: 2
            ));
        }

        var window = _windowBuilder.Build(snapshots);
        var results = _engine.Analyze(window);

        // Weak evidence results (< 30 score) should be filtered out
        results.Should().BeEmpty();
    }

    // ── TEST 2: Low RSSI + High LAN Jitter + LAN Packet Loss ──────────────
    [Fact]
    public void Test2_LowRssi_HighLanJitter_LanPacketLoss_ShouldDetectWifiInstability()
    {
        var snapshots = new List<WirelessSnapshot>();
        var startTime = DateTimeOffset.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            snapshots.Add(BuildSnapshot(
                capturedAt: startTime.AddSeconds(i * 3),
                rssiDbm: -78,
                gatewayMs: 45, // elevated LAN latency
                internetMs: 50, // internet has normal latency (gatewayMs + 5)
                packetLossPercent: 4.5, // high aggregate packet loss
                jitterMs: 35 // high LAN jitter
            ));
        }

        var window = _windowBuilder.Build(snapshots);
        var results = _engine.Analyze(window);

        results.Should().NotBeEmpty();
        var wifiIssue = results.FirstOrDefault(r => r.CorrelationType == CorrelationType.WifiInstability);
        wifiIssue.Should().NotBeNull();
        wifiIssue!.EvidenceScore.Should().BeGreaterThanOrEqualTo(60); // Should be strong evidence
        wifiIssue.Severity.Should().Be(DiagnosticSeverity.Warning);
        wifiIssue.ContributingMetrics.Should().Contain(CorrelationEvidenceKeys.RssiAvg);
        wifiIssue.ContributingMetrics.Should().Contain(CorrelationEvidenceKeys.LanJitterAvg);
        wifiIssue.ContributingMetrics.Should().Contain(CorrelationEvidenceKeys.LanPacketLossAvg);
    }

    // ── TEST 3: LAN stable + WAN degraded ──────────────────────────────────
    [Fact]
    public void Test3_LanStable_WanDegraded_ShouldDetectExternalNetworkIssue()
    {
        var snapshots = new List<WirelessSnapshot>();
        var startTime = DateTimeOffset.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            snapshots.Add(BuildSnapshot(
                capturedAt: startTime.AddSeconds(i * 3),
                rssiDbm: -55,
                gatewayMs: 3, // very stable LAN latency
                internetMs: 250, // degraded WAN latency
                packetLossPercent: 0,
                jitterMs: 1
            ));
        }

        var window = _windowBuilder.Build(snapshots);
        var results = _engine.Analyze(window);

        results.Should().NotBeEmpty();
        var externalIssue = results.FirstOrDefault(r => r.CorrelationType == CorrelationType.ExternalNetworkIssue);
        externalIssue.Should().NotBeNull();
        externalIssue!.EvidenceScore.Should().BeGreaterThanOrEqualTo(60);
        externalIssue.Severity.Should().Be(DiagnosticSeverity.Warning);
        externalIssue.ContributingMetrics.Should().Contain(CorrelationEvidenceKeys.InternetLatencyAvg);
    }

    // ── TEST 4: RSSI progressively declining ──────────────────────────────
    [Fact]
    public void Test4_RssiDecliningProgressively_ShouldDetectSignalDegradation()
    {
        var snapshots = new List<WirelessSnapshot>();
        var startTime = DateTimeOffset.UtcNow;

        // Create a clear downward RSSI trend: -50 to -80 over 90 seconds (30 snapshots at 3s intervals)
        // Trend = -30 dBm over 1.5 minutes = -20 dBm/minute (well below the threshold of -1.0 dBm/minute)
        for (int i = 0; i < 30; i++)
        {
            int rssi = -50 - i; // starts at -50, ends at -79
            snapshots.Add(BuildSnapshot(
                capturedAt: startTime.AddSeconds(i * 3),
                rssiDbm: rssi
            ));
        }

        var window = _windowBuilder.Build(snapshots);
        var results = _engine.Analyze(window);

        results.Should().NotBeEmpty();
        var sigDegradation = results.FirstOrDefault(r => r.CorrelationType == CorrelationType.SignalDegradation);
        sigDegradation.Should().NotBeNull();
        sigDegradation!.EvidenceScore.Should().BeGreaterThanOrEqualTo(70);
        sigDegradation.ContributingMetrics.Should().Contain(CorrelationEvidenceKeys.RssiTrend);
    }

    // ── TEST 5: Single isolated spike ──────────────────────────────────────
    [Fact]
    public void Test5_SingleIsolatedSpike_ShouldNotTriggerNetworkInstability()
    {
        var snapshots = new List<WirelessSnapshot>();
        var startTime = DateTimeOffset.UtcNow;

        // 9 stable snapshots
        for (int i = 0; i < 9; i++)
        {
            snapshots.Add(BuildSnapshot(
                capturedAt: startTime.AddSeconds(i * 3),
                rssiDbm: -55,
                gatewayMs: 4,
                internetMs: 25,
                packetLossPercent: 0,
                jitterMs: 2
            ));
        }

        // 1 spiked snapshot (high latency, packet loss, jitter)
        snapshots.Add(BuildSnapshot(
            capturedAt: startTime.AddSeconds(9 * 3),
            rssiDbm: -55,
            gatewayMs: 150,
            internetMs: 400,
            packetLossPercent: 20,
            jitterMs: 60
        ));

        var window = _windowBuilder.Build(snapshots);
        var results = _engine.Analyze(window);

        // NetworkInstability should NOT fire because it is just 1 spiked sample (10% of window)
        // which is below the 30% persistence threshold.
        var instability = results.FirstOrDefault(r => r.CorrelationType == CorrelationType.NetworkInstability);
        instability.Should().BeNull();
    }

    // ── TEST 6: Simultaneous LAN & WAN degradation ────────────────────────
    [Fact]
    public void Test6_LanAndWanDegradedSimultaneously_ShouldDetectNetworkInstability()
    {
        var snapshots = new List<WirelessSnapshot>();
        var startTime = DateTimeOffset.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            snapshots.Add(BuildSnapshot(
                capturedAt: startTime.AddSeconds(i * 3),
                rssiDbm: -60,
                gatewayMs: 65, // elevated LAN latency (>30)
                internetMs: 180, // elevated WAN latency (>100)
                packetLossPercent: 5.0, // elevated aggregate packet loss (>2%)
                jitterMs: 30 // elevated jitter (>20)
            ));
        }

        var window = _windowBuilder.Build(snapshots);
        var results = _engine.Analyze(window);

        results.Should().NotBeEmpty();
        var instability = results.FirstOrDefault(r => r.CorrelationType == CorrelationType.NetworkInstability);
        instability.Should().NotBeNull();
        instability!.EvidenceScore.Should().BeGreaterThanOrEqualTo(80); // Very strong evidence
        instability.Severity.Should().Be(DiagnosticSeverity.Critical);
        instability.ContributingMetrics.Should().Contain(CorrelationEvidenceKeys.LanLatencyAvg);
        instability.ContributingMetrics.Should().Contain(CorrelationEvidenceKeys.InternetLatencyAvg);
    }

    // ── TEST 7: Insufficient data ─────────────────────────────────────────
    [Fact]
    public void Test7_InsufficientSamples_ShouldNotProduceCorrelations()
    {
        var snapshots = new List<WirelessSnapshot>();
        var startTime = DateTimeOffset.UtcNow;

        // Build with only 3 snapshots (fewer than the required minimum of 5)
        for (int i = 0; i < 3; i++)
        {
            snapshots.Add(BuildSnapshot(
                capturedAt: startTime.AddSeconds(i * 3),
                rssiDbm: -78,
                gatewayMs: 60,
                internetMs: 300,
                packetLossPercent: 12
            ));
        }

        var window = _windowBuilder.Build(snapshots);
        var results = _engine.Analyze(window);

        // Should return empty list because data is insufficient
        results.Should().BeEmpty();
    }
}
