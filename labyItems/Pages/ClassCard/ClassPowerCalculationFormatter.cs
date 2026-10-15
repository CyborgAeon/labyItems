using System.Globalization;
using System.Text.RegularExpressions;

namespace labyItems.Pages.ClassCard;

public static partial class ClassPowerCalculationFormatter
{
    public static string Format(string? calculation, int casterLevel = 8)
    {
        var expression = (calculation ?? string.Empty).Trim();
        if (expression.Length == 0)
            return "—";

        var withCasterLevel = Regex.Replace(expression, @"\bCL\b", casterLevel.ToString(CultureInfo.InvariantCulture), RegexOptions.IgnoreCase);
        if (expression.Contains("TM", StringComparison.OrdinalIgnoreCase))
        {
            return Regex.Replace(withCasterLevel, @"\bTM\b", "Tribe Members", RegexOptions.IgnoreCase);
        }

        if (TokenRegex().IsMatch(withCasterLevel))
            return expression;

        try
        {
            var parser = new ExpressionParser(withCasterLevel);
            var result = parser.Parse();
            return Math.Abs(result - Math.Round(result)) < 0.000001
                ? Math.Round(result).ToString(CultureInfo.InvariantCulture)
                : result.ToString("0.##", CultureInfo.InvariantCulture);
        }
        catch
        {
            return expression;
        }
    }

    [GeneratedRegex(@"[^0-9+\-*/^().\s]", RegexOptions.IgnoreCase)]
    private static partial Regex TokenRegex();

    private sealed class ExpressionParser(string text)
    {
        private int _position;
        public double Parse() { var value = Add(); Skip(); if (_position != text.Length) throw new FormatException(); return value; }
        private double Add() { var value = Multiply(); while (true) { Skip(); if (Take('+')) value += Multiply(); else if (Take('-')) value -= Multiply(); else return value; } }
        private double Multiply() { var value = Power(); while (true) { Skip(); if (Take('*')) value *= Power(); else if (Take('/')) value /= Power(); else return value; } }
        private double Power() { var value = Unary(); Skip(); return Take('^') ? Math.Pow(value, Power()) : value; }
        private double Unary() { Skip(); if (Take('+')) return Unary(); if (Take('-')) return -Unary(); if (Take('(')) { var value = Add(); Skip(); if (!Take(')')) throw new FormatException(); return value; } return Number(); }
        private double Number() { Skip(); var start = _position; while (_position < text.Length && (char.IsDigit(text[_position]) || text[_position] == '.')) _position++; if (start == _position || !double.TryParse(text[start.._position], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) throw new FormatException(); return value; }
        private bool Take(char value) { if (_position >= text.Length || text[_position] != value) return false; _position++; return true; }
        private void Skip() { while (_position < text.Length && char.IsWhiteSpace(text[_position])) _position++; }
    }
}
