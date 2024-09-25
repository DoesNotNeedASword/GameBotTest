using System.Text.Json.Serialization;

public class Location
{
    [JsonPropertyName("city")]
    public string City { get; set; }

    [JsonPropertyName("country")]
    public string Country { get; set; }

    [JsonPropertyName("continent")]
    public string Continent { get; set; }

    [JsonPropertyName("administrative_division")]
    public string AdministrativeDivision { get; set; }

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; }

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }
}