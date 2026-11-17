using System.Text.Json;
using System.Text.Json.Serialization;

namespace UndercutKing;

internal sealed record IpcSnapshot(ulong CharacterId, IpcSnapshot.RetainerDto[] Retainers)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static IpcSnapshot Empty { get; } = new(0, []);
    public int ApiVersion => 1;
    [JsonIgnore] public bool HasRefreshableListings => CharacterId != 0 && Retainers.Any(x => x.Listings.Length > 0);
    [JsonNumberHandling(JsonNumberHandling.WriteAsString)] public ulong CharacterId { get; init; } = CharacterId;

    internal sealed record RetainerDto(string RetainerId, string Name, DateTimeOffset? CapturedAt,
        int? UndercutCount, int? UnknownCount, ListingDto[] Listings);

    internal sealed record ListingDto(int Slot, uint ItemId, bool Hq, uint Quantity, uint UnitPrice,
        HighlightStatus PriceStatus, HighlightStatus HighlightStatus);
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static IpcSnapshot Create(ulong character, IReadOnlyList<(ulong Id, string Name)> owners,
        IEnumerable<RetainerSnapshot> snapshots, Func<RetainerSnapshot, Listing, HighlightStatus> status,
        IReadOnlySet<uint> bagItems, bool priceHighlight, bool bagHighlight)
    {
        if (character == 0) return Empty;
        var latest = snapshots.Where(x => x.CharacterId == character)
            .GroupBy(x => x.RetainerId).ToDictionary(x => x.Key, x => x.MaxBy(y => y.CapturedAt)!);
        var retainers = owners.Select(owner => Retainer(owner.Id, owner.Name, latest.GetValueOrDefault(owner.Id), status, bagItems, priceHighlight, bagHighlight)).ToArray();
        return new IpcSnapshot(character, retainers);
    }

    private static RetainerDto Retainer(ulong id, string name, RetainerSnapshot? snapshot,
        Func<RetainerSnapshot, Listing, HighlightStatus> status, IReadOnlySet<uint> bagItems, bool priceHighlight, bool bagHighlight)
    {
        if (snapshot == null) return new(id.ToString(), name, null, null, null, []);

        var listings = snapshot.Listings.Where(x => x.IsForSale).OrderBy(x => x.Slot).Select(x =>
        {
            var priceStatus = status(snapshot, x);
            var display = BagMatchRules.Display(x, priceStatus, bagItems, priceHighlight, bagHighlight);
            return new ListingDto(x.Slot, x.ItemId, x.Hq, x.Quantity, x.Price, priceStatus, display);
        }).ToArray();
        return new(id.ToString(), name, snapshot.CapturedAt,
            listings.Count(x => x.PriceStatus == HighlightStatus.Undercut),
            listings.Count(x => x.PriceStatus == HighlightStatus.Unknown), listings);
    }

}
