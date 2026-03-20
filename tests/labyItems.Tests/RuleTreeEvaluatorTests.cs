using labyItems.Models.Rules;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class RuleTreeEvaluatorTests
{
    [Fact]
    public void Evaluate_AllModeWithUnionField_PassesWhenAnyMappedFieldMatches()
    {
        var rules = new List<RuleClause>
        {
            new()
            {
                Field = "Race|Class",
                Operator = RuleComparisonOp.In,
                Value = new List<string> { "Elf", "Faerie", "Wizard", "Warlock" }
            }
        };

        var result = RuleTreeEvaluator.Evaluate(rules, Resolve, NormalizeToken);
        Assert.True(result);

        static IEnumerable<string> Resolve(string field)
            => field.Equals("Class", StringComparison.OrdinalIgnoreCase)
                ? new[] { "Wizard" }
                : Array.Empty<string>();
    }

    [Fact]
    public void Evaluate_AllMode_FailsWhenAnyRuleMissing()
    {
        var rules = new List<RuleClause>
        {
            new()
            {
                Field = "Alignment.Moral",
                Operator = RuleComparisonOp.In,
                Value = new List<string> { "Good" }
            },
            new()
            {
                Field = "PeopleType",
                Operator = RuleComparisonOp.In,
                Value = new List<string> { "Baronial" }
            }
        };

        var fails = RuleTreeEvaluator.Evaluate(rules, ResolveFailingContext, NormalizeToken);
        var passes = RuleTreeEvaluator.Evaluate(rules, ResolvePassingContext, NormalizeToken);

        Assert.False(fails);
        Assert.True(passes);

        static IEnumerable<string> ResolveFailingContext(string field)
            => field switch
            {
                "Alignment.Moral" => new[] { "Good" },
                "PeopleType" => new[] { "Amlesian" },
                _ => Array.Empty<string>()
            };

        static IEnumerable<string> ResolvePassingContext(string field)
            => field switch
            {
                "Alignment.Moral" => new[] { "Good" },
                "PeopleType" => new[] { "Baronial" },
                _ => Array.Empty<string>()
            };
    }

    [Fact]
    public void Evaluate_NotInRule_BlocksMatchingValues()
    {
        var rules = new List<RuleClause>
        {
            new()
            {
                Field = "Status",
                Operator = RuleComparisonOp.NotIn,
                Value = new List<string> { "Outlawed" }
            }
        };

        var allowed = RuleTreeEvaluator.Evaluate(rules, ResolveAllowed, NormalizeToken);
        var blocked = RuleTreeEvaluator.Evaluate(rules, ResolveBlocked, NormalizeToken);

        Assert.True(allowed);
        Assert.False(blocked);

        static IEnumerable<string> ResolveAllowed(string field)
            => field.Equals("Status", StringComparison.OrdinalIgnoreCase)
                ? Array.Empty<string>()
                : Array.Empty<string>();

        static IEnumerable<string> ResolveBlocked(string field)
            => field.Equals("Status", StringComparison.OrdinalIgnoreCase)
                ? new[] { "Outlawed" }
                : Array.Empty<string>();
    }

    [Fact]
    public void Evaluate_OnlyRule_RequiresResolvedValuesToBeSubset()
    {
        var rules = new List<RuleClause>
        {
            new()
            {
                Field = "Bracket",
                Operator = RuleComparisonOp.Only,
                Value = new List<string> { "Scout" }
            }
        };

        var pureScout = RuleTreeEvaluator.Evaluate(
            rules,
            (_, field) => field.Equals("Bracket", StringComparison.OrdinalIgnoreCase)
                ? new[] { "Scout" }
                : Array.Empty<string>(),
            NormalizeToken);

        var mixedBrackets = RuleTreeEvaluator.Evaluate(
            rules,
            (_, field) => field.Equals("Bracket", StringComparison.OrdinalIgnoreCase)
                ? new[] { "Scout", "Wizard" }
                : Array.Empty<string>(),
            NormalizeToken);

        var noBracket = RuleTreeEvaluator.Evaluate(
            rules,
            (_, field) => Array.Empty<string>(),
            NormalizeToken);

        Assert.True(pureScout);
        Assert.False(mixedBrackets);
        Assert.False(noBracket);
    }

    [Fact]
    public void ContainsPositiveInValue_OnlyMatchesPositiveInRules()
    {
        var rules = new List<RuleClause>
        {
            new()
            {
                Field = "PeopleType",
                Operator = RuleComparisonOp.In,
                Value = new List<string> { "Baronial" }
            },
            new()
            {
                Field = "PeopleType",
                Operator = RuleComparisonOp.NotIn,
                Value = new List<string> { "Amlesian" }
            },
            new()
            {
                Field = "Bracket",
                Operator = RuleComparisonOp.Only,
                Value = new List<string> { "Scout" }
            }
        };

        Assert.True(RuleTreeEvaluator.ContainsPositiveInValue(
            rules,
            field: "PeopleType",
            value: "Baronial",
            normalizeField: NormalizeToken,
            normalizeValue: NormalizeToken));

        Assert.False(RuleTreeEvaluator.ContainsPositiveInValue(
            rules,
            field: "PeopleType",
            value: "Amlesian",
            normalizeField: NormalizeToken,
            normalizeValue: NormalizeToken));

        Assert.True(RuleTreeEvaluator.ContainsPositiveInValue(
            rules,
            field: "Bracket",
            value: "Scout",
            normalizeField: NormalizeToken,
            normalizeValue: NormalizeToken));
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }
}
