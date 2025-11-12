// using labyItems.Models;
// namespace labyItems.Categories;

// public sealed class WeaponCategory : CategoryBase
// {
//     // You can expose these to bind to your CollectionViews
//     public List<CalcRow> Magic { get; } = new();
//     public List<CalcRow> Spirit { get; } = new();
//     public List<CalcRow> Other { get; } = new();

//     public WeaponCategory()
//     {
//         // Magic
//         Magic.AddRange(new[]
//         {
//             new CalcRow { Name = "+0 Magic, +1 vs one group", Cost = 30 },
//             new CalcRow { Name = "+1 Magic", Cost = 40 },
//             new CalcRow { Name = "+2 Magic", Cost = 60 },
//             new CalcRow { Name = "+0 Pure Magic", Cost = 40 },
//             new CalcRow { Name = "Add magical colour/hue (each)", Cost = 3, AllowMultiple = true },
//         });

//         // Spirit
//         Spirit.AddRange(new[]
//         {
//             new CalcRow { Name = "+0 Spirit (any one alignment)", Cost = 25 },
//             new CalcRow { Name = "+0 Spirit, +1 vs one type", Cost = 30 },
//             new CalcRow { Name = "+0 Spirit, +1 vs one group", Cost = 35 },
//             new CalcRow { Name = "+1 Spirit", Cost = 50 },
//             new CalcRow { Name = "+2 Spirit", Cost = 75 },
//             new CalcRow { Name = "+0 Pure Spirit", Cost = 45 },
//             new CalcRow { Name = "Add non-opposite spirit alignment", Cost = 5, AllowMultiple = true },
//         });

//         // Other
//         Other.AddRange(new[]
//         {
//             new CalcRow { Name = "+0 Mantic (any one colour & any one alignment)", Cost = 50 },
//             new CalcRow { Name = "+1 Mantic", Cost = 100 },
//             new CalcRow { Name = "+0 Pure Mantic", Cost = 90 },
//             new CalcRow { Name = "+1 Physical", Cost = 20 },
//             new CalcRow { Name = "Magic/Spirit weapon turns PURE 1/day (5 mins)", Cost = 5 },
//             new CalcRow { Name = "Mantic weapon turns PURE 1/day (5 mins)", Cost = 10 },
//             new CalcRow { Name = "Adventure perm dmg 1/day (5 mins)", Cost = 25 },
//             new CalcRow { Name = "Inflicts dmg thru PAC (always)", Cost = 30 },
//             new CalcRow { Name = "Oversize weapon can be blade-sharpened", Cost = 5 },
//             new CalcRow { Name = "Supernatural weapon can be blade-sharpened", Cost = 10 },
//             new CalcRow { Name = "Cut thru Aura of Defence 1/day (5 mins)", Cost = 25 },
//         });
//     }

//     public override (int TotalIsp, List<string> Lines) AddToSummary()
//     {
//         var total = SumRows(Magic) + SumRows(Spirit) + SumRows(Other);
//         // Round to nearest 5 above 0, if you keep that rule
//         if (total > 0) total = (int)(Math.Round(total / 5.0, MidpointRounding.AwayFromZero) * 5);

//         var lines = new List<string>();
//         lines.AddRange(LinesFrom(Magic));
//         lines.AddRange(LinesFrom(Spirit));
//         lines.AddRange(LinesFrom(Other));

//         return (total, lines);
//     }
// }
