using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace GameDomain.Models
{
    public class Region
    {
        [BsonId]
        [BsonRepresentation(BsonType.Int64)]
        [BsonElement("RegionId")]
        public long RegionId { get; set; }

        [Required(ErrorMessage = "Region name is required")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Region name must be between 3 and 100 characters")]
        [BsonElement("Name")]
        public string Name { get; set; }

        [Required(ErrorMessage = "IP Address is required")]
        [BsonElement("Ip")]
        public string Ip { get; set; }
    }
}