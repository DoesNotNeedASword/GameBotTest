using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameDomain.Models;

public class Player
{
    [BsonId]
    [BsonRepresentation(BsonType.Int64)]
    [BsonElement("TelegramId")]
    public long TelegramId { get; set; } 

    [Required(ErrorMessage = "Name is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Name must be between 3 and 50 characters")]
    [BsonElement("Name")]
    public string Name { get; set; }

    [Range(1, 100, ErrorMessage = "Level must be between 1 and 100")]
    [BsonElement("Level")]
    public int Level { get; set; }  

    [Range(0, int.MaxValue, ErrorMessage = "Score must be a positive number")]
    [BsonElement("Score")]
    public int Score { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Rating must be a non-negative number")]
    [BsonElement("Rating")]
    public int Rating { get; set; } = 0;

    [Range(0, int.MaxValue, ErrorMessage = "SoftCurrency must be a non-negative number")]
    [BsonRepresentation(BsonType.Int64)]
    [BsonElement("SoftCurrency")]
    public long SoftCurrency { get; set; } = 0;

    [Range(0, int.MaxValue, ErrorMessage = "HardCurrency must be a non-negative number")]
    [BsonRepresentation(BsonType.Int64)]
    [BsonElement("HardCurrency")]
    public long HardCurrency { get; set; } = 0;

    [Range(1, long.MaxValue, ErrorMessage = "ReferrerId must be a valid number greater than 0")]
    [BsonElement("ReferrerId")]
    public long ReferrerId { get; set; }

    [BsonRepresentation(BsonType.Int64)]
    [BsonElement("RegionId")]
    public long RegionId { get; set; } = 1; 
    
    [BsonElement("CurrentEnergy")]
    public int CurrentEnergy { get; set; } = 100;

    [BsonElement("MaxEnergy")]
    public int MaxEnergy { get; set; } = 100;
    
    [BsonElement("AvatarId")]
    public int AvatarId { get; set; }

    [BsonElement("FrameId")]
    public int FrameId { get; set; }

    [BsonElement("TitleId")]
    public int TitleId { get; set; }

    [BsonElement("FavoritePhraseId")]
    public int PhraseId { get; set; }
    [BsonElement("Statistics")]
    public BsonDocument Statistics { get; set; } = new BsonDocument();
    
    [BsonElement("CompletedQuests")]
    public List<string> CompletedQuests { get; set; } = []; 
    
    [BsonElement("LastLoginDate")]
    public DateTime LastLoginDate { get; set; } = DateTime.MinValue;

    [BsonElement("LoginStreak")]
    public int LoginStreak { get; set; } = 0;

    [BsonElement("MaxLoginStreak")]
    public int MaxLoginStreak { get; set; } = 0; 

    [BsonElement("MissedDayCompensation")]
    public bool MissedDayCompensation { get; set; } = false;
}