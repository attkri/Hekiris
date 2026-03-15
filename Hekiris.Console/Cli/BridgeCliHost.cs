using Hekiris.Application;
using Hekiris.Infrastructure.Configuration;
using Hekiris.Infrastructure.Logging;
using Hekiris.Infrastructure.OpenCode;
using Hekiris.Infrastructure.Telegram;

namespace Hekiris.Host.Cli;

public static class BridgeCliHost
{
    public static async Task<int> RunAsync(string[] args)
    {
        ConsoleTranscriptWriter console = new();
        BridgeApplication application = new(console);
        ParsedCommand parsedCommand;

        try
        {
            parsedCommand = CommandLineParser.Parse(args);
        }
        catch (CommandLineException exception)
        {
            console.WriteError(exception.Message);
            System.Console.WriteLine();
            System.Console.WriteLine(CommandLineParser.GetHelpText());
            return 1;
        }

        if (parsedCommand.Command == BridgeCommand.Help)
        {
            System.Console.WriteLine(CommandLineParser.GetHelpText());
            return 0;
        }

        LoadedBridgeConfiguration loadedConfiguration;

        try
        {
            loadedConfiguration = new AppConfigurationLoader().Load();
        }
        catch (ConfigurationException exception)
        {
            console.WriteError(exception.Message);
            return 1;
        }

        ConfigurationValidationResult validation = new AppConfigurationValidator().Validate(loadedConfiguration.Options);

        if (parsedCommand.Command == BridgeCommand.ConfigShow)
        {
            System.Console.WriteLine($"Source: {loadedConfiguration.ConfigPath}");
            System.Console.WriteLine(ConfigMasker.ToSanitizedJson(loadedConfiguration.Options));

            if (!validation.IsValid)
            {
                System.Console.WriteLine();
                application.WriteValidationErrors(validation);
                return 1;
            }

            return 0;
        }

        if (!validation.IsValid)
        {
            application.WriteValidationErrors(validation);
            return 1;
        }

        using TelegramBotClient telegramClient = new(loadedConfiguration.Options.Telegram);
        using OpenCodeClient openCodeClient = new(loadedConfiguration.Options.OpenCode);
        CommandTimeLoopStateStore commandTimeLoopStateStore = new();

        return parsedCommand.Command switch
        {
            BridgeCommand.Check => await application.CheckAsync(loadedConfiguration.Options, validation, telegramClient, openCodeClient, CancellationToken.None),
            BridgeCommand.Start => await application.StartAsync(loadedConfiguration.Options, telegramClient, openCodeClient, commandTimeLoopStateStore, CancellationToken.None),
            _ => 1,
        };
    }
}
