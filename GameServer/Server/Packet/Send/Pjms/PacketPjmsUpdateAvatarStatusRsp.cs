using KianaBH.KcpSharp;
using KianaBH.Proto;

namespace KianaBH.GameServer.Server.Packet.Send.Pjms;

public class PacketPjmsUpdateAvatarStatusRsp : BasePacket
{
    public PacketPjmsUpdateAvatarStatusRsp(PjmsUpdateAvatarStatusReq req) : base(CmdIds.PjmsUpdateAvatarStatusRsp)
    {
        var proto = new PjmsUpdateAvatarStatusRsp
        {
            ChapterId = req.ChapterId,
            StarRingEnergy = req.StarRingEnergy
        };
        proto.AvatarStatusList.AddRange(req.AvatarStatusList);
        // TODO: Create a part2 table to save changes

        SetData(proto);
    }
}