using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NetLens.Domain.Entities;
using NetLens.Application.Abstractions;
using System;
using System.Linq;

namespace NetLens.Reporting;

public class DiagnosticReportGenerator : IReportGenerator
{
    static DiagnosticReportGenerator()
    {
        // Set QuestPDF license (Community license is free for individuals/small organizations)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GeneratePdfReport(DiagnosticSession session)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial").FontColor(Colors.Grey.Darken3));

                // ── HEADER ─────────────────────────────────────────────────────────────
                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("NetLens").Bold().FontSize(24).FontColor("#0078D4");
                            col.Item().Text("Network Diagnostic Flight Report").FontSize(10).FontColor(Colors.Grey.Medium);
                        });

                        row.ConstantItem(100).AlignRight().AlignMiddle().Column(col =>
                        {
                            col.Item().Text($"Session: {session.SessionId.ToString()[..8]}").Bold().FontSize(10);
                            col.Item().Text(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm")).FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });

                    header.Item().PaddingVertical(0.5f, Unit.Centimetre).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                // ── CONTENT ────────────────────────────────────────────────────────────
                page.Content().PaddingVertical(0.5f, Unit.Centimetre).Column(col =>
                {
                    col.Spacing(15);

                    // Metadata Section
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                        {
                            c.Item().Text("Session Info").Bold().FontSize(12).FontColor("#0078D4");
                            c.Item().Text($"Started: {session.StartedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
                            c.Item().Text($"Ended: {(session.EndedAt.HasValue ? session.EndedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "Running")}");
                            c.Item().Text($"State: {session.State}");
                        });

                        row.ConstantItem(15);

                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                        {
                            c.Item().Text("Client Details").Bold().FontSize(12).FontColor("#0078D4");
                            c.Item().Text($"Client: {session.ClientName ?? "N/A"}");
                            c.Item().Text($"Site: {session.SiteName ?? "N/A"}");
                            c.Item().Text($"Operator: {session.OperatorName ?? "N/A"}");
                        });
                    });

                    // Latest Network State
                    var latest = session.LatestSnapshot;
                    if (latest != null)
                    {
                        col.Item().Column(latestCol =>
                        {
                            latestCol.Item().Text("Latest Connection State").Bold().FontSize(14).FontColor("#0078D4");
                            latestCol.Item().PaddingTop(5);

                            latestCol.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(3);
                                });

                                table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("SSID").Bold();
                                table.Cell().Padding(5).Text(latest.Ssid);
                                table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("BSSID").Bold();
                                table.Cell().Padding(5).Text(latest.Bssid.Value);

                                table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("RSSI").Bold();
                                table.Cell().Padding(5).Text($"{latest.Rssi} dBm");
                                table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Channel").Bold();
                                table.Cell().Padding(5).Text($"{latest.Channel} ({latest.Frequency} MHz)");

                                table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Gateway Latency").Bold();
                                table.Cell().Padding(5).Text(latest.GatewayLatency.ToString());
                                table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Packet Loss").Bold();
                                table.Cell().Padding(5).Text(latest.PacketLoss.ToString());
                            });
                        });
                    }

                    // Timeline / Rule Violations Section
                    col.Item().Column(timelineCol =>
                    {
                        timelineCol.Item().Text("Diagnostic Event Timeline").Bold().FontSize(14).FontColor("#0078D4");
                        timelineCol.Item().PaddingTop(5);

                        var criticalEvents = session.Timeline.Where(e => e.Severity != TimelineEventSeverity.Info).ToList();
                        if (criticalEvents.Count == 0)
                        {
                            timelineCol.Item().Border(1).BorderColor(Colors.Green.Lighten2).Background(Colors.Green.Lighten5).Padding(10)
                                .Text("✓ No alerts or quality degradation events detected in this session.").FontColor(Colors.Green.Darken3);
                        }
                        else
                        {
                            timelineCol.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(80); // Timestamp
                                    columns.ConstantColumn(60); // Severity
                                    columns.RelativeColumn();     // Description
                                    columns.RelativeColumn();     // Evidence
                                });

                                // Headers
                                table.Cell().Background("#0078D4").Padding(5).Text("Time").Bold().FontColor(Colors.White);
                                table.Cell().Background("#0078D4").Padding(5).Text("Severity").Bold().FontColor(Colors.White);
                                table.Cell().Background("#0078D4").Padding(5).Text("Description").Bold().FontColor(Colors.White);
                                table.Cell().Background("#0078D4").Padding(5).Text("Evidence").Bold().FontColor(Colors.White);

                                foreach (var ev in criticalEvents)
                                {
                                    var bg = ev.Severity == TimelineEventSeverity.Critical ? Colors.Red.Lighten5 : Colors.Orange.Lighten5;
                                    var textCol = ev.Severity == TimelineEventSeverity.Critical ? Colors.Red.Darken3 : Colors.Orange.Darken3;

                                    table.Cell().Background(bg).Padding(5).Text(ev.OccurredAt.ToLocalTime().ToString("HH:mm:ss")).FontColor(textCol);
                                    table.Cell().Background(bg).Padding(5).Text(ev.Severity.ToString()).Bold().FontColor(textCol);
                                    table.Cell().Background(bg).Padding(5).Text(ev.Description).FontColor(textCol);
                                    table.Cell().Background(bg).Padding(5).Text(string.Join(", ", ev.Evidence.Select(kv => $"{kv.Key}: {kv.Value}"))).FontSize(9).FontColor(textCol);
                                }
                            });
                        }
                    });
                });

                // ── FOOTER ─────────────────────────────────────────────────────────────
                page.Footer().Column(footer =>
                {
                    footer.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    footer.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text("Confidential — Generated by NetLens").FontSize(8).FontColor(Colors.Grey.Medium);
                        row.RelativeItem().AlignRight().Text(x =>
                        {
                            x.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                            x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });
                });
            });
        }).GeneratePdf();
    }
}
