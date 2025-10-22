namespace labyItems.Categories;

public interface ICalculatorCategory
{
    (int TotalIsp, List<string> Lines) AddToSummary();
}