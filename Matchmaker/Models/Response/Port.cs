using System.Text.Json.Serialization;

public class Port
{
    [JsonPropertyName("external")]
    public int External { get; set; }

    [JsonPropertyName("internal")]
    public int Internal { get; set; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("tls_upgrade")]
    public bool TlsUpgrade { get; set; }

    [JsonPropertyName("link")]
    public string Link { get; set; }

    [JsonPropertyName("proxy")]
    public int? Proxy { get; set; } 
}