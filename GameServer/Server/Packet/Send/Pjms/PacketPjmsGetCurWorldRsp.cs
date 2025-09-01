using KianaBH.KcpSharp;
using KianaBH.Proto;

namespace KianaBH.GameServer.Server.Packet.Send.Pjms;

public class PacketPjmsGetCurWorldRsp : BasePacket
{
    public PacketPjmsGetCurWorldRsp() : base(CmdIds.PjmsGetCurWorldRsp)
    {
        var proto = new PjmsGetCurWorldRsp
        {
            World = new() { WorldId = 400 }
        };

        SetData(proto);
    }
}
