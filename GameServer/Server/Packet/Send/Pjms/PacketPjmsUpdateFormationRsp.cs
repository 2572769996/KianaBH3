using KianaBH.KcpSharp;
using KianaBH.Proto;

namespace KianaBH.GameServer.Server.Packet.Send.Pjms;

public class PacketPjmsUpdateFormationRsp : BasePacket
{
    public PacketPjmsUpdateFormationRsp(PjmsUpdateFormationReq req) : base(CmdIds.PjmsUpdateFormationRsp)
    {
        var proto = new PjmsUpdateFormationRsp
        {
            ChapterId = req.ChapterId,
            Formation = new()
            {
                CurAvatarId = req.AvatarList[0],
                ElfId = req.ElfId,
                StarRingEnergy = 150
            }
        };
        proto.Formation.AvatarIdList.AddRange(req.AvatarList);
        // TODO: Create a part2 table to save changes

        SetData(proto);
    }
}