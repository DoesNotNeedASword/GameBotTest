using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Text;

namespace GameAPI.Services;

public class VerificationService
{
    static byte[] Hmacsha256Hash(byte[] key, byte[] data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }

    public static bool IsValidData(NameValueCollection nameValueCollection, string key, string botToken)
    {
        var dataDict = new SortedDictionary<string, string>(
            nameValueCollection.AllKeys.ToDictionary(x => x!, x => nameValueCollection[x]!),
            StringComparer.Ordinal);
        var dataCheckString = string.Join(
            '\n', dataDict.Where(x => x.Key != "hash")
                .Select(x => $"{x.Key}={x.Value}"));
    
        var secretKey = Hmacsha256Hash(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(botToken));
        var generatedHash = Hmacsha256Hash(secretKey, Encoding.UTF8.GetBytes(dataCheckString));
        var actualHash = Convert.FromHexString(dataDict["hash"]);
    
        return actualHash.SequenceEqual(generatedHash);
    }
}