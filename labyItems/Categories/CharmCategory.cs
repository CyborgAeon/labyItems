using labyItems.Models;
using labyItems.Pages.Configs;
namespace labyItems.Categories;

public sealed class CharmCategory : ICalculatorCategory
{
    // After picking evocations & configuring, add them here:
    public List<EvocationConfig> Configured { get; } = new();

    public (int TotalIsp, List<string> Lines) AddToSummary()
    {
        var total = Configured.Sum(c => c.Total);

        var lines = Configured
            .Select(c =>
                $"• {c.EvocationName}: " +
                $"Basic x{c.BasicPerDay}, Advanced x{c.AdvancedPerDay}" +
                $"{(c.AddBasic ? ", +Basic" : "")}" +
                $"{(c.AddAdvanced ? ", +Advanced" : "")}" +
                $"{(c.AddPrep ? ", +30s prep" : "")} ({c.Total} ISP)")
            .ToList();

        return (total, lines);
    }
}
