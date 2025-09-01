using KianaBH.KcpSharp;
using KianaBH.Proto;

namespace KianaBH.GameServer.Server.Packet.Send.Pjms;

public class PacketPjmsGetAvatarStatusRsp : BasePacket
{
    public PacketPjmsGetAvatarStatusRsp() : base(CmdIds.PjmsGetAvatarStatusRsp)
    {
        var proto = new PjmsGetAvatarStatusRsp
        {

        };

        SetData(proto);
    }
}