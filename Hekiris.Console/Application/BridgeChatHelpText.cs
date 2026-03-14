namespace Hekiris.Application;

public static class BridgeChatHelpText
{
    public static string GetText()
    {
        return """
/help => shows the available Hekiris commands
/stop => stops Hekiris
/ss => shows the current Hekiris status
/sc => shows the available commands
/cNs => stops running command N (see /ss)

Configured commands can be run with /c1, /c2, /c3, and so on.
""";
    }
}
