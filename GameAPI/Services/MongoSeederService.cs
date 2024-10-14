using GameDomain.Models;
using MongoDB.Driver;

namespace GameAPI.Services;

public static class MongoDbSeederSevice
{
    public static async Task SeedAsync(IMongoDatabase database)
    {
        var regionsCollection = database.GetCollection<Region>("Regions");

        // Проверяем, есть ли записи в коллекции Regions
        var existingRegion = await regionsCollection.Find(_ => true).AnyAsync();

        if (!existingRegion)
        {
            // Если записей нет, добавляем стандартные регионы
            var defaultRegions = new List<Region>
            {
                new Region { RegionId = 1, Name = "Europe", Ip = "192.168.1.1" },
                new Region { RegionId = 2, Name = "Asia", Ip = "192.168.1.2" },
                new Region { RegionId = 3, Name = "North America", Ip = "192.168.1.3" }
            };

            await regionsCollection.InsertManyAsync(defaultRegions);
        }
        else
        {
            Console.WriteLine("Regions already exist in the database.");
        }
    }
}
