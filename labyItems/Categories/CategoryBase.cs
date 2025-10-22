using System.ComponentModel;
using System.Runtime.CompilerServices;
using labyItems.Models;
namespace labyItems.Categories;

public abstract class CategoryBase : ICalculatorCategory
{

    protected static int SumRows(IEnumerable<CalcRow> rows)
        => rows.Sum(r => r.Count * r.Cost);

    protected static List<string> LinesFrom(IEnumerable<CalcRow> rows)
        => rows.Where(r => r.Count > 0)
               .Select(r => $"• {r.Name}{(r.AllowMultiple ? $" x{r.Count}" : "")} ({r.Cost * r.Count} ISP)")
               .ToList();

    public abstract (int TotalIsp, List<string> Lines) AddToSummary();
}
