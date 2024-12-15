namespace GameAPI.Models;

public class EnergyStationLevelData
{
    public int Level { get; set; }
    public int MaxEnergy { get; set; } // Максимальное количество энергии
    public int UpgradeCost { get; set; } // Стоимость улучшения
    public int RefillIntervalMinutes { get; set; } // Интервал восстановления энергии в минутах
    public int MinDaysBeforeNextUpgrade { get; set; } // Время до следующей прокачки (в днях)
}
