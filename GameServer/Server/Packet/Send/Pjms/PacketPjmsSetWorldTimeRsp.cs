using KianaBH.KcpSharp;
using KianaBH.Proto;

namespace KianaBH.GameServer.Server.Packet.Send.Pjms;

public class PacketPjmsSetWorldTimeRsp : BasePacket
{
    public PacketPjmsSetWorldTimeRsp(uint TargetTime) : base(CmdIds.PjmsSetWorldTimeRsp)
    {
        var proto = new PjmsSetWorldTimeRsp
        {
            CurTime = TargetTime
        };

        SetData(proto);
    }
}