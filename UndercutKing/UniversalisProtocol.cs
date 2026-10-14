using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace UndercutKing;

public static class UniversalisProtocol
{
    public static byte[] Subscribe(uint world, string change) => new BsonDocument
    {
        { "event", "subscribe" }, { "channel", $"listings/{change}{{world={world}}}" },
    }.ToBson();

    public static (uint World, uint Item)? ReadChange(byte[] bytes)
    {
        var message = BsonSerializer.Deserialize<BsonDocument>(bytes);
        if (!message.TryGetValue("event", out var kind) || !kind.IsString || kind.AsString is not ("listings/add" or "listings/remove")) return null;
        if (!message.TryGetValue("world", out var world) || !message.TryGetValue("item", out var item)) return null;
        var w = world.ToInt64(); var i = item.ToInt64();
        return w is > 0 and <= uint.MaxValue && i is > 0 and <= uint.MaxValue ? ((uint)w, (uint)i) : null;
    }

    public static MarketQuote ParseQuote(JsonElement root, uint item, IReadOnlySet<string> owners, DateTimeOffset now)
    {
        if (!root.TryGetProperty("itemID", out var id) || id.GetUInt32() != item || !root.TryGetProperty("listings", out var listings) || listings.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("市场响应缺少完整物品数据");
        uint? nq = null, hq = null;
        foreach (var listing in listings.EnumerateArray())
        {
            if (listing.TryGetProperty("retainerName", out var name) && owners.Contains(name.GetString() ?? "")) continue;
            var price = listing.GetProperty("pricePerUnit").GetUInt32();
            if (price == 0) continue;
            if (listing.GetProperty("hq").GetBoolean()) hq = hq == null ? price : Math.Min(hq.Value, price);
            else nq = nq == null ? price : Math.Min(nq.Value, price);
        }
        var uploaded = root.TryGetProperty("lastUploadTime", out var time) ? DateTimeOffset.FromUnixTimeMilliseconds(time.GetInt64()) : DateTimeOffset.MinValue;
        if (uploaded > now.AddMinutes(5)) throw new InvalidDataException("市场数据时间异常");
        return new(nq, hq, uploaded, "Universalis", listings.GetArrayLength() < 100);
    }
}
