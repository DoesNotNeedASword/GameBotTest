namespace GameAPI.Options;

public class RedisOptions : IRedisOptions
{
    public string ConnectionString { get; set; }
}

public interface IRedisOptions
{
    public string ConnectionString { get; set; }
}