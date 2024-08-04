using GameAPI.Services;
using GameDomain.Interfaces;
using GameDomain.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Tests;

public class PlayerServiceTests
{
    private readonly Mock<ICacheService> _mockCacheService; 
    private readonly PlayerService _service;

    public PlayerServiceTests()
    {
        Mock<IMongoDatabase> mockDatabase = new();
        Mock<IMongoCollection<Player>> mockCollection = new();
        _mockCacheService = new Mock<ICacheService>();
        var mockCursor = new Mock<IAsyncCursor<Player>>();

        mockDatabase.Setup(m => m.GetCollection<Player>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
            .Returns(mockCollection.Object);

        mockCollection.Setup(m => m.FindAsync(It.IsAny<FilterDefinition<Player>>(),
                It.IsAny<FindOptions<Player, Player>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        // Настройка курсора для возврата списка игроков
        mockCursor.SetupSequence(m => m.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        mockCursor.Setup(m => m.Current)
            .Returns(new List<Player> { new Player { TelegramId = 1 }, new Player { TelegramId = 2 } });

        _service = new PlayerService(mockDatabase.Object, new LevelService());
    }

    [Fact]
    public async Task GetAsync_ReturnsAllPlayers()
    {
        var players = await _service.GetAsync();
        Assert.Equal(2, players.Count); 
    }
    
}
