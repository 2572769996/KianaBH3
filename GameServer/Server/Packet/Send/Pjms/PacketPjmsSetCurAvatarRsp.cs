using KianaBH.KcpSharp;
using KianaBH.Proto;

namespace KianaBH.GameServer.Server.Packet.Send.Pjms;

public class PacketPjmsSetCurAvatarRsp : BasePacket
{
    public PacketPjmsSetCurAvatarRsp(uint ChapterId, uint CurAvatarId, bool IsElfMode) : base(CmdIds.PjmsSetCurAvatarRsp)
    {
        var proto = new PjmsSetCurAvatarRsp
        {
            ChapterId = ChapterId,
            CurAvatarId = CurAvatarId,
            IsElfMode = IsElfMode
        };
        // TODO: Create a part2 table to save changes

        SetData(proto);
    }
}