using GameAPI.Services;
using GameDomain.Models;
using MongoDB.Driver;
using Moq;

namespace Tests;

public class PlayerServiceTests
{
    private readonly Mock<IFindFluent<Player, Player>> _mockFindFluent;
    private readonly PlayerService _service;

    public PlayerServiceTests()
    {
        Mock<IMongoDatabase> mockDatabase = new();
        Mock<IMongoCollection<Player>> mockCollection = new();
        _mockFindFluent = new Mock<IFindFluent<Player, Player>>();
        Mock<IAsyncCursor<Player>> mockCursor = new();

        // Настройка мока базы данных для возврата мокированной коллекции
        mockDatabase.Setup(m => m.GetCollection<Player>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
            .Returns(mockCollection.Object);

        _service = new PlayerService(mockDatabase.Object, new LevelService()); 

        // Настройка мока коллекции
        mockCollection.Setup(m => m.FindAsync(It.IsAny<FilterDefinition<Player>>(), It.IsAny<FindOptions<Player, Player>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        // Настройка курсора
        mockCursor.SetupSequence(m => m.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        mockCursor.Setup(m => m.Current)
            .Returns(new List<Player> { new Player { TelegramId = 1 }, new Player { TelegramId = 2 } });
    }

    [Fact]
    public async Task GetAsync_ReturnsAllPlayers()
    {
        var players = await _service.GetAsync();
        Assert.Equal(2, players.Count);
    }
    
    [Fact]
    public async Task CreateAsync_ThrowsException_WhenPlayerExists()
    {
        var mockCursor = new Mock<IAsyncCursor<Player>>();
        mockCursor.SetupSequence(m => m.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true) 
            .ReturnsAsync(false); 
        mockCursor.Setup(m => m.Current)
            .Returns(new List<Player> { new Player { TelegramId = 123 } }); 
        
        var mockCollection = new Mock<IMongoCollection<Player>>();
        mockCollection.Setup(m => m.FindAsync(It.IsAny<FilterDefinition<Player>>(),
                It.IsAny<FindOptions<Player, Player>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        var mockDatabase = new Mock<IMongoDatabase>();
        mockDatabase.Setup(m => m.GetCollection<Player>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
            .Returns(mockCollection.Object);
        var service = new PlayerService(mockDatabase.Object, new LevelService());

        await Assert.ThrowsAsync<Exception>(() => service.CreateAsync(new Player { TelegramId = 123 }));
    }
}