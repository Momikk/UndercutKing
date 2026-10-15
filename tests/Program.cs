using UndercutKing;

if (args.Contains("--probe")) { await TransportRegression.ProbeAsync(); return; }

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
foreach (var status in new[] { HighlightStatus.Undercut, HighlightStatus.Unknown, HighlightStatus.BagMatch })
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
var retainerChecks = 0;
void RetainerCheck(string scenario, bool passes)
{
    retainerChecks++;
    if (!passes) throw new Exception(scenario);
}
var retainerSnapshots = new List<RetainerSnapshot>
{
    new() { CharacterId = 1, RetainerId = 10, CapturedAt = now, Listings = [listed with { Quantity = 99 }, listed with { Slot = 1, Price = 900 }] },
    new() { CharacterId = 1, RetainerId = 11, CapturedAt = now, Listings = [listed, listed with { Slot = 1 }] },
    new() { CharacterId = 2, RetainerId = 10, CapturedAt = now, Listings = [listed, listed with { Slot = 1 }] },
    new() { CharacterId = 1, RetainerId = 10, CapturedAt = now.AddDays(-1), Listings = [listed, listed with { Slot = 1 }] },
};
UndercutSummary ForRetainer(ulong id, Func<RetainerSnapshot, Listing, HighlightStatus>? evaluate = null) =>
    UndercutSummary.Count(retainerSnapshots.Where(x => x.RetainerId == id), 1, evaluate ?? Evaluate);
RetainerCheck("counts slots, not stack quantity, and ignores older or foreign-character snapshots", ForRetainer(10).RetainerSuffix == " (1)");
RetainerCheck("separate retainer count", ForRetainer(11).RetainerSuffix == " (2)");
RetainerCheck("unscanned retainer is not zero", ForRetainer(12).RetainerSuffix == " (?)");
RetainerCheck("expired prices are not zero", ForRetainer(10, (_, _) => HighlightStatus.Unknown).RetainerSuffix == " (?)");
RetainerCheck("partial prices retain confirmed count", ForRetainer(10, (_, item) => item.Slot == 0 ? HighlightStatus.Undercut : HighlightStatus.Unknown).RetainerSuffix == " (1+)");
retainerSnapshots[0].Listings[0] = listed with { Price = 899 };
RetainerCheck("repricing to zero removes this retainer's suffix", ForRetainer(10).RetainerSuffix == "");
RetainerCheck("repricing does not change another retainer's count", ForRetainer(11).RetainerSuffix == " (2)");
retainerSnapshots.Add(new() { CharacterId = 1, RetainerId = 12, CapturedAt = now, Listings = [] });
RetainerCheck("scanned empty retainer hides zero", ForRetainer(12).RetainerSuffix == "");
var originalText = System.Text.Encoding.UTF8.GetBytes("出售中（14件）\0");
var coloredText = new byte[] { 2, 19, 2, 1, 3 }.Concat(originalText).ToArray();
var decorated = HighlightText.Compose(originalText, "出售中（14件）", HighlightStatus.Undercut, " (3)");
RetainerCheck("append count after original sale quantity", System.Text.Encoding.UTF8.GetString(decorated) == "出售中（14件） (3)\0");
var updatedText = HighlightText.Compose(originalText, "出售中（14件）", HighlightStatus.Undercut, " (2)");
RetainerCheck("changing counts does not duplicate suffix", System.Text.Encoding.UTF8.GetString(updatedText) == "出售中（14件） (2)\0");
RetainerCheck("normal counted text retains original game payloads", HighlightText.Compose(coloredText, "出售中（14件）", HighlightStatus.Normal, " (2)").AsSpan().StartsWith(coloredText.AsSpan(0, coloredText.Length - 1)));
RetainerCheck("colored highlighting uses plain text and one suffix", HighlightText.Compose(coloredText, "出售中（14件）", HighlightStatus.Unknown, " (?)").SequenceEqual(System.Text.Encoding.UTF8.GetBytes("出售中（14件） (?)\0")));
RetainerCheck("disabling decoration restores exact original bytes", HighlightText.Compose(coloredText, "出售中（14件）", HighlightStatus.Normal, "").SequenceEqual(coloredText));
RetainerCheck("disabling count keeps highlight without suffix", HighlightText.Compose(coloredText, "出售中（14件）", HighlightStatus.Undercut, "").SequenceEqual(originalText));
Console.WriteLine($"PASS: {retainerChecks} per-retainer count and text decoration regression scenarios");
var refreshChecks = 0;
void RefreshCheck(string scenario, bool passes)
{
    refreshChecks++;
    if (!passes) throw new Exception(scenario);
}
var sixMinuteQuote = market with { ObservedAt = now.AddMinutes(-6) };
RefreshCheck("no quote requests immediately", MarketRefreshRules.ShouldRequest(now, null, null, false, 5));
RefreshCheck("two-day validity does not suppress five-minute polling", PriceRules.Evaluate(listed, sixMinuteQuote, now, 2880) != HighlightStatus.Unknown && MarketRefreshRules.ShouldRequest(now, now.AddMinutes(-6), sixMinuteQuote, false, 5));
RefreshCheck("fresh game or API quote avoids needless polling", !MarketRefreshRules.ShouldRequest(now, null, market, false, 5));
RefreshCheck("recent attempts throttle old uploads", !MarketRefreshRules.ShouldRequest(now, now.AddMinutes(-1), market with { ObservedAt = now.AddDays(-3) }, false, 5));
RefreshCheck("failed queries wait before retrying", !MarketRefreshRules.ShouldRequest(now, now.AddMinutes(-1), null, false, 5));
RefreshCheck("retry at interval boundary", MarketRefreshRules.ShouldRequest(now, now.AddMinutes(-5), null, false, 5));
RefreshCheck("quote refresh at interval boundary", MarketRefreshRules.ShouldRequest(now, null, market with { ObservedAt = now.AddMinutes(-5) }, false, 5));
RefreshCheck("manual refresh bypasses poll interval", MarketRefreshRules.ShouldRequest(now, now.AddSeconds(-6), market, true, 5));
RefreshCheck("manual refresh keeps five-second cooldown", !MarketRefreshRules.ShouldRequest(now, now.AddSeconds(-4), market, true, 5));
RefreshCheck("shorter configured interval takes effect", MarketRefreshRules.ShouldRequest(now, now.AddMinutes(-2), sixMinuteQuote, false, 1));
RefreshCheck("longer configured interval takes effect", !MarketRefreshRules.ShouldRequest(now, now.AddMinutes(-6), sixMinuteQuote, false, 30));
RefreshCheck("invalid interval cannot hammer the API", !MarketRefreshRules.ShouldRequest(now, now.AddSeconds(-30), null, false, 0));
RefreshCheck("older uploads never become fresh by fetching them", PriceRules.Evaluate(listed, market with { ObservedAt = now.AddDays(-3) }, now, 2880) == HighlightStatus.Unknown);
Console.WriteLine($"PASS: {refreshChecks} background refresh scheduling regression scenarios");
await TransportRegression.RunAsync();
var bagChecks = 0;
void BagCheck(string scenario, bool ok) { bagChecks++; if (!ok) throw new Exception(scenario); }
var bagItems = BagMatchRules.ItemIds([(100u, 1u), (100u, 99u), (101u, 0u), (0u, 1u)]);
BagCheck("positive nonempty bag slots only, duplicates merged", bagItems.SetEquals([100u]));
BagCheck("NQ listing matches bag base ID", BagMatchRules.Display(listed, HighlightStatus.Normal, bagItems, true, true) == HighlightStatus.BagMatch);
BagCheck("HQ listing matches same bag base ID", BagMatchRules.Display(listed with { Hq = true }, HighlightStatus.Normal, bagItems, true, true) == HighlightStatus.BagMatch);
BagCheck("red overrides blue visually", BagMatchRules.Display(listed, HighlightStatus.Undercut, bagItems, true, true) == HighlightStatus.Undercut);
BagCheck("blue overrides unknown visually", BagMatchRules.Display(listed, HighlightStatus.Unknown, bagItems, true, true) == HighlightStatus.BagMatch);
BagCheck("unrelated item keeps price highlight", BagMatchRules.Display(listed with { ItemId = 102 }, HighlightStatus.Undercut, bagItems, true, true) == HighlightStatus.Undercut);
BagCheck("removing bag item restores price status", BagMatchRules.Display(listed, HighlightStatus.Undercut, new HashSet<uint>(), true, true) == HighlightStatus.Undercut);
BagCheck("turning blue off restores price status", BagMatchRules.Display(listed, HighlightStatus.Unknown, bagItems, true, false) == HighlightStatus.Unknown);
BagCheck("blue works independently of price highlight toggle", BagMatchRules.Display(listed, HighlightStatus.Undercut, bagItems, false, true) == HighlightStatus.BagMatch);
BagCheck("all highlighting disabled returns normal", BagMatchRules.Display(listed, HighlightStatus.Undercut, bagItems, false, false) == HighlightStatus.Normal);
BagCheck("missing snapshot cannot mark another retainer", BagMatchRules.Display(null, HighlightStatus.Undercut, bagItems, true, true) == HighlightStatus.Normal);
BagCheck("empty or unavailable bag has no blue match", BagMatchRules.Display(listed, HighlightStatus.Normal, new HashSet<uint>(), true, true) == HighlightStatus.Normal);
BagCheck("price evaluation remains undercut for blue item", PriceRules.Evaluate(listed, market, now, 2880) == HighlightStatus.Undercut);
BagCheck("price counter remains independent of blue", UndercutSummary.Count([new() { CharacterId = 1, RetainerId = 10, Listings = [listed] }], 1, Evaluate).Undercut == 1);
Console.WriteLine($"PASS: {bagChecks} bag matching and color precedence regression scenarios");
var retainerBagChecks = 0;
void RetainerBagCheck(string scenario, bool ok) { retainerBagChecks++; if (!ok) throw new Exception(scenario); }
var bagRetainers = new List<RetainerSnapshot>
{
    new() { CharacterId = 1, RetainerId = 10, CapturedAt = now, Listings = [listed with { Hq = true }] },
    new() { CharacterId = 1, RetainerId = 11, CapturedAt = now, Listings = [listed with { ItemId = 102 }] },
    new() { CharacterId = 2, RetainerId = 12, CapturedAt = now, Listings = [listed] },
    new() { CharacterId = 1, RetainerId = 11, CapturedAt = now.AddDays(-1), Listings = [listed] },
};
RetainerBagCheck("HQ retainer listing matches NQ/HQ-neutral bag ID", BagMatchRules.MatchesRetainer(bagRetainers, 1, 10, bagItems));
RetainerBagCheck("nonmatching latest snapshot wins over matching old snapshot", !BagMatchRules.MatchesRetainer(bagRetainers, 1, 11, bagItems));
RetainerBagCheck("another character cannot mark current retainer blue", !BagMatchRules.MatchesRetainer(bagRetainers, 1, 12, bagItems));
RetainerBagCheck("unscanned retainer cannot match", !BagMatchRules.MatchesRetainer(bagRetainers, 1, 13, bagItems));
RetainerBagCheck("moving item out clears retainer blue", !BagMatchRules.MatchesRetainer(bagRetainers, 1, 10, new HashSet<uint>()));
RetainerBagCheck("logout has no bag match", !BagMatchRules.MatchesRetainer(bagRetainers, 0, 10, bagItems));
bagRetainers[0].Listings.Clear();
RetainerBagCheck("scanned empty retainer loses blue", !BagMatchRules.MatchesRetainer(bagRetainers, 1, 10, bagItems));
bagRetainers[0].Listings.AddRange([listed with { ItemId = 102 }, listed with { Slot = 1, Hq = true }]);
RetainerBagCheck("any matching sale slot marks retainer", BagMatchRules.MatchesRetainer(bagRetainers, 1, 10, bagItems));
RetainerBagCheck("undercut suffix remains independent of blue", UndercutSummary.Count(bagRetainers.Where(x => x.RetainerId == 10), 1, (_, _) => HighlightStatus.Undercut).RetainerSuffix == " (2)");
Console.WriteLine($"PASS: {retainerBagChecks} retainer-list bag matching regression scenarios");
var overlayChecks = 0;
void OverlayCheck(string scenario, bool ok) { overlayChecks++; if (!ok) throw new Exception(scenario); }
var activeSale = new RetainerSnapshot { CharacterId = 1, RetainerId = 10, Listings = [listed, listed with { Slot = 1, Hq = true }, listed with { ItemId = 102 }] };
HashSet<uint> OverlayTargets(RetainerSnapshot? snapshot = null, ulong character = 1, ulong retainer = 10, bool visible = true, bool blue = true, bool mirror = true) =>
    BagOverlayRules.Targets(snapshot ?? activeSale, character, retainer, visible, blue, mirror, bagItems);
OverlayCheck("only blue matches of current sale list, qualities and repeated listings merged", OverlayTargets().SetEquals([100u]));
OverlayCheck("closing sale list removes bag frames", OverlayTargets(visible: false).Count == 0);
OverlayCheck("another retainer does not inherit frames", OverlayTargets(retainer: 11).Count == 0);
OverlayCheck("another character does not inherit frames", OverlayTargets(character: 2).Count == 0);
OverlayCheck("logged out shows no frames", OverlayTargets(character: 0).Count == 0);
OverlayCheck("no active retainer shows no frames", OverlayTargets(retainer: 0).Count == 0);
OverlayCheck("blue feature off removes frames", OverlayTargets(blue: false).Count == 0);
OverlayCheck("independent mirror toggle removes frames", OverlayTargets(mirror: false).Count == 0);
OverlayCheck("missing live sale snapshot shows no frames", BagOverlayRules.Targets(null, 1, 10, true, true, true, bagItems).Count == 0);
OverlayCheck("empty retainer shows no frames", OverlayTargets(new() { CharacterId = 1, RetainerId = 10 }).Count == 0);
OverlayCheck("removed bag item clears frames", BagOverlayRules.Targets(activeSale, 1, 10, true, true, true, new HashSet<uint>()).Count == 0);
OverlayCheck("first and last normal-bag slots are accepted", BagOverlayRules.ValidSource(0, 0) && BagOverlayRules.ValidSource(3, 34));
OverlayCheck("saddlebag or corrupt page excluded", !BagOverlayRules.ValidSource(4, 0) && !BagOverlayRules.ValidSource(-1, 0));
OverlayCheck("out-of-range slot excluded", !BagOverlayRules.ValidSource(0, 35) && !BagOverlayRules.ValidSource(0, -1));
Console.WriteLine($"PASS: {overlayChecks} mirrored bag highlight gating and slot safety scenarios");
var priorityChecks = 0;
void PriorityCheck(string name, HighlightStatus price, bool match, bool redYellowEnabled, bool blueEnabled, HighlightStatus expected)
{
    priorityChecks++;
    if (BagMatchRules.Prioritize(price, match, redYellowEnabled, blueEnabled) != expected) throw new Exception(name);
}
PriorityCheck("retainer/item red precedes bag match", HighlightStatus.Undercut, true, true, true, HighlightStatus.Undercut);
PriorityCheck("retainer/item blue precedes yellow", HighlightStatus.Unknown, true, true, true, HighlightStatus.BagMatch);
PriorityCheck("red remains without bag match", HighlightStatus.Undercut, false, true, true, HighlightStatus.Undercut);
PriorityCheck("yellow remains without bag match", HighlightStatus.Unknown, false, true, true, HighlightStatus.Unknown);
PriorityCheck("normal price can still be blue", HighlightStatus.Normal, true, true, true, HighlightStatus.BagMatch);
PriorityCheck("disabled price highlighting allows blue", HighlightStatus.Undercut, true, false, true, HighlightStatus.BagMatch);
PriorityCheck("disabled blue allows yellow", HighlightStatus.Unknown, true, true, false, HighlightStatus.Unknown);
PriorityCheck("disabled colors stay normal", HighlightStatus.Undercut, true, false, false, HighlightStatus.Normal);
Console.WriteLine($"PASS: {priorityChecks} shared highlight priority scenarios");
