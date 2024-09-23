using GameBotTest.Models;

namespace GameBotTest.Handlers;

public class CommandParser
{
    public string Parse(string messageText, Context context)
    {
        if (!messageText.StartsWith("/start"))
            return messageText switch
            {
                "запустить mini app" => "startMiniApp",
                "присоединиться к комьюнити" => "joinCommunity",
                _ => "unknown"
            };
        var code = GetCodeFromStartCommand(messageText);
        if (!int.TryParse(code, out var id)) return "noRef";
        context.RefId = id;
        return $"start";

    }
    string GetCodeFromStartCommand(string startCommand)
    {
        var code = startCommand.Split(' ');
        return code.Length > 1 ? code[1] : string.Empty;
    }

}