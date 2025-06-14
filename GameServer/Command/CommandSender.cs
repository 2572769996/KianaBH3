﻿using KianaBH.Enums.Player;
using KianaBH.GameServer.Game.Player;
using KianaBH.Util;

namespace KianaBH.GameServer.Command;

public interface ICommandSender
{
    public ValueTask SendMsg(string msg);

    public int GetSender();
}

public class ConsoleCommandSender(Logger logger) : ICommandSender
{
    public async ValueTask SendMsg(string msg)
    {
        logger.Info(msg);
        await Task.CompletedTask;
    }

    public int GetSender()
    {
        return (int)ServerEnum.Console;
    }
}

public class PlayerCommandSender(PlayerInstance player) : ICommandSender
{
    public PlayerInstance Player = player;

    public async ValueTask SendMsg(string msg)
    {
        // TODO SEND MSG
    }

    public int GetSender()
    {
        return Player.Uid;
    }
}