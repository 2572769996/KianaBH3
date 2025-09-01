using KianaBH.GameServer.Server.Packet.Send.Pjms;
using KianaBH.Proto;

namespace KianaBH.GameServer.Server.Packet.Recv.Pjms;

[Opcode(CmdIds.PjmsGetAvatarStatusReq)]
public class HandlerPjmsGetAvatarStatusReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        await connection.SendPacket(new PacketPjmsGetAvatarStatusRsp());
    }
}