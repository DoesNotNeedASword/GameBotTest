using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameAPI.Models;

public class Player
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    
    [BsonElement("TelegramId")]
    public long TelegramId { get; set; }
    
    [BsonElement("Name")]
    public string Name { get; set; } 
    
    [BsonElement("Level")]
    public int Level { get; set; }  
    
    [BsonElement("Score")]
    public int Score { get; set; }
    
    [BsonElement("Rating")]
    public int Rating { get; set; } = 0;

    [BsonElement("SoftCurrency")]
    public int SoftCurrency { get; set; } = 0;
    
    [BsonElement("HardCurrency")]
    public int HardCurrency { get; set; } = 0;

    [BsonElement("ReferrerId")]
    public string ReferrerId { get; set; } 
}
