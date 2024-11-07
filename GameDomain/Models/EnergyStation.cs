using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameDomain.Models;

public class EnergyStation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("PlayerId")]
    public long PlayerId { get; set; }

    [BsonElement("RefillRate")]
    public int RefillRate { get; set; } = 10;  

    [BsonElement("RefillIntervalMinutes")]
    public int RefillIntervalMinutes { get; set; } = 120;

    [BsonElement("Level")] public int Level { get; set; } = 1;

    [BsonElement("UpgradeCost")] public int UpgradeCost { get; set; } = 100;
    
    [BsonElement("LastRefillTime")]
    public DateTime LastRefillTime { get; set; } = DateTime.UtcNow;
}
