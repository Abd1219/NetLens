namespace NetLens.Domain.Model;

/// <summary>
/// Represents a wireless channel number.
/// </summary>
public readonly record struct Channel
{
    public int Number { get; }

    public Channel(int number)
    {
        if (number <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "Channel number must be greater than 0.");
        }
        Number = number;
    }

    public override string ToString() => $"Ch {Number}";

    public static implicit operator int(Channel channel) => channel.Number;
    public static explicit operator Channel(int number) => new(number);
}
