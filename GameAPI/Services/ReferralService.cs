namespace GameAPI.Services;

public class ReferralService(IConfiguration configuration)
{
    public string GenerateReferralLink(string playerId)
    {
        return $"{configuration["REFERRAL:LINK"]}{playerId}";
    } 
}