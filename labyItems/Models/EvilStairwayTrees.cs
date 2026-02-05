using System;
using System.Collections.Generic;
using System.Linq;

namespace labyItems.Models;

public static class EvilStairwayTrees
{
    public static readonly EvilStairwayTree Necromancy = new(
        "Necromancy",
        new[]
        {
            Node("Animate Zombie"),
            Node("Animate Skeleton"),
            Node("Create Ghoul"),
            Node("Create Mummy"),
            Node("Summon Wraith/Wight", new[] { "Summon Wraith", "Summon Barrow-Wight" }),
            Node("Summon Vampire")
        });

    public static readonly EvilStairwayTree Control = new(
        "Control",
        new[]
        {
            Node("Control Zombie"),
            Node("Control Skeleton"),
            Node("Control Ghoul"),
            Node("Control Mummy"),
            Node("Control Wraith/Wight", new[] { "Control Wraith" })
        });

    public static readonly EvilStairwayTree Causing = new(
        "Causing",
        new[]
        {
            Node("Cause Minor Wound", optional: true),
            Node("Cause Wound"),
            Node("Cause Serious Wounds"),
            Node("Cause Disease"),
            Node("Cause Grievous Wounds"),
            Node("Suspend Life"),
            Node("Touch of Death")
        });

    public static readonly EvilStairwayTree Dominion = new(
        "Dominion",
        new[]
        {
            Node("Temporary Curse"),
            Node("Curse"),
            Node("Cause Fear"),
            Node("Beguile Spirit"),
            Node("Spiritual Mastery")
        });

    public static readonly IReadOnlyList<EvilStairwayTree> DefaultTrees = new[]
    {
        Necromancy,
        Control,
        Causing,
        Dominion
    };

    public static readonly IReadOnlyDictionary<string, EvilStairwayTree> TreeLookup = BuildTreeLookup();

    private static Dictionary<string, EvilStairwayTree> BuildTreeLookup()
    {
        var dict = new Dictionary<string, EvilStairwayTree>(StringComparer.OrdinalIgnoreCase);
        foreach (var tree in DefaultTrees)
        {
            foreach (var name in tree.AllNames)
            {
                if (!dict.ContainsKey(name))
                    dict[name] = tree;
            }
        }

        return dict;
    }

    private static EvilStairwayNode Node(string name, bool optional = false)
        => new(new[] { name }, optional, name);

    private static EvilStairwayNode Node(string displayName, string[] names, bool optional = false)
        => new(names, optional, displayName);
}

public sealed class EvilStairwayTree
{
    public string Name { get; }
    public IReadOnlyList<EvilStairwayNode> Nodes { get; }
    public bool HasOptionalNodes => Nodes.Any(n => n.Optional);

    public EvilStairwayTree(string name, IReadOnlyList<EvilStairwayNode> nodes)
    {
        Name = name;
        Nodes = nodes;
    }

    public IReadOnlyList<string> AllNames
        => Nodes.SelectMany(n => n.Names).ToList();

    public bool ContainsName(string name)
        => Nodes.Any(n => n.Matches(name));

    public int FindNodeIndex(string name)
    {
        for (var i = 0; i < Nodes.Count; i++)
        {
            if (Nodes[i].Matches(name))
                return i;
        }

        return -1;
    }

    public int GetPreviousRequiredIndex(int index)
    {
        for (var i = index - 1; i >= 0; i--)
        {
            if (!Nodes[i].Optional)
                return i;
        }

        return -1;
    }
}

public sealed class EvilStairwayNode
{
    public IReadOnlyList<string> Names { get; }
    public bool Optional { get; }
    public string DisplayName { get; }

    public EvilStairwayNode(IReadOnlyList<string> names, bool optional, string displayName)
    {
        Names = names;
        Optional = optional;
        DisplayName = displayName;
    }

    public bool Matches(string name)
        => Names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
}
