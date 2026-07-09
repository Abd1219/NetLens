using NetLens.Domain.Entities;

namespace NetLens.Application.Abstractions;

/// <summary>
/// Abstraction for generating immutable diagnostic reports.
/// Implementations are located in the Reporting layer.
/// </summary>
public interface IReportGenerator
{
    /// <summary>
    /// Generates a PDF report for a completed diagnostic session.
    /// </summary>
    /// <param সিংession>The completed diagnostic session to report on.</param>
    /// <returns>Raw byte array of the generated PDF document.</returns>
    byte[] GeneratePdfReport(DiagnosticSession session);
}
