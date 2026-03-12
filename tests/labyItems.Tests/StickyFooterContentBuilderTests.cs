using System;
using System.IO;
using labyItems.Controls;
using Xunit;

namespace labyItems.Tests;

public sealed class StickyFooterContentBuilderTests : ServiceTestBase
{
    [Fact]
    public void Build_WhenRowsAreEmpty_ReturnsEmptyStateContent()
    {
        var result = StickyFooterContentBuilder.Build(sourceRows: null);

        Assert.False(result.HasRows);
        Assert.Empty(result.Rows);
        Assert.Equal(StickyFooterContentBuilder.DefaultEmptyMessage, result.EmptyMessage);
    }

    [Fact]
    public void Build_WhenRowsExist_FiltersBlankRowsAndPreservesValues()
    {
        var rows = new[]
        {
            new ContributionRow { Id = "a", Text = "Spell: Shock = 20", RunningTotal = 20 },
            new ContributionRow { Id = "b", Text = "  ", RunningTotal = 20 },
            new ContributionRow { Id = "c", Text = "Miracle: Heal = 30", RunningTotal = 50 }
        };

        var result = StickyFooterContentBuilder.Build(rows, "Nothing selected");

        Assert.True(result.HasRows);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("Spell: Shock = 20", result.Rows[0].Text);
        Assert.Equal(20, result.Rows[0].RunningTotal);
        Assert.Equal("Miracle: Heal = 30", result.Rows[1].Text);
        Assert.Equal(50, result.Rows[1].RunningTotal);
        Assert.Equal("Nothing selected", result.EmptyMessage);
    }

    [Fact]
    public void MpAndIspTabPages_UseStickyFooterWithBreakdownBinding()
    {
        var tabPages = new[]
        {
            "labyItems/Pages/Calculator/MpBasicPage.xaml",
            "labyItems/Pages/Calculator/MpCrewPage.xaml",
            "labyItems/Pages/Calculator/MpThemedayPage.xaml",
            "labyItems/Pages/Calculator/CalcNav/ArmourNav.xaml",
            "labyItems/Pages/Calculator/Weapon/WeaponConfigPage.xaml",
            "labyItems/Pages/Calculator/CalcNav/CharmNav.xaml",
            "labyItems/Pages/Calculator/CalcNav/LifeConfigPage.xaml",
            "labyItems/Pages/Calculator/CalcNav/MoreNav.xaml"
        };

        foreach (var relativePath in tabPages)
        {
            var fullPath = Path.Combine(
                ServiceTestEnvironment.RepoRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(fullPath), $"Expected XAML file to exist: {fullPath}");
            var xaml = File.ReadAllText(fullPath);

            Assert.Contains("<controls:StickyFooterControl", xaml, StringComparison.Ordinal);
            Assert.Contains("BreakdownItems=", xaml, StringComparison.Ordinal);
        }
    }
}
