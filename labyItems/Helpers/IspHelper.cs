namespace labyItems.Helpers;

public static class IspHelper
{
    public static int AddIf(this bool condition, int total, int additive) =>
        condition ? total + additive : total;

    public static List<string> AddToSummaryIf(this List<string> s, bool condition, string label)
    {
        if (condition) { s.Add(label); }
        return s;
    }

    public static List<string> AddToSummaryIf(this List<string> s, int condition, string label)
    {
        if (condition <= 0) return s;
        s.Add(label);
        return s;
    }
}