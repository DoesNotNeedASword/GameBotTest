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

    [BsonElement("MaxEnergy")]
    public int MaxEnergy { get; set; } = 5;

    [BsonElement("CurrentEnergy")]
    public int CurrentEnergy { get; set; } = 5;

    [BsonElement("RefillIntervalMinutes")]
    public int RefillIntervalMinutes { get; set; } = 120;

    [BsonElement("Level")]
    public int Level { get; set; } = 0;

    [BsonElement("UpgradeCost")]
    public int UpgradeCost { get; set; } = 5000;

    [BsonElement("LastRefillTime")]
    public DateTime LastRefillTime { get; set; } = DateTime.UtcNow;

    [BsonElement("UpgradeStartTime")]
    public DateTime? UpgradeStartTime { get; set; }

    [BsonElement("UpgradeDurationMinutes")]
    public int UpgradeDurationMinutes { get; set; } = 240;

    [BsonElement("NextUpgradeAvailableDate")]
    public DateTime NextUpgradeAvailableDate { get; set; } = DateTime.UtcNow; // Дата, когда можно начать следующий апгрейд
}