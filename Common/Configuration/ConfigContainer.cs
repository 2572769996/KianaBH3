﻿namespace KianaBH.Configuration;

public class ConfigContainer
{
    public HttpServerConfig HttpServer { get; set; } = new();
    public GameServerConfig GameServer { get; set; } = new();
    public PathConfig Path { get; set; } = new();
    public ServerOption ServerOption { get; set; } = new();
}

public class HttpServerConfig
{
    public string BindAddress { get; set; } = "0.0.0.0";
    public string PublicAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 80;

    public string GetDisplayAddress()
    {
        return "http" + "://" + PublicAddress + ":" + Port;
    }

    public string GetBindDisplayAddress()
    {
        return "http" + "://" + BindAddress + ":" + Port;
    }
}

public class GameServerConfig
{
    public string BindAddress { get; set; } = "0.0.0.0";
    public string PublicAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 21000;
    public int KcpAliveMs { get; set; } = 45000;
    public string DatabaseName { get; set; } = "kiana.db";
    public string GameServerId { get; set; } = "KianaBH";
    public string GameServerName { get; set; } = "KianaBH";
    public string GetDisplayAddress()
    {
        return PublicAddress + ":" + Port;
    }
}

public class PathConfig
{
    public string ResourcePath { get; set; } = "Resources";
    public string ConfigPath { get; set; } = "Config";
    public string DatabasePath { get; set; } = "Config/Database";
    public string HandbookPath { get; set; } = "Config/Handbook";
    public string LogPath { get; set; } = "Config/Logs";
    public string DataPath { get; set; } = "Config/Data";
}

public class ServerOption
{
    public string Language { get; set; } = "EN";
    public string FallbackLanguage { get; set; } = "EN";
    public string[] DefaultPermissions { get; set; } = ["Admin"];
    public ServerProfile ServerProfile { get; set; } = new();
    public bool AutoCreateUser { get; set; } = true;
    public bool SavePersonalDebugFile { get; set; } = false;
    public bool AutoSendResponseWhenNoHandler { get; set; } = true;
#if DEBUG
    public bool EnableDebug { get; set; } = true;
#else
    public bool EnableDebug { get; set; } = false;
#endif
    public bool DebugMessage { get; set; } = true;
    public bool DebugDetailMessage { get; set; } = true;
    public bool DebugNoHandlerPacket { get; set; } = true;
}

public class ServerProfile
{
    public string Name { get; set; } = "Server";
    public int Uid { get; set; } = 80;
    public string Signature { get; set; } = "Type /help for a list of commands";
    public int Level { get; set; } = 1;
    public int HeadIcon { get; set; } = 200105;
    public int ChatBubbleId { get; set; } = 220001;
    public int DisplayAvatarId { get; set; } = 1001;
    public int DisplayAvatarLevel { get; set; } = 1;
}