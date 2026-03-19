using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace aeportalsnet
{
    public static class PortalRegistry
    {
        private static System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<BlockPos>> portals = 
            new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<BlockPos>>();
        private static System.Collections.Generic.List<BEportal> activePortals = 
            new System.Collections.Generic.List<BEportal>();

        public static void RegisterPortal(string name, BlockPos pos)
        {
            if (!portals.ContainsKey(name))
            {
                portals[name] = new System.Collections.Generic.List<BlockPos>();
            }
            
            if (!portals[name].Contains(pos))
            {
                portals[name].Add(pos);
            }
        }

        public static void RemovePortal(string name, BlockPos pos)
        {
            if (portals.ContainsKey(name))
            {
                portals[name].Remove(pos);
                if (portals[name].Count == 0)
                {
                    portals.Remove(name);
                }
            }
        }

        public static void AddActivePortal(BEportal portal)
        {
            if (!activePortals.Contains(portal))
            {
                activePortals.Add(portal);
            }
        }

        public static void RemoveActivePortal(BEportal portal)
        {
            activePortals.Remove(portal);
        }

        public static void ResetPlayerDialogStateForAllPortals(string playerUID)
        {
            System.Collections.Generic.List<BEportal> portalsCopy = 
                new System.Collections.Generic.List<BEportal>(activePortals);
            foreach (var portal in portalsCopy)
            {
                portal.ResetPlayerDialogState(playerUID);
            }
        }
    }

    public class BEportal : BlockEntity
    {
        private string portalName = "portal";
        private string ownerUID = "";
        private string ownerName = "";
        private float teleportCooldown = 0f;
        private float playerSpecificCooldown = 0f;
        private const float TELEPORT_DELAY = 1.5f;
        private const float POST_TELEPORT_COOLDOWN = 3.0f;
        private const float SELECTION_DELAY = 2.0f;
        private long listenerId = -1;
        private string lastTeleportedPlayer = "";
        private long particleListenerId = -1;
        
        private System.Collections.Generic.Dictionary<string, bool> playerDialogShown = 
            new System.Collections.Generic.Dictionary<string, bool>();
        private System.Collections.Generic.Dictionary<string, double> playerEntryTime = 
            new System.Collections.Generic.Dictionary<string, double>();

        public string PortalName
        {
            get { return portalName; }
            set
            {
                if (portalName != value)
                {
                    if (Api?.Side == EnumAppSide.Server && !string.IsNullOrEmpty(portalName))
                    {
                        PortalRegistry.RemovePortal(portalName, Pos);
                    }

                    portalName = value;

                    if (Api?.Side == EnumAppSide.Server)
                    {
                        PortalRegistry.RegisterPortal(portalName, Pos);
                    }

                    MarkDirty();
                }
            }
        }

        public string OwnerUID
        {
            get { return ownerUID; }
            set
            {
                if (ownerUID != value)
                {
                    ownerUID = value;
                    MarkDirty();
                }
            }
        }

        public string OwnerName
        {
            get { return ownerName; }
            set
            {
                if (ownerName != value)
                {
                    ownerName = value;
                    MarkDirty();
                }
            }
        }

        public bool CanBreak(IPlayer player)
        {
            if (player == null) return false;
            if (player.HasPrivilege("root")) return true;
            if (string.IsNullOrEmpty(ownerUID)) return false;
            return player.PlayerUID == ownerUID;
        }

        public void SetOwnerIfMissing(IPlayer player)
        {
            if (Api?.Side == EnumAppSide.Server && string.IsNullOrEmpty(ownerUID) && player != null)
            {
                ownerUID = player.PlayerUID;
                ownerName = player.PlayerName;
                MarkDirty();
                
                Api.Logger.Notification($"[aeportalsnet] Owner set for portal at {Pos} to {player.PlayerName}");
            }
        }

        // ИСПРАВЛЕННЫЙ МЕТОД: UpdatePortalName с чистым голубым цветом (как у меток транслокатора)
        public void UpdatePortalName(string newName, IPlayer player)
        {
            if (Api?.Side != EnumAppSide.Server) return;

            string oldName = portalName;
            PortalName = newName;
            
            Api.Logger.Notification($"[aeportalsnet] UpdatePortalName: old='{oldName}', new='{newName}', ownerUID='{ownerUID}', Pos={Pos}");

            if (!string.IsNullOrEmpty(ownerUID))
            {
                IServerPlayer ownerPlayer = (Api as ICoreServerAPI)?.World.PlayerByUid(ownerUID) as IServerPlayer;
                if (ownerPlayer != null)
                {
                    Api.Logger.Notification($"[aeportalsnet] Found owner player: {ownerPlayer.PlayerName}, creating waypoint...");
                    
                    // ИСПОЛЬЗУЕМ: чистый голубой цвет (как у меток транслокатора)
                    // RGB: 0, 255, 255 - яркий голубой (циан)
                    int portalColor = ColorUtil.ColorFromRgba(0, 255, 255, 255);
                    
                    Api.Logger.Notification($"[aeportalsnet] Using pure cyan color for waypoint");
                    
                    var waypointUtil = new PortalWaypointUtil(ownerPlayer);
                    bool result = waypointUtil.AddWaypoint(Pos, newName, portalColor, "spiral", true);
                    
                    Api.Logger.Notification($"[aeportalsnet] Waypoint creation result: {result}");
                    
                    if (result)
                    {
                        Api.Logger.Notification($"[aeportalsnet] Waypoint successfully created for {ownerPlayer.PlayerName} at {Pos}");
                    }
                    else
                    {
                        Api.Logger.Error($"[aeportalsnet] Failed to create waypoint for {ownerPlayer.PlayerName} at {Pos}");
                    }
                }
                else
                {
                    Api.Logger.Notification($"[aeportalsnet] Owner player with UID {ownerUID} not found online! Waypoint not created.");
                }
            }
            else
            {
                Api.Logger.Notification($"[aeportalsnet] OwnerUID is empty, cannot create waypoint");
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            
            if (api.Side == EnumAppSide.Server)
            {
                PortalRegistry.RegisterPortal(portalName, Pos);
                PortalRegistry.AddActivePortal(this);
                listenerId = api.World.RegisterGameTickListener(OnGameTick, 500);
                
                api.World.BlockAccessor.MarkBlockDirty(Pos);
                api.World.BlockAccessor.MarkBlockDirty(Pos.UpCopy());
                api.World.BlockAccessor.MarkBlockDirty(Pos.DownCopy());
                api.World.BlockAccessor.MarkBlockDirty(Pos.NorthCopy());
                api.World.BlockAccessor.MarkBlockDirty(Pos.SouthCopy());
                api.World.BlockAccessor.MarkBlockDirty(Pos.EastCopy());
                api.World.BlockAccessor.MarkBlockDirty(Pos.WestCopy());
                
                Api.Logger.Notification($"[aeportalsnet] BEportal initialized at {Pos} with name '{portalName}', owner='{ownerUID}'");
            }
            
            if (api.Side == EnumAppSide.Client)
            {
                RegisterParticles(api as ICoreClientAPI);
            }
        }

        private void RegisterParticles(ICoreClientAPI capi)
        {
            if (capi == null) return;
            
            particleListenerId = capi.Event.RegisterGameTickListener((dt) =>
            {
                if (Api?.Side == EnumAppSide.Client)
                {
                    Random random = new Random();
                    
                    for (int i = 0; i < 3; i++)
                    {
                        double offsetX = (random.NextDouble() - 0.5) * 0.9;
                        double offsetZ = (random.NextDouble() - 0.5) * 0.9;
                        
                        Vec3d minPos = new Vec3d(
                            Pos.X + 0.5 + offsetX,
                            Pos.Y + 0.1,
                            Pos.Z + 0.5 + offsetZ
                        );
                        Vec3d maxPos = minPos.Clone();
                        
                        Vec3f minVelocity = new Vec3f(
                            (float)(random.NextDouble() - 0.5) * 0.03f,
                            0.15f,
                            (float)(random.NextDouble() - 0.5) * 0.03f
                        );
                        Vec3f maxVelocity = new Vec3f(
                            (float)(random.NextDouble() - 0.5) * 0.03f,
                            0.25f,
                            (float)(random.NextDouble() - 0.5) * 0.03f
                        );
                        
                        int color = 0x4C00FFFF;
                        
                        capi.World.SpawnParticles(
                            1f,
                            color,
                            minPos,
                            maxPos,
                            minVelocity,
                            maxVelocity,
                            4.0f,
                            0f,
                            0.27f,
                            EnumParticleModel.Cube
                        );
                    }
                }
            }, 100);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (byItemStack?.Attributes != null && byItemStack.Attributes.HasAttribute("portalName"))
            {
                portalName = byItemStack.Attributes.GetString("portalName", "portal_" + Pos.X + "_" + Pos.Y + "_" + Pos.Z);
            }
            else
            {
                portalName = "portal_" + Pos.X + "_" + Pos.Y + "_" + Pos.Z;
            }

            if (Api?.Side == EnumAppSide.Server)
            {
                PortalRegistry.RegisterPortal(portalName, Pos);
                PortalRegistry.AddActivePortal(this);
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
                
                Api.Logger.Notification($"[aeportalsnet] Portal placed at {Pos} with name '{portalName}'");
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api?.Side == EnumAppSide.Server)
            {
                // Удаляем метку портала для всех игроков
                var sapi = Api as ICoreServerAPI;
                if (sapi != null)
                {
                    // Находим первого онлайн игрока для создания утилиты
                    var firstPlayer = sapi.World.AllOnlinePlayers.FirstOrDefault() as IServerPlayer;
                    if (firstPlayer != null)
                    {
                        var waypointUtil = new PortalWaypointUtil(firstPlayer);
                        bool result = waypointUtil.RemoveWaypointForAllPlayers(Pos);
                        Api.Logger.Notification($"[aeportalsnet] Waypoint removal for portal at {Pos}: {result}");
                    }
                    else
                    {
                        Api.Logger.Notification($"[aeportalsnet] No players online to remove waypoint for portal at {Pos}");
                    }
                }

                PortalRegistry.RemovePortal(portalName, Pos);
                PortalRegistry.RemoveActivePortal(this);
                
                RemovePortalFromAllPlayers();
                
                if (listenerId != -1)
                {
                    Api.World.UnregisterGameTickListener(listenerId);
                    listenerId = -1;
                }
                
                Api.Logger.Notification($"[aeportalsnet] Portal removed at {Pos} with name '{portalName}'");
            }
            
            if (Api?.Side == EnumAppSide.Client)
            {
                if (particleListenerId != -1)
                {
                    (Api as ICoreClientAPI)?.Event.UnregisterGameTickListener(particleListenerId);
                    particleListenerId = -1;
                }
            }
        }

        private void RemovePortalFromAllPlayers()
        {
            if (Api?.Side != EnumAppSide.Server) return;
            
            var modSystem = Api.ModLoader.GetModSystem<PortalModSystem>();
            var portalManager = modSystem.GetPlayerPortalManager();
            
            var sapi = Api as ICoreServerAPI;
            if (sapi == null) return;
            
            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
            {
                portalManager.RemovePlayerPortal(player.PlayerUID, portalName);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (Api?.Side == EnumAppSide.Server)
            {
                PortalRegistry.RemovePortal(portalName, Pos);
                PortalRegistry.RemoveActivePortal(this);
                
                if (listenerId != -1)
                {
                    Api.World.UnregisterGameTickListener(listenerId);
                    listenerId = -1;
                }
            }
            
            if (Api?.Side == EnumAppSide.Client)
            {
                if (particleListenerId != -1)
                {
                    (Api as ICoreClientAPI)?.Event.UnregisterGameTickListener(particleListenerId);
                    particleListenerId = -1;
                }
            }
        }

        private void OnGameTick(float dt)
        {
            if (Api?.Side != EnumAppSide.Server) return;
            
            if (teleportCooldown > 0) teleportCooldown -= dt;
            if (playerSpecificCooldown > 0) playerSpecificCooldown -= dt;

            CheckForPlayersNearby();
        }

        private void CheckForPlayersNearby()
        {
            if (Api?.Side != EnumAppSide.Server) return;
            if (teleportCooldown > 0) return;

            var sapi = Api as ICoreServerAPI;
            if (sapi == null) return;

            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;
                if (player.PlayerUID == lastTeleportedPlayer && playerSpecificCooldown > 0) continue;

                double distance = player.Entity.Pos.XYZ.DistanceTo(Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                
                if (distance < 1.5)
                {
                    HandlePlayerInPortal(player);
                }
                else
                {
                    if (playerDialogShown.ContainsKey(player.PlayerUID)) playerDialogShown.Remove(player.PlayerUID);
                    if (playerEntryTime.ContainsKey(player.PlayerUID)) playerEntryTime.Remove(player.PlayerUID);
                }
            }
        }

        private void HandlePlayerInPortal(IServerPlayer player)
        {
            if (playerDialogShown.ContainsKey(player.PlayerUID) && playerDialogShown[player.PlayerUID]) return;
            if (!playerEntryTime.ContainsKey(player.PlayerUID))
            {
                playerEntryTime[player.PlayerUID] = Api.World.ElapsedMilliseconds / 1000.0;
                return;
            }

            double currentTime = Api.World.ElapsedMilliseconds / 1000.0;
            double elapsedTime = currentTime - playerEntryTime[player.PlayerUID];

            if (elapsedTime >= SELECTION_DELAY)
            {
                ShowPortalSelection(player);
                playerDialogShown[player.PlayerUID] = true;
            }
        }

        private void ShowPortalSelection(IServerPlayer player)
        {
            try
            {
                var modSystem = Api.ModLoader.GetModSystem<PortalModSystem>();
                if (modSystem == null) return;

                var portalManager = modSystem.GetPlayerPortalManager();
                if (portalManager == null) return;
                
                var portalNames = portalManager.GetPlayerPortalNames(player.PlayerUID);

                if (portalNames.Count > 0)
                {
                    teleportCooldown = TELEPORT_DELAY;
                    
                    PortalListMessage message = new PortalListMessage
                    {
                        PortalNames = portalNames
                    };
                    
                    var channel = (Api as ICoreServerAPI).Network.GetChannel("aeportalsnet");
                    channel?.SendPacket(message, player);
                    
                    Api.Logger.Notification($"[aeportalsnet] Sent portal list to {player.PlayerName} with {portalNames.Count} portals");
                }
            }
            catch (Exception e)
            {
                Api.Logger.Error($"[aeportalsnet] Error in ShowPortalSelection: {e.Message}");
            }
        }

        public void ResetPlayerDialogState(string playerUID)
        {
            if (playerDialogShown.ContainsKey(playerUID)) playerDialogShown.Remove(playerUID);
            if (playerEntryTime.ContainsKey(playerUID)) playerEntryTime.Remove(playerUID);
        }

        public void OnBlockInteract(IPlayer player)
        {
            if (Api?.Side == EnumAppSide.Server)
            {
                SetOwnerIfMissing(player);
            }
            
            if (Api.Side == EnumAppSide.Client)
            {
                OpenNameDialog(player as IClientPlayer);
            }
        }

        private void OpenNameDialog(IClientPlayer player)
        {
            if (Api is ICoreClientAPI capi)
            {
                GuiDialogPortalName nameDialog = new GuiDialogPortalName(capi, this, portalName);
                nameDialog.TryOpen();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("portalName", portalName);
            tree.SetString("ownerUID", ownerUID);
            tree.SetString("ownerName", ownerName);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            portalName = tree.GetString("portalName", "portal");
            ownerUID = tree.GetString("ownerUID", "");
            ownerName = tree.GetString("ownerName", "");
        }
    }
}
