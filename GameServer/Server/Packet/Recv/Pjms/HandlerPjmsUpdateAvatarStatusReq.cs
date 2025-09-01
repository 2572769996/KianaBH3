using KianaBH.GameServer.Server.Packet.Send.Pjms;
using KianaBH.Proto;

namespace KianaBH.GameServer.Server.Packet.Recv.Pjms;

[Opcode(CmdIds.PjmsUpdateAvatarStatusReq)]
public class HandlerPjmsUpdateAvatarStatusReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = PjmsUpdateAvatarStatusReq.Parser.ParseFrom(data);
        await connection.SendPacket(new PacketPjmsUpdateAvatarStatusRsp(req));
    }
}