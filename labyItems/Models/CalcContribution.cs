namespace labyItems.Models;
public sealed record CalcContribution(string Id, string Source, CalcResult Result, Action? OnRemove = null);
