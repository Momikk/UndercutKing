using UndercutKing;

var now = DateTimeOffset.UtcNow;
var listed = new Listing(0, 0, 100, false, 1, 1000);
var market = new MarketQuote(900, 1500, now, "test");
var checks = 0;
void Check(string scenario, HighlightStatus actual, HighlightStatus expected)
{
    checks++;
    if (actual != expected) throw new Exception($"{scenario}: expected {expected}, got {actual}");
}
Check("initial undercut", PriceRules.Evaluate(listed, market, now, 15), HighlightStatus.Undercut);
Check("lowering price clears highlight", PriceRules.Evaluate(listed with { Price = 899 }, market, now, 15), HighlightStatus.Normal);
Check("equal price is competitive", PriceRules.Evaluate(listed with { Price = 900 }, market, now, 15), HighlightStatus.Normal);
Check("fresh market rise clears highlight", PriceRules.Evaluate(listed, market with { Nq = 1100 }, now, 15), HighlightStatus.Normal);
Check("HQ compares with cheaper NQ", PriceRules.Evaluate(listed with { Hq = true }, market, now, 15), HighlightStatus.Undercut);
Check("NQ compares with cheaper HQ", PriceRules.Evaluate(listed, market with { Nq = 1500, Hq = 900 }, now, 15), HighlightStatus.Undercut);
Check("single quality quote serves both qualities", PriceRules.Evaluate(listed, market with { Nq = null, Hq = 900 }, now, 15), HighlightStatus.Undercut);
Check("expired data is unknown", PriceRules.Evaluate(listed, market with { ObservedAt = now.AddMinutes(-16) }, now, 15), HighlightStatus.Unknown);
Check("no data is unknown", PriceRules.Evaluate(listed, null, now, 15), HighlightStatus.Unknown);
Check("not-yet-loaded price is unknown", PriceRules.Evaluate(listed with { Price = 0 }, market, now, 15), HighlightStatus.Unknown);
Check("complete empty market is competitive", PriceRules.Evaluate(listed, market with { Nq = null, Hq = null }, now, 15), HighlightStatus.Normal);
Check("partial empty market is unknown", PriceRules.Evaluate(listed, market with { Nq = null, Hq = null, Complete = false }, now, 15), HighlightStatus.Unknown);
Check("47-hour quote remains valid with two-day setting", PriceRules.Evaluate(listed, market with { ObservedAt = now.AddHours(-47) }, now, 2880), HighlightStatus.Undercut);
Check("49-hour quote expires with two-day setting", PriceRules.Evaluate(listed, market with { ObservedAt = now.AddHours(-49) }, now, 2880), HighlightStatus.Unknown);
var snapshots = new List<RetainerSnapshot>
{
    new() { CharacterId = 1, RetainerId = 10, Listings = [listed, listed with { Slot = 1 }] },
    new() { CharacterId = 2, RetainerId = 20, Listings = [listed] },
};
HighlightStatus Evaluate(RetainerSnapshot _, Listing value) => PriceRules.Evaluate(value, market, now, 15);
var summary = UndercutSummary.Count(snapshots, 1, Evaluate);
if (summary.Undercut != 2 || summary.ScannedRetainers != 1 || summary.BarText != "压价 2") throw new Exception("DTR must count listing slots only for current character");
snapshots[0].Listings[0] = listed with { Price = 899 };
if (UndercutSummary.Count(snapshots, 1, Evaluate).Undercut != 1) throw new Exception("DTR did not decrease after repricing");
if (UndercutSummary.Count(snapshots, 1, (_, _) => HighlightStatus.Unknown).BarText != "压价 ?") throw new Exception("Unknown prices must not report confirmed zero");
if (new UndercutSummary(2, 1, 1).BarText != "压价 2+") throw new Exception("Partial counts must be marked");
if (UndercutSummary.Count(snapshots, 0, Evaluate).Undercut != 0) throw new Exception("Logged-out state leaked another character count");
Console.WriteLine($"PASS: {checks} pricing and 5 DTR regression scenarios");
var appearanceChecks = 0;
void AppearanceCheck(string scenario, bool passes)
{
    appearanceChecks++;
    if (!passes) throw new Exception(scenario);
}
var lightOriginal = new TextAppearance(new(50, 50, 50, 210), new(230, 230, 230, 180), 2);
var darkOriginal = new TextAppearance(new(220, 220, 220), new(30, 30, 30), 0);
foreach (var status in new[] { HighlightStatus.Undercut, HighlightStatus.Unknown })
{
    var lightStyle = HighlightAppearance.Apply(status, lightOriginal, HighlightTheme.Light);
    var darkStyle = HighlightAppearance.Apply(status, darkOriginal, HighlightTheme.Dark);
    foreach (var background in new[] { new TextColor(170, 170, 170), new TextColor(190, 190, 190) })
        AppearanceCheck("light theme contrast", lightStyle.Fill.Contrast(background) >= 4.5);
    foreach (var background in new[] { new TextColor(32, 32, 32), new TextColor(50, 50, 50) })
        AppearanceCheck("dark theme contrast", darkStyle.Fill.Contrast(background) >= 4.5);
    AppearanceCheck("auto light palette", HighlightAppearance.Apply(status, lightOriginal, HighlightTheme.Auto) == lightStyle);
    AppearanceCheck("auto dark palette", HighlightAppearance.Apply(status, darkOriginal, HighlightTheme.Auto) == darkStyle);
    AppearanceCheck("outline enabled without changing other flags", lightStyle.Flags == (lightOriginal.Flags | HighlightAppearance.EdgeFlag));
    AppearanceCheck("all original fields restored", HighlightAppearance.Restore(lightOriginal, lightStyle, lightStyle) == lightOriginal);
    var external = lightStyle with { Fill = new TextColor(10, 90, 120), Edge = new TextColor(60, 70, 80), Flags = (ushort)(lightStyle.Flags | 1) };
    var restored = HighlightAppearance.Restore(lightOriginal, external, lightStyle);
    AppearanceCheck("external recoloring preserved", restored.Fill == external.Fill && restored.Edge == external.Edge);
    AppearanceCheck("restore only our outline bit", restored.Flags == (lightOriginal.Flags | 1));
}
AppearanceCheck("normal appearance not changed", HighlightAppearance.Apply(HighlightStatus.Normal, lightOriginal, HighlightTheme.Dark) == lightOriginal);
Console.WriteLine($"PASS: {appearanceChecks} theme contrast and appearance restoration scenarios");
