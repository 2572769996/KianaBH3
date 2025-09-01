using KianaBH.Data;
using KianaBH.GameServer.Server.Packet.Send.Mission;
using KianaBH.Proto;

namespace KianaBH.GameServer.Server.Packet.Recv.Mission;

[Opcode(CmdIds.GetMissionDataReq)]
public class HandlerGetMissionDataReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var player = connection.Player!;

        var missionTable = GameData.MissionData;
        var missionList = missionTable.Values.ToList();
        const int chunkSize = 3000;

        var totalChunks = (int)Math.Ceiling((double)missionList.Count / chunkSize);
        var requestCount = player.GetMissionDataRequestCount;

        while (requestCount < totalChunks - 1)
        {
            var selectedMissions = missionList
                .Skip(requestCount * chunkSize)
                .Take(chunkSize);

            var missions = selectedMissions.Select(m => new Proto.Mission
            {
                MissionId = m.Id,
                Status = MissionStatus.None,
                Priority = m.Priority,
                Progress = m.TotalProgress,
                BeginTime = 0,
                EndTime = 2073239999,
                CycleId = 1,
            }).ToList();

            await connection.SendPacket(new PacketGetMissionDataRsp(missions,false));

            requestCount++;
            player.GetMissionDataRequestCount = requestCount;
        }

        // Last chunk
        var lastMissions = missionList
            .Skip(requestCount * chunkSize)
            .Take(chunkSize);

        var lastMissionList = lastMissions.Select(m => new Proto.Mission
        {
            MissionId = m.Id,
            Status = MissionStatus.None,
            Priority = m.Priority,
            Progress = m.TotalProgress,
            BeginTime = 0,
            EndTime = 2073239999,
            CycleId = 1,
        }).ToList();

        player.GetMissionDataRequestCount = requestCount + 1;

        await connection.SendPacket(new PacketGetMissionDataRsp(lastMissionList,true));
    }
}
