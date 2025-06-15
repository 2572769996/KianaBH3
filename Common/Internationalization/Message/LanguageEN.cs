﻿namespace KianaBH.Internationalization.Message;

#region Root

public class LanguageEN
{
    public GameTextEN Game { get; } = new();
    public ServerTextEN Server { get; } = new();
    public WordTextEN Word { get; } = new(); // a placeholder for the actual word text
}

#endregion

#region Layer 1

/// <summary>
///     path: Game
/// </summary>
public class GameTextEN
{
    public CommandTextEN Command { get; } = new();
}

/// <summary>
///     path: Server
/// </summary>
public class ServerTextEN
{
    public WebTextEN Web { get; } = new();
    public ServerInfoTextEN ServerInfo { get; } = new();
}

/// <summary>
///     path: Word
/// </summary>
public class WordTextEN
{
    public string Star => "Star";
    public string Valk => "Valkyrie";
    public string Material => "Material";
    public string Stigmata => "Stigmata";
    public string Weapon => "Weapon";
    public string Banner => "Gacha";
    public string Activity => "Activity";

    // server info
    public string Config => "Config File";
    public string Language => "Language";
    public string Log => "Log";
    public string GameData => "Game Data";
    public string Cache => "Resource Cache";
    public string CustomData => "Custom Data";
    public string Database => "Database";
    public string Command => "Command";
    public string SdkServer => "Web Server";
    public string Handler => "Packet Handler";
    public string Dispatch => "Global Dispatch";
    public string Game => "Game";
    public string Handbook => "Handbook";
    public string NotFound => "Not Found";
    public string Error => "Error";
    public string DatabaseAccount => "Database Account";
    public string Tutorial => "Tutorial";
}

#endregion

#region Layer 2

#region GameText

/// <summary>
///     path: Game.Command
/// </summary>
public class CommandTextEN
{
    public NoticeTextEN Notice { get; } = new();
    public HelpTextEN Help { get; } = new();
    public ValkTextEN Valk { get; } = new();
    public GiveAllTextEN GiveAll { get; } = new();
}

#endregion

#region ServerTextEN

/// <summary>
///     path: Server.Web
/// </summary>
public class WebTextEN
{
    public string Maintain => "The server is undergoing maintenance, please try again later.";
}

/// <summary>
///     path: Server.ServerInfo
/// </summary>
public class ServerInfoTextEN
{
    public string Shutdown => "Shutting down...";
    public string CancelKeyPressed => "Cancel key pressed (Ctrl + C), server shutting down...";
    public string StartingServer => "Starting KianaBH";
    public string CurrentVersion => "Server supported versions: {0}";
    public string InvalidVersion => "Unsupported game version {0}\nPlease update game to {1}";
    public string LoadingItem => "Loading {0}...";
    public string GeneratingItem => "Building {0}...";
    public string WaitingItem => "Waiting for process {0} to complete...";
    public string RegisterItem => "Registered {0} {1}(s).";
    public string FailedToLoadItem => "Failed to load {0}.";
    public string NewClientSecretKey => "Client Secret Key does not exist and a new Client Secret Key is being generated.";
    public string FailedToInitializeItem => "Failed to initialize {0}.";
    public string FailedToReadItem => "Failed to read {0}, file {1}";
    public string GeneratedItem => "Generated {0}.";
    public string LoadedItem => "Loaded {0}.";
    public string LoadedItems => "Loaded {0} {1}(s).";
    public string ServerRunning => "{0} server listening on {1}";

    public string ServerStarted =>
        "Startup complete! Took {0}s, better than 99% of users. Type 'help' for command help"; // This is a meme, consider localpermissiong in English

    public string MissionEnabled =>
        "Mission system enabled. This feature is still in development and may not work as expected. Please report any bugs to the developers.";
    public string KeyStoreError => "The SSL certificate does not exist, SSL functionality has been disabled.";
    public string CacheLoadSkip => "Skipped cache loading.";

    public string ConfigMissing => "{0} is missing. Please check your resource folder: {1}, {2} may not be available.";
    public string UnloadedItems => "Unloaded all {0}.";
    public string SaveDatabase => "Database saved in {0}s";

    public string WaitForAllDone =>
        "You cannot enter the game yet. Please wait for all items to load before trying again";

    public string UnhandledException => "An unhandled exception occurred: {0}";
}

#endregion

#endregion

#region Layer 3

#region CommandText

/// <summary>
///     path: Game.Command.Notice
/// </summary>
public class NoticeTextEN
{
    public string PlayerNotFound => "Player not found!";
    public string InvalidArguments => "Invalid arguments!";
    public string NoPermission => "You don't have permission!";
    public string CommandNotFound => "Command not found! Type '/help' for assistance";
    public string TargetOffline => "Target {0}({1}) is offline! Clearing current target";
    public string TargetFound => "Target {0}({1}) found. Next command will default to this target";
    public string TargetNotFound => "Target {0} not found!";
    public string InternalError => "Internal error occurred while processing command!";
}

/// <summary>
///     path: Game.Command.Help
/// </summary>
public class HelpTextEN
{
    public string Desc => "Show help information";
    public string Usage =>
        "Usage: /help\n" +
        "Usage: /help [cmd]";
    public string Commands => "Commands: ";
    public string CommandUsage => "Usage: ";
    public string CommandPermission => "Level Permission For Access: ";
    public string CommandAlias => "Command Alias：";
}

/// <summary>
///     path: Game.Command.Valk
/// </summary>
public class ValkTextEN
{
    public string Desc => "Set attributes for owned characters\n" +
                          "Note: -1 means all owned characters\n";

    public string Usage =>
        "Usage: /valk add [ValkID/-1] l<Level> s<Star>\n\n" +
        "Usage: /valk level [ValkID/-1] [Level]\n\n" +
        "Usage: /valk star [ValkID/-1] [Star]\n\n" +
        "Usage: /valk skill [ValkID/-1] for max skill level";

    public string ValkNotFound => "Character does not exist!";
    public string ValkAddedAll => "Granted all characters to player!";
    public string ValkAdded => "Granted character {0} to player!";
    public string ValkSetLevelAll => "Set all characters to level {0}!";
    public string ValkSetLevel => "Set character {0} to level {1}!";
    public string ValkSetStarAll => "Set all characters' Resonance to {0}!";
    public string ValkSetStar => "Set character {0}'s Resonance to {1}!";
    public string ValkSetSkillLevelAll => "Set all characters' skill levels to max!";
    public string ValkSetSkillLevel => "Set character {0}'s skill levels to max!";
}

/// <summary>
///     path: Game.Command.GiveAll
/// </summary>
public class GiveAllTextEN
{
    public string Desc => "Give all items of specified type\n" +
                          "weapon,stigmata";

    public string Usage =>
        "Usage: /giveall weapon\n\n" +
        "Usage: /giveall stigmata";

    public string GiveAllItems => "Granted all {0}";
}

#endregion

#endregion