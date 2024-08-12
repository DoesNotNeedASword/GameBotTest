using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameDomain.Models;

public class Player
{
    [BsonId]
    [BsonRepresentation(BsonType.Int64)]
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
    public long ReferrerId { get; set; } 
}
