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
Check("HQ does not compare with cheap NQ", PriceRules.Evaluate(listed with { Hq = true }, market, now, 15), HighlightStatus.Normal);
Check("expired data is unknown", PriceRules.Evaluate(listed, market with { ObservedAt = now.AddMinutes(-16) }, now, 15), HighlightStatus.Unknown);
Check("no data is unknown", PriceRules.Evaluate(listed, null, now, 15), HighlightStatus.Unknown);
Check("not-yet-loaded price is unknown", PriceRules.Evaluate(listed with { Price = 0 }, market, now, 15), HighlightStatus.Unknown);
Check("complete empty market is competitive", PriceRules.Evaluate(listed, market with { Nq = null }, now, 15), HighlightStatus.Normal);
Check("partial market cannot prove absent quality", PriceRules.Evaluate(listed, market with { Nq = null, Complete = false }, now, 15), HighlightStatus.Unknown);
Console.WriteLine($"PASS: {checks} pricing regression scenarios");
