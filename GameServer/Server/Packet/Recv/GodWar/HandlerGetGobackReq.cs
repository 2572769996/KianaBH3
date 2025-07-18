using KianaBH.GameServer.Server.Packet.Send.GodWar;
using KianaBH.Proto;

namespace KianaBH.GameServer.Server.Packet.Recv.GodWar;

[Opcode(CmdIds.GetGobackReq)]
public class HandlerGetGobackReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        await connection.SendPacket(new PacketGetGobackRsp());
    }
}
