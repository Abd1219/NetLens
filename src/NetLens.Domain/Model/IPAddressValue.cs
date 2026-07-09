using System.Net;

namespace NetLens.Domain.Model;

/// <summary>
/// Represents a validated IPv4 or IPv6 Address.
/// </summary>
public readonly record struct IPAddressValue
{
    public string Value { get; }

    public bool IsIPv6 => Value.Contains(':');

    public IPAddressValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !IPAddress.TryParse(value, out _))
        {
            throw new ArgumentException("Invalid IP Address format.", nameof(value));
        }
        Value = value;
    }

    public override string ToString() => Value;

    public static implicit operator string(IPAddressValue ip) => ip.Value;
    public static explicit operator IPAddressValue(string value) => new(value);
}
