using KianaBH.KcpSharp;
using KianaBH.Proto;

namespace KianaBH.GameServer.Server.Packet.Send.Pjms;

public class PacketPjmsGetMainDataRsp : BasePacket
{
    public PacketPjmsGetMainDataRsp() : base(CmdIds.PjmsGetMainDataRsp)
    {
        var proto = new PjmsGetMainDataRsp
        {
            World = new() { WorldId = 400 },
            Map = new()
            {   // Fog and Teleport read from excel
                UnlockFogIdList = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 90202, 90203, 90204, 90205, 90207, 90208, 90209 },
            },
            GenderType = 1,
            WorldTime = 43200,
            Name = "Dreamseeker",
            NameCdEndTime = 14400,
            GenderCdEndTime = 14400,
            WorldTransactionStr = "0"
        };
        proto.Map.TeleportList.AddRange(new uint[] { 128, 13, 110, 136, 183, 125, 129, 153, 106, 9, 103, 124, 101, 14, 111, 134, 120, 133, 135, 151, 104, 131, 113, 16, 137, 114, 301, 10, 107, 123, 122, 118, 127, 12, 109, 15, 112, 132, 121, 130, 116, 1, 126, 102, 152, 8, 105, 108, 11, 117, 2, 115, 119 }.Select(x => new PjmsTeleport { TeleportId = x, Status = PjmsTeleport.Types.Status.PjmsTeleportStatusActive }));

        SetData(proto);
    }
}
