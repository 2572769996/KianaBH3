using KianaBH.GameServer.Server.Packet.Send.Pjms;
using KianaBH.Proto;

namespace KianaBH.GameServer.Server.Packet.Recv.Pjms;

[Opcode(CmdIds.PjmsSetCurAvatarReq)]
public class HandlerPjmsSetCurAvatarReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = PjmsSetCurAvatarReq.Parser.ParseFrom(data);
        await connection.SendPacket(new PacketPjmsSetCurAvatarRsp(req.ChapterId, req.CurAvatarId, req.IsElfMode));
    }
}