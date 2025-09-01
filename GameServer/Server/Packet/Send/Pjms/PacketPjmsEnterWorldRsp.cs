using KianaBH.KcpSharp;
using KianaBH.Proto;

namespace KianaBH.GameServer.Server.Packet.Send.Pjms;

public class PacketPjmsEnterWorldRsp : BasePacket
{
    public PacketPjmsEnterWorldRsp(uint WorldId, uint TeleportId) : base(CmdIds.PjmsEnterWorldRsp)
    {
        var proto = new PjmsEnterWorldRsp
        {
            World = new() { WorldId = WorldId },
            TeleportId = TeleportId,
            // Read from db table after creating it
            Formation = new()
            {
                CurAvatarId = 4223,
                ElfId = 120,
                StarRingEnergy = 150,
                AvatarIdList = { 4223, 4224, 4225 }
            },
            WorldTransactionStr = "0"
        };

        SetData(proto);
    }
}