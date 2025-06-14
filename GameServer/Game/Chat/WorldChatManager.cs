﻿using KianaBH.GameServer.Command;
using KianaBH.GameServer.Game.Player;
using KianaBH.GameServer.Server.Packet.Send.Chat;
using KianaBH.Proto;
using KianaBH.Util.Extensions;

namespace KianaBH.GameServer.Game.Battle;

public class WorldChatManager(PlayerInstance player) : BasePlayerManager(player)
{
    public List<ChatMsg> Chats { get; set; } = [];

    public async ValueTask SendMessage(string message, ChatMsg chatMsg)
    {
        chatMsg.Uid = (uint)Player.Data.Uid;
        chatMsg.Nickname = Player.Data.Name;
        chatMsg.Time = (uint)Extensions.GetUnixSec();
        chatMsg.AvatarId = (uint)Player.Data.AssistantAvatarId;
        chatMsg.DressId = (uint)Player.AvatarManager!.AvatarData.Avatars.Where(avatar => avatar.AvatarId == Player.Data.AssistantAvatarId).First().DressId;
        chatMsg.FrameId = (uint)Player.Data.HeadFrame;
        chatMsg.CustomHeadId = (uint)Player.Data.HeadIcon;

        await Player.SendPacket(new PacketRecvChatMsgNotify(chatMsg));

        if (message.StartsWith('/') == true)
        {
            var cmd = message[1..];
            CommandExecutor.ExecuteCommand(new PlayerCommandSender(Player), cmd);
        } 
    }
}