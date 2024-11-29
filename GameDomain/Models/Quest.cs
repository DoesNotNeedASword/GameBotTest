using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameDomain.Models
{
    public class Quest
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("Name")]
        public string Name { get; set; }

        [BsonElement("Description")]
        public string Description { get; set; }

        [BsonElement("Reward")]
        public int Reward { get; set; } 

        [BsonElement("RequirementKey")]
        public string RequirementKey { get; set; } // Ключ для статистики (например, "km")

        [BsonElement("RequirementValue")]
        public int RequirementValue { get; set; } 
    }
}