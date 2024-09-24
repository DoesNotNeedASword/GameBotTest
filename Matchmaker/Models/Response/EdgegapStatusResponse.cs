﻿using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public class EdgegapStatusResponse
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; }

    [JsonPropertyName("fqdn")]
    public string Fqdn { get; set; }

    [JsonPropertyName("app_name")]
    public string AppName { get; set; }

    [JsonPropertyName("app_version")]
    public string AppVersion { get; set; }

    [JsonPropertyName("current_status")]
    public string CurrentStatus { get; set; }

    [JsonPropertyName("current_status_label")]
    public string CurrentStatusLabel { get; set; }

    [JsonPropertyName("running")]
    public bool Running { get; set; }

    [JsonPropertyName("start_time")]
    public DateTime? StartTime { get; set; }

    [JsonPropertyName("removal_time")]
    public DateTime? RemovalTime { get; set; }

    [JsonPropertyName("elapsed_time")]
    public int? ElapsedTime { get; set; }

    [JsonPropertyName("error")]
    public bool Error { get; set; }

    [JsonPropertyName("public_ip")]
    public string PublicIp { get; set; }

    [JsonPropertyName("whitelisting_active")]
    public bool WhitelistingActive { get; set; }

    [JsonPropertyName("ports")]
    public Dictionary<string, Port> Ports { get; set; }

    [JsonPropertyName("command")]
    public string Command { get; set; }

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; }

    [JsonPropertyName("last_status")]
    public string LastStatus { get; set; }

    [JsonPropertyName("last_status_label")]
    public string LastStatusLabel { get; set; }

    [JsonPropertyName("location")]
    public Location Location { get; set; }
}

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
