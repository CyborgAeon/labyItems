using System.Text.RegularExpressions;

namespace labyItems.Helpers;

public static class RegexOptionsCompat
{
    public static RegexOptions ForRuntime(RegexOptions options)
        => OperatingSystem.IsIOS()
            ? options & ~RegexOptions.Compiled
            : options;
}
