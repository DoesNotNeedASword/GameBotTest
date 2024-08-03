using GameDomain.Interfaces;
using GameDomain.Models;

namespace GameAPI.Services;

public class LevelService : ILevelService
{
    private readonly List<LevelThreshold> _levelThresholds =
    [
        new LevelThreshold { Level = 1, Threshold = 100 },
        new LevelThreshold { Level = 2, Threshold = 500 }
    ];

    public int CheckLevel(int rating)
    {
        var level = _levelThresholds.LastOrDefault(l => rating >= l.Threshold)?.Level ?? 1;
        return level;
    }
}


