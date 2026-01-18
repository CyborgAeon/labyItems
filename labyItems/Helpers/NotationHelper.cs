namespace labyItems.Helpers;

public static class NotationHelper
{
    public static string ToKNotation(this int number) => Format(number);

    public static string ToKNotation(this long number) => Format(number);

    private static string Format(long number)
    {
        if (number < 1000)
            return number.ToString();

        long rounded = number / 1000; // rounds to nearest 1000
        return $"{rounded}k";
    }
}
