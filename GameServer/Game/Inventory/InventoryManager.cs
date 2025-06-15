﻿using KianaBH.Data;
using KianaBH.Database;
using KianaBH.Database.Inventory;
using KianaBH.Enums.Item;
using KianaBH.GameServer.Game.Player;
using KianaBH.GameServer.Server.Packet.Send.Item;
using KianaBH.Util;

namespace KianaBH.GameServer.Game.Inventory;

public class InventoryManager(PlayerInstance player) : BasePlayerManager(player)
{
    public InventoryData Data = DatabaseHelper.GetInstanceOrCreateNew<InventoryData>(player.Uid);

    public async ValueTask<ItemData?> AddItem(int itemId, int count, ItemMainTypeEnum type, int level = 1, int equipAvatar = 0, bool sync = true)
    {
        ItemData? itemData = null;

        switch (type)
        {
            case ItemMainTypeEnum.Material:
                GameData.MaterialData.TryGetValue(itemId, out var materialConfig);
                if (materialConfig == null) return null;
                itemData = await PutItem(itemId, count, type);
                break;
            case ItemMainTypeEnum.Weapon:
                GameData.WeaponData.TryGetValue(itemId, out var weaponConfig);
                if (weaponConfig == null) return null;
                itemData = await PutItem(itemId, 1, type, level, equipAvatar: equipAvatar, uniqueId: ++Data.NextUniqueId);
                break;
            case ItemMainTypeEnum.Stigmata:
                GameData.StigmataData.TryGetValue(itemId, out var stigmataConfig);
                itemData = await PutItem(itemId, 1, type, level, uniqueId: ++Data.NextUniqueId);
                break;
            default:
                break;
        }

        if (sync) await Player.SendPacket(new PacketGetEquipmentDataRsp(Player));

        return itemData;
    }

    public async ValueTask<ItemData> PutItem(int itemId, int count, ItemMainTypeEnum type, int level = 0,
        int exp = 0, int equipAvatar = 0, int uniqueId = 0)
    {
        var item = new ItemData
        {
            ItemId = itemId,
            Count = count,
            Level = level,
            Exp = exp,
            EquipAvatar = equipAvatar,
        };

        if (uniqueId > 0) item.UniqueId = uniqueId;

        switch (type)
        {
            case ItemMainTypeEnum.Material:
                var oldItem = Data.MaterialItems.Find(x => x.ItemId == itemId);
                if (oldItem != null)
                {
                    oldItem.Count += count;
                    item = oldItem;
                    break;
                }
                Data.MaterialItems.Add(item);
                break;
            case ItemMainTypeEnum.Weapon:
                if (Data.WeaponItems.Count + 1 > GameConstants.INVENTORY_MAX_EQUIPMENT)
                {
                    return item;
                }
                Data.WeaponItems.Add(item);
                break;
            case ItemMainTypeEnum.Stigmata:
                if (Data.StigmataItems.Count + 1 > GameConstants.INVENTORY_MAX_EQUIPMENT)
                {
                    return item;
                }
                Data.StigmataItems.Add(item);
                break;
        }

        return item;
    }

    public async ValueTask EquipAvatar(int avatarId, int uniqueId)
    {
        var itemData = Data.WeaponItems.Find(x => x.UniqueId == uniqueId);
        var avatarData = Player.AvatarManager!.GetAvatar(avatarId);
        if (itemData == null || avatarData == null) return;
        var oldItem = Data.WeaponItems.Find(x => x.UniqueId == avatarData.WeaponUniqueId);
        if (itemData.EquipAvatar > 0) // already be dressed
        {
            var equipAvatarId = itemData.EquipAvatar;
            var equipAvatar = Player.AvatarManager.GetAvatar(equipAvatarId);
            if (equipAvatar != null && oldItem != null)
            {
                // switch
                equipAvatar.WeaponUniqueId = oldItem.UniqueId;
                oldItem.EquipAvatar = equipAvatar.AvatarId;
            }
            else if (equipAvatar != null && oldItem == null)
            {
                equipAvatar.WeaponUniqueId = 0;
            }
        }
        else
        {
            if (oldItem != null)
            {
                oldItem.EquipAvatar = 0;
            }
        }

        itemData.EquipAvatar = avatarData.AvatarId;
        avatarData.WeaponUniqueId = itemData.UniqueId;
        await Player.SyncValk();
    }

    public async ValueTask ExchangeAvatar(int avatarId1, int avatarId2)
    {
        var avatarData1 = Player.AvatarManager!.GetAvatar(avatarId1);
        var avatarData2 = Player.AvatarManager!.GetAvatar(avatarId2);
        if (avatarData1 == null || avatarData2 == null) return;

        var item1 = Data.WeaponItems.Find(x => x.UniqueId == avatarData1.WeaponUniqueId);
        var item2 = Data.WeaponItems.Find(x => x.UniqueId == avatarData2.WeaponUniqueId);
        if (item1 == null || item2 == null) return;

        var tempWeaponId = avatarData1.WeaponUniqueId;

        avatarData1.WeaponUniqueId = avatarData2.WeaponUniqueId;
        avatarData2.WeaponUniqueId = tempWeaponId;

        item1.EquipAvatar = avatarData2.AvatarId;
        item2.EquipAvatar = avatarData1.AvatarId;

        await Player.SyncValk();
    }

    public async ValueTask EquipStigmata(int avatarId, int uniqueId, int slot)
    {
        var itemData = Data.StigmataItems.Find(x => x.UniqueId == uniqueId);
        var avatarData = Player.AvatarManager!.GetAvatar(avatarId);
        if (itemData == null || avatarData == null) return;
        avatarData.Stigmata.TryGetValue(slot, out var id);
        var oldItem = Data.StigmataItems.Find(x => x.UniqueId == id);

        if (itemData.EquipAvatar > 0) // already be dressed
        {
            var equipAvatarId = itemData.EquipAvatar;
            var equipAvatar = Player.AvatarManager!.GetAvatar(equipAvatarId);
            if (equipAvatar != null && oldItem != null)
            {
                // switch
                equipAvatar.Stigmata[slot] = oldItem.UniqueId;
                oldItem.EquipAvatar = equipAvatar.AvatarId;
            }
            else if (equipAvatar != null && oldItem == null)
            {
                equipAvatar.Stigmata[slot] = 0;
            }
        }
        else
        {
            if (oldItem != null)
            {
                oldItem.EquipAvatar = 0;
            }
        }

        itemData.EquipAvatar = avatarData.AvatarId;
        avatarData.Stigmata[slot] = itemData.UniqueId;
        await Player.SyncValk();
    }

    public async ValueTask ExchangeStigmata(int avatarId1, int avatarId2, int slot)
    {
        var avatarData1 = Player.AvatarManager!.GetAvatar(avatarId1);
        var avatarData2 = Player.AvatarManager!.GetAvatar(avatarId2);
        if (avatarData1 == null || avatarData2 == null) return;

        var has1 = avatarData1.Stigmata.TryGetValue(slot, out var id1);
        var has2 = avatarData2.Stigmata.TryGetValue(slot, out var id2);

        var item1 = Data.StigmataItems.Find(x => x.UniqueId == id1);
        var item2 = Data.StigmataItems.Find(x => x.UniqueId == id2);
        if (item1 == null || item2 == null) return;

        var tempStigmataId = avatarData1.Stigmata[slot];

        avatarData1.Stigmata[slot] = avatarData2.Stigmata[slot];
        avatarData2.Stigmata[slot] = tempStigmataId;

        item1.EquipAvatar = avatarData2.AvatarId;
        item2.EquipAvatar = avatarData1.AvatarId;

        await Player.SyncValk();
    }
}