namespace labyItems.Helpers;

public static class FontAwesomeGlyphs
{
    public const string SolidFamily = "FASolid";
    public const string Back = "\uf060";
    public const string Add = "\uf067";
    public const string Remove = "\uf00d";
    public const string Close = "\u2715";
    public const string Menu = "\u2630";
    public const string Previous = "\u2039";
    public const string Chevron = "\uf054";
    public const string ChevronRight = Chevron;
    public const string ChevronUp = Chevron;
    public const string ChevronDown = Chevron;
    public const string InfoCircle = "\uf05a";
    public const string Search = "\uf002";
    public const string Filter = "\uf0b0";
    public const string Check = "\uf00c";
    public const string CheckCircle = "\uf058";
    public const string Circle = "\uf111";
    public const string RadioSelected = "\uf192";
    public const string CheckboxChecked = "\uf14a";
    public const string CheckboxUnchecked = "\uf0c8";
    public const string MoreVertical = "\uf142";
    public const string Star = "\uf005";
    public const string Lightning = "\uf0e7";
    public const string Upload = "\uf093";
    public const string ExternalLink = "\uf08e";
    // public const string Book = "\uf5da";
    public const string Home = "\uf015";
    public const string User = "\uf007";
    public const string CharacterClass = "\uf19d";
    public const string Race = "\uf0c0";
    public const string Paw = "\uf1b0";
    public const string Globe = "\uf0ac";
    public const string Tree = "\uf1bb";
    public const string Sitemap = "\uf0e8";
    public const string Guild = "\ue56e";
    public const string GraduationCap = "\uf19d";
    public const string Neutral = "\uf24e";
    public const string ScaleBalanced = Neutral;
    public const string ScaleUnbalanced = "\uf515";
    public const string ScaleUnbalancedFlip = "\uf516";
    public const string Evil = ScaleUnbalanced;
    public const string Goodly = ScaleUnbalancedFlip;
    public const string Settings = "\uf013";
    public const string Calendar = "\uf073";
    public const string Calculator = "\uf1ec";
    public const string EditSquare = "\uf044";
    public const string Pen = "\uf1fc";
    public const string Trash = "\uf1f8";
    public const string Delete = Trash;
    public const string Question = "\uf129";
    public const string Lock = "\uf023";
    public const string Minus = "\uf068";
    public const string Tag = "\uf02b";
    public const string Tasks = "\uf0ae";
    public const string Shield = "\uf3ed";
    public const string Infinity = "\uf534";
    public const string Gesture = "\ue05e";
    public const string LocationArrow = "\uf124";
    public const string Clock = "\uf017";
    // public const string Exclamation = "\uf12a";
    public const string Shamanic = "\uf06c";
    public const string MultiRace = "\ue068";
    public const string MultiClass = "\uf501";
    public const string Mobile = "\uf362";
    public const string Palette = "\uf53f";
    public const string Wand = "\uf72b";
    public const string Items = "\uf468";
    public const string MonsterPointItem = "\uf6d5";
    public const string Scroll = "\uf70e";
    public const string Wallet = "\uf555";
    public const string Miracle = "\uf5da";
    public const string CustomClass = "\uf6e4";
    public const string Spell = "\uf5da";
    public const string Ability = "\uf559";
    public const string Ghost = "\uf54c";
    public const string IspItem = "\uf187";
    public const string AssignedMonsterPointItem = "\uf279";
    public const string AbilitySearchResult = "\uf014";
    public const string SpellSearchResult = "\uf01d";
    public const string Progression = "\uf201";
    public const string Specialisation = "\uf518";
    public const string Review = "\uf15c";
    public const string Edit = "\uf1fc";
    public const string Advance = "\uf6de";
    public const string Battleboard = "\u2694";
    public const string ExportBattleboard = "\uf56e";
    public const string Crafting = "\uf7d9";
    public const string Dominion = "\uf0e3";
    public const string Sun = "\uf185";
    public const string Moon = "\uf186";
    public const string Force = "\uf0e7";
    public const string Causing = "\uf7a9";
    public const string Dismissal = "\ue05d";
    public const string Heart = "\uf004";
    public const string Warding = "\ue59a";
    public const string Benediction = "\ue574";
    public const string Control = "\ue4f8";
    public const string Communication = "\uf086";
    public const string Wayfinder = "\uf14e";
    public static string GetSphereIcon(string? sphere)
    {
        var token = NormalizeSphereToken(sphere);
        return token switch
        {
            "dismissal" => Dismissal,
            "healing" => Heart,
            "universal" => Globe,
            "warding" => Warding,
            "causing" => Causing,
            "dominion" => Dominion,
            "balance" => Neutral,
            "benediction" => Benediction,
            "communication" => Communication,
            "control" => Control,
            "force" => Force,
            "necromancy" => Ghost,
            "shamanic" => Shamanic,
            "wayfinder" => Wayfinder,
            "goodlywitchcraft" => Sun,
            "evilwitchcraft" => Moon,
            _ => "\uf7cc",
        };
    }

    private static string NormalizeSphereToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var chars = raw
            .Trim()
            .Where(char.IsLetterOrDigit)
            .ToArray();

        return new string(chars).ToLowerInvariant();
    }
}
