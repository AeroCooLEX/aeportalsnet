using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Newtonsoft.Json;

namespace aeportalsnet
{
    public class PortalInfo
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public DateTime CreatedAt { get; set; }
        public string OwnerUID { get; set; }
    }

    public class PlayerPortalData
    {
        public Dictionary<string, PortalInfo> Portals { get; set; } = new Dictionary<string, PortalInfo>();
    }

    public class PlayerPortalManager
    {
        private ICoreServerAPI sapi;
        private Dictionary<string, PlayerPortalData> playerPortals = new Dictionary<string, PlayerPortalData>();
        private string savePath;

        public PlayerPortalManager(ICoreServerAPI api)
        {
            sapi = api;
            savePath = Path.Combine(api.GetOrCreateDataPath("aeportalsnet"), "playerportals.json");
            LoadAllPortals();
        }

        private void LoadAllPortals()
        {
            if (File.Exists(savePath))
            {
                try
                {
                    string json = File.ReadAllText(savePath);
                    playerPortals = JsonConvert.DeserializeObject<Dictionary<string, PlayerPortalData>>(json) 
                        ?? new Dictionary<string, PlayerPortalData>();
                }
                catch (Exception e)
                {
                    sapi.Logger.Error("Failed to load player portals: " + e.Message);
                    playerPortals = new Dictionary<string, PlayerPortalData>();
                }
            }
        }

        private void SaveAllPortals()
        {
            try
            {
                string json = JsonConvert.SerializeObject(playerPortals, Formatting.Indented);
                File.WriteAllText(savePath, json);
            }
            catch (Exception e)
            {
                sapi.Logger.Error("Failed to save player portals: " + e.Message);
            }
        }

        // ИСПРАВЛЕННЫЙ МЕТОД: проверить, существует ли блок портала по координатам (с учетом выгруженных чанков)
        public bool DoesPortalExist(BlockPos pos)
        {
            if (sapi == null || sapi.World == null || sapi.World.BlockAccessor == null)
                return false;
            
            // Проверяем, загружен ли чанк
            IWorldChunk chunk = sapi.World.BlockAccessor.GetChunkAtBlockPos(pos);
            bool isChunkLoaded = chunk != null;
            
            // Если чанк не загружен, мы не можем точно сказать, есть там портал или нет
            // В этом случае считаем, что портал существует (оптимистичное предположение)
            if (!isChunkLoaded)
            {
                // Логируем для отладки
                sapi.Logger.Notification($"[aeportalsnet] Chunk not loaded for {pos}, assuming portal exists");
                return true;
            }
            
            // Чанк загружен - можем проверить точно
            Block block = sapi.World.BlockAccessor.GetBlock(pos);
            if (block == null) return false;
            
            // Проверяем, является ли блок нашим порталом
            bool isPortal = block is PortalBlock;
            
            // Дополнительно проверяем, есть ли блок-сущность
            if (isPortal)
            {
                BEportal be = sapi.World.BlockAccessor.GetBlockEntity(pos) as BEportal;
                return be != null;
            }
            
            return false;
        }

        // НОВЫЙ МЕТОД: проверить существование портала с учетом того, что он мог быть удален
        public bool IsPortalDefinitelyMissing(BlockPos pos)
        {
            if (sapi == null || sapi.World == null || sapi.World.BlockAccessor == null)
                return false;
            
            // Проверяем, загружен ли чанк
            IWorldChunk chunk = sapi.World.BlockAccessor.GetChunkAtBlockPos(pos);
            bool isChunkLoaded = chunk != null;
            
            // Если чанк не загружен, мы не можем сказать точно
            if (!isChunkLoaded)
            {
                return false; // Не знаем точно - считаем что может существовать
            }
            
            // Чанк загружен - можем проверить точно
            Block block = sapi.World.BlockAccessor.GetBlock(pos);
            if (block == null) return true; // Блок отсутствует
            
            // Проверяем, является ли блок нашим порталом
            bool isPortal = block is PortalBlock;
            
            if (isPortal)
            {
                BEportal be = sapi.World.BlockAccessor.GetBlockEntity(pos) as BEportal;
                return be == null; // Если блок есть, но нет сущности - портал битый
            }
            
            return true; // Есть другой блок - портал точно удален
        }

        // ИСПРАВЛЕННЫЙ МЕТОД: очистить список порталов игрока от несуществующих (только если точно знаем, что их нет)
        public void CleanupPlayerPortals(string playerUID)
        {
            if (!playerPortals.ContainsKey(playerUID)) return;
            
            var playerData = playerPortals[playerUID];
            var portalsToRemove = new List<string>();
            
            foreach (var portal in playerData.Portals)
            {
                BlockPos pos = new BlockPos(portal.Value.X, portal.Value.Y, portal.Value.Z);
                
                // Удаляем только если точно знаем, что портала нет
                if (IsPortalDefinitelyMissing(pos))
                {
                    portalsToRemove.Add(portal.Key);
                    sapi.Logger.Notification($"[aeportalsnet] Removing definitely non-existent portal '{portal.Key}' at {pos} for player {playerUID}");
                }
                else
                {
                    sapi.Logger.Notification($"[aeportalsnet] Portal '{portal.Key}' at {pos} may exist (chunk not loaded or loaded), keeping in list");
                }
            }
            
            foreach (var portalName in portalsToRemove)
            {
                playerData.Portals.Remove(portalName);
            }
            
            if (portalsToRemove.Count > 0)
            {
                SaveAllPortals();
            }
        }

        public void UpdatePlayerPortal(string playerUID, BlockPos pos, string oldName, string newName)
        {
            if (!playerPortals.ContainsKey(playerUID))
            {
                playerPortals[playerUID] = new PlayerPortalData();
            }

            var playerData = playerPortals[playerUID];
            
            if (!string.IsNullOrEmpty(oldName) && playerData.Portals.ContainsKey(oldName))
            {
                playerData.Portals.Remove(oldName);
            }
            
            playerData.Portals[newName] = new PortalInfo
            {
                X = pos.X,
                Y = pos.Y,
                Z = pos.Z,
                CreatedAt = DateTime.Now,
                OwnerUID = playerUID
            };
            
            SaveAllPortals();
        }

        public void AddForeignPortalForPlayer(string playerUID, BlockPos pos, string portalName, string ownerUID)
        {
            if (!playerPortals.ContainsKey(playerUID))
            {
                playerPortals[playerUID] = new PlayerPortalData();
            }

            var playerData = playerPortals[playerUID];

            if (playerData.Portals.ContainsKey(portalName))
            {
                playerData.Portals.Remove(portalName);
            }

            playerData.Portals[portalName] = new PortalInfo
            {
                X = pos.X,
                Y = pos.Y,
                Z = pos.Z,
                CreatedAt = DateTime.Now,
                OwnerUID = ownerUID
            };

            SaveAllPortals();
        }

        public void RemovePlayerPortal(string playerUID, string portalName)
        {
            if (playerPortals.ContainsKey(playerUID))
            {
                playerPortals[playerUID].Portals.Remove(portalName);
                SaveAllPortals();
            }
        }

        public bool PlayerHasPortal(string playerUID, BlockPos pos)
        {
            if (!playerPortals.ContainsKey(playerUID)) return false;
            
            foreach (var portal in playerPortals[playerUID].Portals)
            {
                if (portal.Value.X == pos.X && portal.Value.Y == pos.Y && portal.Value.Z == pos.Z)
                {
                    return true;
                }
            }
            return false;
        }

        public void RemovePlayerPortalByPosition(string playerUID, BlockPos pos)
        {
            if (!playerPortals.ContainsKey(playerUID)) return;
            
            string portalNameToRemove = null;
            foreach (var portal in playerPortals[playerUID].Portals)
            {
                if (portal.Value.X == pos.X && portal.Value.Y == pos.Y && portal.Value.Z == pos.Z)
                {
                    portalNameToRemove = portal.Key;
                    break;
                }
            }
            
            if (portalNameToRemove != null)
            {
                playerPortals[playerUID].Portals.Remove(portalNameToRemove);
                SaveAllPortals();
            }
        }

        public BlockPos GetPortalPosition(string playerUID, string portalName)
        {
            if (playerPortals.ContainsKey(playerUID) && 
                playerPortals[playerUID].Portals.ContainsKey(portalName))
            {
                var info = playerPortals[playerUID].Portals[portalName];
                BlockPos pos = new BlockPos(info.X, info.Y, info.Z);
                
                // Проверяем, существует ли еще портал (с учетом выгруженных чанков)
                if (!DoesPortalExist(pos))
                {
                    // Если портала точно нет, удаляем его из списка
                    if (IsPortalDefinitelyMissing(pos))
                    {
                        RemovePlayerPortal(playerUID, portalName);
                        sapi.Logger.Notification($"[aeportalsnet] Removed non-existent portal '{portalName}' from {playerUID}'s list");
                    }
                    else
                    {
                        // Чанк не загружен - считаем что портал может существовать
                        sapi.Logger.Notification($"[aeportalsnet] Portal '{portalName}' at {pos} may exist (chunk not loaded), allowing teleport attempt");
                        return pos;
                    }
                    return null;
                }
                
                return pos;
            }
            return null;
        }

        public List<string> GetPlayerPortalNames(string playerUID)
        {
            var validPortals = new List<string>();
            
            if (playerPortals.ContainsKey(playerUID))
            {
                // Очищаем только те порталы, которые точно не существуют
                CleanupPlayerPortals(playerUID);
                
                foreach (var portal in playerPortals[playerUID].Portals)
                {
                    validPortals.Add(portal.Key);
                }
            }
            
            return validPortals;
        }

        public string GetPortalOwner(BlockPos pos)
        {
            foreach (var playerData in playerPortals.Values)
            {
                foreach (var portal in playerData.Portals)
                {
                    if (portal.Value.X == pos.X && portal.Value.Y == pos.Y && portal.Value.Z == pos.Z)
                    {
                        return portal.Value.OwnerUID;
                    }
                }
            }
            return null;
        }

        public bool IsPlayerOwner(string playerUID, BlockPos pos)
        {
            string owner = GetPortalOwner(pos);
            return owner != null && owner == playerUID;
        }

        public PlayerPortalData GetPlayerPortalData(string playerUID)
        {
            if (playerPortals.ContainsKey(playerUID))
            {
                return playerPortals[playerUID];
            }
            return null;
        }

        public IEnumerable<KeyValuePair<string, PlayerPortalData>> GetAllPlayersData()
        {
            return playerPortals;
        }

        public bool IsChunkLoaded(BlockPos pos)
        {
            if (sapi == null || sapi.World == null || sapi.World.BlockAccessor == null)
                return false;
            
            IWorldChunk chunk = sapi.World.BlockAccessor.GetChunkAtBlockPos(pos);
            return chunk != null;
        }
    }
}
