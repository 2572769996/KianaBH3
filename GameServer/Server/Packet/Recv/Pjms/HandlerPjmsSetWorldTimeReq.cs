using KianaBH.GameServer.Server.Packet.Send.Pjms;
using KianaBH.Proto;

namespace KianaBH.GameServer.Server.Packet.Recv.Pjms;

[Opcode(CmdIds.PjmsSetWorldTimeReq)]
public class HandlerPjmsSetWorldTimeReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = PjmsSetWorldTimeReq.Parser.ParseFrom(data);
        await connection.SendPacket(new PacketPjmsSetWorldTimeRsp(req.TargetTime));
    }
}