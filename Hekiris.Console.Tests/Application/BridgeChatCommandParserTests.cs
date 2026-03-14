using Hekiris.Application;

namespace Hekiris.Tests.Application;

public sealed class BridgeChatCommandParserTests
{
    [Theory]
    [InlineData("/help", BridgeChatCommandType.Help)]
    [InlineData("/stop", BridgeChatCommandType.Stop)]
    [InlineData("/ss", BridgeChatCommandType.ShowStatus)]
    [InlineData("/sc", BridgeChatCommandType.ShowCommands)]
    [InlineData("hallo", BridgeChatCommandType.None)]
    [InlineData("/unknown", BridgeChatCommandType.Unknown)]
    public void Parse_ReturnsExpectedCommandType(string text, BridgeChatCommandType expectedType)
    {
        BridgeChatCommand result = BridgeChatCommandParser.Parse(text);

        Assert.Equal(expectedType, result.Type);
    }

    [Fact]
    public void Parse_ReturnsConfiguredCommandIndex()
    {
        BridgeChatCommand result = BridgeChatCommandParser.Parse("/c3");

        Assert.Equal(BridgeChatCommandType.RunConfiguredCommand, result.Type);
        Assert.Equal(2, result.CommandIndex);
    }

    [Fact]
    public void Parse_ReturnsConfiguredCommandStopIndex()
    {
        BridgeChatCommand result = BridgeChatCommandParser.Parse("/c2s");

        Assert.Equal(BridgeChatCommandType.StopConfiguredCommand, result.Type);
        Assert.Equal(1, result.CommandIndex);
    }

    [Fact]
    public void HelpText_ContainsBaseCommands()
    {
        string text = BridgeChatHelpText.GetText();

        Assert.Contains("/help", text, StringComparison.Ordinal);
        Assert.Contains("/stop", text, StringComparison.Ordinal);
        Assert.Contains("/ss", text, StringComparison.Ordinal);
        Assert.Contains("/sc", text, StringComparison.Ordinal);
        Assert.Contains("/cNs", text, StringComparison.Ordinal);
    }
}
