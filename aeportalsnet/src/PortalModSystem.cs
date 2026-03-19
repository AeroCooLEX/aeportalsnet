using System;
using Vintagestory.API.Common;
using System.Linq;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using ProtoBuf;

namespace aeportalsnet
{
    public class PortalModSystem : ModSystem
    {
        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;
        private PlayerPortalManager playerPortalManager;

        public PlayerPortalManager GetPlayerPortalManager()
        {
            return playerPortalManager;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            
            api.RegisterBlockClass("PortalBlock", typeof(PortalBlock));
            api.RegisterBlockEntityClass("aeportalsnet.BEportal", typeof(BEportal));
            
            api.Network.RegisterChannel("aeportalsnet")
                .RegisterMessageType(typeof(PortalNameMessage))
                .RegisterMessageType(typeof(PortalListMessage))
                .RegisterMessageType(typeof(PortalTeleportMessage))
                .RegisterMessageType(typeof(PortalDialogClosedMessage));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;
            
            playerPortalManager = new PlayerPortalManager(api);

            api.Network.GetChannel("aeportalsnet")
                .SetMessageHandler<PortalNameMessage>((player, message) =>
                {
                    BlockPos pos = new BlockPos(message.X, message.Y, message.Z);
                    BEportal be = api.World.BlockAccessor.GetBlockEntity(pos) as BEportal;
                    
                    if (be != null)
                    {
                        string oldName = be.PortalName;
                        string newName = message.PortalName;
                        string oldOwnerUid = be.OwnerUID;
                        string actingPlayerUid = player.PlayerUID;
                        
                        api.Logger.Notification($"[aeportalsnet] Portal rename: old='{oldName}', new='{newName}', owner={oldOwnerUid}, actor={actingPlayerUid}");

                        // 1. Обновляем имя самого портала (важно для всех)
                        be.PortalName = newName;
                        
                        // 2. Обновляем запись для ВЛАДЕЛЬЦА (игрок 1)
                        // У него имя портала всегда остаётся тем, которое он дал последним
                        // Если владелец переименовал сам - имя обновится
                        // Если чужой переименовал - для владельца имя НЕ МЕНЯЕТСЯ!
                        if (actingPlayerUid == oldOwnerUid)
                        {
                            // Владелец сам переименовал - обновляем его запись
                            playerPortalManager.UpdatePlayerPortal(oldOwnerUid, pos, oldName, newName);
                            api.Logger.Notification($"[aeportalsnet] Owner renamed their portal from '{oldName}' to '{newName}'");
                        }
                        else
                        {
                            // Чужой игрок переименовал - для владельца имя остаётся старым
                            // Но нам нужно убедиться, что в его списке есть запись со старым именем
                            if (!playerPortalManager.PlayerHasPortal(oldOwnerUid, pos))
                            {
                                // Если вдруг записи нет - создаём
                                playerPortalManager.UpdatePlayerPortal(oldOwnerUid, pos, null, oldName);
                            }
                            api.Logger.Notification($"[aeportalsnet] Foreign player renamed portal, owner's name remains '{oldName}'");
                        }

                        // 3. Обработка для игрока, который переименовывает (игрок 2)
                        if (actingPlayerUid != oldOwnerUid)
                        {
                            // Удаляем СТАРУЮ запись этого портала у игрока 2 (если была)
                            playerPortalManager.RemovePlayerPortalByPosition(actingPlayerUid, pos);
                            
                            // Добавляем НОВУЮ запись с новым именем
                            playerPortalManager.AddForeignPortalForPlayer(actingPlayerUid, pos, newName, oldOwnerUid);
                            
                            api.SendMessage(player, GlobalConstants.GeneralChatGroup,
                                string.Format("Портал '{0}' добавлен в ваш список для телепортации.", newName),
                                EnumChatType.Notification);
                            
                            api.Logger.Notification($"[aeportalsnet] Foreign player {actingPlayerUid}: removed old entry, added new portal '{newName}'");
                        }
                        
                        // 4. Создаём/обновляем метку на карте для владельца
                        if (!string.IsNullOrEmpty(oldOwnerUid))
                        {
                            IServerPlayer ownerPlayer = api.World.PlayerByUid(oldOwnerUid) as IServerPlayer;
                            if (ownerPlayer != null)
                            {
                                // Для владельца используем ЕГО имя портала
                                string ownerPortalName = (actingPlayerUid == oldOwnerUid) ? newName : oldName;
                                be.UpdatePortalName(ownerPortalName, ownerPlayer);
                            }
                        }
                    }
                })
                .SetMessageHandler<PortalTeleportMessage>((player, message) =>
                {
                    BlockPos targetPos = playerPortalManager.GetPortalPosition(player.PlayerUID, message.PortalName);
                    if (targetPos != null)
                    {
                        api.World.PlaySoundAt(new AssetLocation("game:sounds/block/teleporter"), player.Entity, null, false, 16f);
                        
                        player.Entity.TeleportTo(targetPos.X, targetPos.Y, targetPos.Z);
                        player.Entity.Pos.X = targetPos.X + 0.5;
                        player.Entity.Pos.Y = targetPos.Y + 1.0;
                        player.Entity.Pos.Z = targetPos.Z + 0.5;
                        
                        api.World.PlaySoundAt(new AssetLocation("game:sounds/block/teleporter"), player.Entity, null, false, 16f);
                        
                        PortalRegistry.ResetPlayerDialogStateForAllPortals(player.PlayerUID);
                    }
                })
                .SetMessageHandler<PortalDialogClosedMessage>((player, message) =>
                {
                    PortalRegistry.ResetPlayerDialogStateForAllPortals(player.PlayerUID);
                });
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;

            api.Network.GetChannel("aeportalsnet")
                .SetMessageHandler<PortalListMessage>((message) =>
                {
                    if (message.PortalNames != null && message.PortalNames.Count > 0)
                    {
                        api.World.PlaySoundAt(new AssetLocation("game:sounds/block/teleporter"), 0, 0, 0, null, false, 16f);
                        
                        GuiDialogPortalSelection selectionDialog = new GuiDialogPortalSelection(capi, message.PortalNames);
                        selectionDialog.TryOpen();
                    }
                });
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class PortalNameMessage
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public string PortalName { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class PortalListMessage
    {
        public System.Collections.Generic.List<string> PortalNames { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class PortalTeleportMessage
    {
        public string PortalName { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class PortalDialogClosedMessage
    {
    }
}
