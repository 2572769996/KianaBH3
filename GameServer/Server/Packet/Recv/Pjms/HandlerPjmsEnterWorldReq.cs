using KianaBH.GameServer.Server.Packet.Send.Pjms;
using KianaBH.Proto;

namespace KianaBH.GameServer.Server.Packet.Recv.Pjms;

[Opcode(CmdIds.PjmsEnterWorldReq)]
public class HandlerPjmsEnterWorldReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = PjmsEnterWorldReq.Parser.ParseFrom(data);
        await connection.SendPacket(new PacketPjmsEnterWorldRsp(req.WorldId, req.TeleportId));
    }
}