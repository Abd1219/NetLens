namespace NetLens.Domain.Rules;

/// <summary>
/// Value Object representing the confidence level of a diagnostic result, from 0 to 100.
///
/// Confidence is calculated deterministically based on the quantity and coherence
/// of available evidence. No machine learning is used.
///
/// Heuristics:
///   &lt; 20  — Insufficient: a single marginal metric near a threshold.
///   20–49 — Low: limited or ambiguous evidence.
///   50–79 — Medium: clear single-metric violation with supporting context.
///   80–94 — High: multiple correlated metrics pointing to the same root cause.
///   95–100 — Very High: direct, unambiguous evidence (e.g., Timeout, multiple confirming metrics).
/// </summary>
public readonly record struct DiagnosticConfidence
{
    public int Value { get; }

    public DiagnosticConfidenceLevel Level => Value switch
    {
        >= 95 => DiagnosticConfidenceLevel.VeryHigh,
        >= 80 => DiagnosticConfidenceLevel.High,
        >= 50 => DiagnosticConfidenceLevel.Medium,
        >= 20 => DiagnosticConfidenceLevel.Low,
        _     => DiagnosticConfidenceLevel.Insufficient
    };

    public DiagnosticConfidence(int value)
    {
        if (value < 0 || value > 100)
            throw new ArgumentOutOfRangeException(nameof(value), "Confidence must be between 0 and 100.");
        Value = value;
    }

    public override string ToString() => $"{Value}%";

    // Common pre-defined confidence levels for use in rule implementations.
    public static DiagnosticConfidence Insufficient => new(10);
    public static DiagnosticConfidence Low          => new(35);
    public static DiagnosticConfidence Medium       => new(65);
    public static DiagnosticConfidence High         => new(85);
    public static DiagnosticConfidence VeryHigh     => new(97);
    public static DiagnosticConfidence Certain      => new(100);
}

public enum DiagnosticConfidenceLevel
{
    Insufficient, // < 20: evidencia marginal o insuficiente
    Low,          // 20–49: evidencia limitada
    Medium,       // 50–79: evidencia moderada con contexto claro
    High,         // 80–94: múltiples métricas correlacionadas
    VeryHigh      // 95–100: evidencia directa e inequívoca
}
