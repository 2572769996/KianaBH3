using KianaBH.KcpSharp;
using KianaBH.Proto;

namespace KianaBH.GameServer.Server.Packet.Send.Pjms;

public class PacketPjmsGetChapterDataRsp : BasePacket
{
    public PacketPjmsGetChapterDataRsp() : base(CmdIds.PjmsGetChapterDataRsp)
    {
        // TODO: Hardcoded
        var proto = new PjmsGetChapterDataRsp
        {
            CurChapterId = 200,
            IsAll = true,
            ChapterList =
            {
                new PjmsChapter
                {
                    ChapterId = 100,
                    ChapterShadowlake = new ChapterShadowLake
                    {
                        EnergyNum = 2,
                        MaxEnergyNum = 5
                    },
                    CurTrackSeriesId = 1022,
                    Exp = 530,
                    Formation =
                        new PjmsFormation
                        {
                            AvatarIdList = { 150 },
                            ElfId = 4224,
                            IsElfMode = true
                        },
                    LastTakeChapterRewardLevel = 7,
                    LastTakeChapterRewardMaterialNum = 1000,
                    Level = 7,
                    PlayingBgmId = 19,
                    TalentLevel = 7,
                    UnitInfo = new PjmsUnitInfo
                    {
                        AuxiliaryUnitList =
                        {
                            new PjmsAuxiliaryUnit { Exp = 170, Level = 2, UniqueId = 1000, UnitId = 301 },
                            new PjmsAuxiliaryUnit { Level = 1, UniqueId = 101, UnitId = 301 },
                            new PjmsAuxiliaryUnit { Exp = 10, Level = 2, UniqueId = 1002, UnitId = 201 },
                            new PjmsAuxiliaryUnit { Level = 1, UniqueId = 1003, UnitId = 205 },
                            new PjmsAuxiliaryUnit { Level = 1, UniqueId = 1004, UnitId = 302 },
                            new PjmsAuxiliaryUnit { Level = 1, UniqueId = 1005, UnitId = 302 },
                            new PjmsAuxiliaryUnit { Level = 1, UniqueId = 1006, UnitId = 303 },
                            new PjmsAuxiliaryUnit { Level = 1, UniqueId = 1007, UnitId = 207 },
                            new PjmsAuxiliaryUnit { Level = 1, UniqueId = 1008, UnitId = 303 }
                        },
                        CoreUnitList =
                        {
                            new PjmsCoreUnit { Level = 1, UnitId = 1 },
                            new PjmsCoreUnit { Level = 2, UnitId = 2 },
                            new PjmsCoreUnit { Level = 1, UnitId = 3 }
                        },
                        CurUnitSetId = 1,
                        UnitSetList =
                        {
                            new PjmsUnitSet
                            {
                                SetId = 1,
                                SlotList =
                                {
                                    new PjmsUnitSetSlot { Id = 2, SlotId = 10 },
                                    new PjmsUnitSetSlot { Id = 1002, SlotId = 100 },
                                    new PjmsUnitSetSlot { Id = 1004, SlotId = 110 },
                                    new PjmsUnitSetSlot { Id = 1006, SlotId = 120 }
                                }
                            },
                            new PjmsUnitSet { SetId = 2 },
                            new PjmsUnitSet { SetId = 3 },
                            new PjmsUnitSet { SetId = 4 },
                            new PjmsUnitSet { SetId = 5 }
                        }
                    }
                },
                new PjmsChapter
                {
                    ChapterId = 200,
                    Formation = new()
                    {
                        CurAvatarId = 4223,
                        ElfId = 120,
                        StarRingEnergy = 150,
                        AvatarIdList = { 4223, 4224, 4225 }
                    },
                    UnitInfo = new()
                    {
                        CurUnitSetId = 1,
                        UnitSetList =
                        {
                            new PjmsUnitSet { SetId = 1 },
                            new PjmsUnitSet { SetId = 2 },
                            new PjmsUnitSet { SetId = 3 },
                            new PjmsUnitSet { SetId = 4 },
                            new PjmsUnitSet { SetId = 5 }
                        }
                    },
                    PlayingBgmId = 59,
                    CurSectionId = 1
                }
            }
        };

        SetData(proto);
    }
}
