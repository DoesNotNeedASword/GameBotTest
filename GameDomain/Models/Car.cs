using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameDomain.Models;

public class Car
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("PlayerId")]
    public long PlayerId { get; set; } 

    [BsonElement("WheelId")]
    public int WheelId { get; set; }

    [BsonElement("SpoilerId")]
    public int SpoilerId { get; set; }

    [BsonElement("ColorId")]
    public int ColorId { get; set; }
}