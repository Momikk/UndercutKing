using UndercutKing;

internal static class MixedQualityRegression
{
    internal static void Run()
    {
        var checks = 0;
        void Check(bool condition, string reason) { checks++; if (!condition) throw new Exception(reason); }
        BagItem[] items = [new(100, 1, false), new(100, 99, true), new(100, 3, false),
            new(200, 2, false), new(200, 1, false), new(300, 1, true)];
        var targets = MixedQualityRules.Targets(items);
        Check(targets.SetEquals([100u]), "Only the same base item with both qualities must match");
        Check(items.Count(x => targets.Contains(x.ItemId)) == 3, "Every NQ and HQ stack of the matching item must be marked");
        Check(MixedQualityRules.Targets(items.Where(x => !x.Hq)).Count == 0, "Only NQ stacks must not match");
        Check(MixedQualityRules.Targets(items.Where(x => x.Hq)).Count == 0, "Only HQ stacks must not match");
        Check(MixedQualityRules.Targets([new(100, 1, false), new(200, 1, true)]).Count == 0, "Different base items must not be paired");
        Check(MixedQualityRules.Targets([new(100, 1, false), new(100, 0, true)]).Count == 0, "Removed HQ stack must clear the mark");
        Check(MixedQualityRules.Targets([new(100, 0, false), new(100, 1, true)]).Count == 0, "Removed NQ stack must clear the mark");
        Check(MixedQualityRules.Targets([new(0, 1, false), new(0, 1, true)]).Count == 0, "Empty IDs must not match");
        Check(MixedQualityRules.Targets([]).Count == 0, "Empty or unavailable contents must clear targets");
        Check(MixedQualityRules.Targets(items, false).Count == 0, "Feature off must clear targets");
        Check(MixedQualityRules.Targets(items.Concat([new BagItem(300, 1, false)])).SetEquals([100u, 300u]), "Multiple mixed items must be found independently");
        var mirrors = BagOverlayRules.Targets(null, 1, 0, false, false, false, new HashSet<uint>());
        mirrors.UnionWith(targets);
        Check(mirrors.SetEquals([100u]), "Mixed quality marks must work without a retainer and with retainer highlighting disabled");
        Console.WriteLine($"PASS: {checks} mixed NQ/HQ bag highlighting scenarios");
    }
}
