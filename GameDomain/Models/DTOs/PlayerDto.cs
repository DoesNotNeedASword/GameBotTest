namespace GameDomain.Models.DTOs;

public class PlayerDto(Player player)
    {
        public long TelegramId { get; set; } = player.TelegramId;
        public string Name { get; set; } = player.Name;
        public int Level { get; set; } = player.Level;
        public int Score { get; set; } = player.Score;
        public long ReferrerId { get; set; } = player.ReferrerId;
        public long RegionId { get; set; } = player.RegionId;
        public int CurrentEnergy { get; set; } = player.CurrentEnergy;
        public int MaxEnergy { get; set; } = player.MaxEnergy;
        public long SoftCurrency { get; set; } = player.SoftCurrency;
        public long HardCurrency { get; set; } = player.HardCurrency;
        public int Rating { get; set; } = player.Rating;
        public int AvatarId { get; set; } = player.AvatarId;
        public int FrameId { get; set; } = player.FrameId;
        public int TitleId { get; set; } = player.TitleId;
        public int PhraseId { get; set; } = player.PhraseId;
    }
