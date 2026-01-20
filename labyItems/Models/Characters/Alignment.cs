
public enum MoralAxis { Good, Neutral, Evil }
public enum OrderAxis { Lawful, Neutral, Chaotic }

public readonly record struct Alignment(OrderAxis Order, MoralAxis Moral)
{
    public override string ToString()
    {
        if (Order == OrderAxis.Neutral && Moral == MoralAxis.Neutral) return "True Neutral";
        return $"{Order} {Moral}";
    }
}

public sealed class AlignmentRestriction
{
    public HashSet<MoralAxis> RemoveMoral { get; } = new();
    public HashSet<OrderAxis> RemoveOrder { get; } = new();
}

public sealed class AlignmentRule
{
    // "restrict" or "set"
    public string Mode { get; set; } = "restrict";

    public AllowedAxes? Allowed { get; set; }
    public List<string>? AllowedPairs { get; set; }
}

public sealed class AllowedAxes
{
    public List<MoralAxis>? Moral { get; set; }
    public List<OrderAxis>? Order { get; set; }
}
