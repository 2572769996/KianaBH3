using KianaBH.GameServer.Server.Packet.Send.Pjms;
using KianaBH.Proto;

namespace KianaBH.GameServer.Server.Packet.Recv.Pjms;

[Opcode(CmdIds.PjmsUpdateFormationReq)]
public class HandlerPjmsUpdateFormationReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = PjmsUpdateFormationReq.Parser.ParseFrom(data);
        await connection.SendPacket(new PacketPjmsUpdateFormationRsp(req));
    }
}