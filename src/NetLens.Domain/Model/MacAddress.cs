using System.Text.RegularExpressions;

namespace NetLens.Domain.Model;

/// <summary>
/// Represents a hardware MAC Address, validated and formatted.
/// </summary>
public readonly record struct MacAddress
{
    private static readonly Regex MacRegex = new(
        @"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$|^([0-9A-Fa-f]{12})$",
        RegexOptions.Compiled);

    public string Value { get; }

    public MacAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !MacRegex.IsMatch(value))
        {
            throw new ArgumentException("Invalid MAC Address format.", nameof(value));
        }

        // Normalize to upper case with colon separators
        var clean = value.Replace("-", "").Replace(":", "").ToUpperInvariant();
        var formatted = string.Join(":", Enumerable.Range(0, 6).Select(i => clean.Substring(i * 2, 2)));
        Value = formatted;
    }

    public override string ToString() => Value;
}
