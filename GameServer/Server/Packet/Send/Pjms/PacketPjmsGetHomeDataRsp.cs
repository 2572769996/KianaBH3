using KianaBH.KcpSharp;
using KianaBH.Proto;

namespace KianaBH.GameServer.Server.Packet.Send.Pjms;

public class PacketPjmsGetHomeDataRsp : BasePacket
{
    public PacketPjmsGetHomeDataRsp() : base(CmdIds.PjmsGetHomeDataRsp)
    {
        var proto = new PjmsGetHomeDataRsp
        {
            UnlockBgmIdList = { 51, 59, 60, 61 }    // Read from excel
        };

        SetData(proto);
    }
}