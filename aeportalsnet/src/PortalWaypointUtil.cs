using System;
using System.Linq;
using System.Reflection;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.Common;

namespace aeportalsnet
{
    public class PortalWaypointUtil
    {
        private readonly WaypointMapLayer _waypointMapLayer;
        private readonly IServerPlayer _player;
        private static readonly MethodInfo _resendWaypointsMethod;
        private static readonly MethodInfo _rebuildMapComponentsMethod;

        static PortalWaypointUtil()
        {
            Type type = typeof(WaypointMapLayer);
            _resendWaypointsMethod = type.GetMethod("ResendWaypoints", BindingFlags.NonPublic | BindingFlags.Instance);
            _rebuildMapComponentsMethod = type.GetMethod("RebuildMapComponents", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public PortalWaypointUtil(IServerPlayer player)
        {
            _player = player;
            WorldMapManager worldMapManager = player.Entity.Api.ModLoader.GetModSystem<WorldMapManager>();
            _waypointMapLayer = worldMapManager.MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer;

            if (_waypointMapLayer == null)
            {
                player.Entity.Api.Logger.Error($"[aeportalsnet] Failed to get WaypointMapLayer for player {player.PlayerName}");
            }
            else
            {
                player.Entity.Api.Logger.Notification($"[aeportalsnet] Successfully got WaypointMapLayer for player {player.PlayerName}");
            }
        }

        private bool IsValid => _waypointMapLayer != null && _player != null;

        private void InvokeResendWaypoints()
        {
            try
            {
                if (_resendWaypointsMethod == null) return;
                
                // Пробуем разные сигнатуры
                var parameters = _resendWaypointsMethod.GetParameters();
                if (parameters.Length == 0)
                {
                    // Версия без параметров
                    _resendWaypointsMethod.Invoke(_waypointMapLayer, null);
                }
                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(IServerPlayer))
                {
                    // Версия с одним параметром (игрок)
                    _resendWaypointsMethod.Invoke(_waypointMapLayer, new object[] { _player });
                }
                else
                {
                    // Версия с другими параметрами - пробуем без параметров как запасной вариант
                    _resendWaypointsMethod.Invoke(_waypointMapLayer, null);
                }
            }
            catch (Exception e)
            {
                _player.Entity.Api.Logger.Error($"[aeportalsnet] Error invoking ResendWaypoints: {e.Message}");
            }
        }

        private void InvokeRebuildMapComponents()
        {
            try
            {
                if (_rebuildMapComponentsMethod == null) return;
                
                var parameters = _rebuildMapComponentsMethod.GetParameters();
                if (parameters.Length == 0)
                {
                    _rebuildMapComponentsMethod.Invoke(_waypointMapLayer, null);
                }
                else
                {
                    // Если есть параметры, пробуем передать null
                    _rebuildMapComponentsMethod.Invoke(_waypointMapLayer, new object[parameters.Length]);
                }
            }
            catch (Exception e)
            {
                _player.Entity.Api.Logger.Error($"[aeportalsnet] Error invoking RebuildMapComponents: {e.Message}");
            }
        }

        public bool AddWaypoint(BlockPos pos, string name, int color, string icon = "spiral", bool pinned = true)
        {
            if (!IsValid)
            {
                _player.Entity.Api.Logger.Error($"[aeportalsnet] AddWaypoint: Invalid state - _waypointMapLayer={_waypointMapLayer != null}, _player={_player != null}");
                return false;
            }

            try
            {
                _player.Entity.Api.Logger.Notification($"[aeportalsnet] Adding waypoint at {pos} for {_player.PlayerName}: name='{name}', color={color}, icon='{icon}'");

                var waypoint = new Waypoint
                {
                    Position = new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5),
                    Title = name,
                    Icon = icon,
                    Color = color,
                    Pinned = pinned,
                    OwningPlayerUid = _player.PlayerUID
                };

                _waypointMapLayer.Waypoints.Add(waypoint);
                _player.Entity.Api.Logger.Notification($"[aeportalsnet] Waypoint added to collection, count now: {_waypointMapLayer.Waypoints.Count}");
                
                InvokeResendWaypoints();
                _player.Entity.Api.Logger.Notification($"[aeportalsnet] ResendWaypoints called");
                
                InvokeRebuildMapComponents();
                _player.Entity.Api.Logger.Notification($"[aeportalsnet] RebuildMapComponents called");
                
                return true;
            }
            catch (Exception e)
            {
                _player.Entity.Api.Logger.Error($"[aeportalsnet] Exception in AddWaypoint: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        public bool UpdateWaypointName(BlockPos pos, string newName)
        {
            if (!IsValid) return false;

            try
            {
                var waypoint = _waypointMapLayer.Waypoints.FirstOrDefault(w =>
                    w.OwningPlayerUid == _player.PlayerUID &&
                    w.Position.AsBlockPos == pos);

                if (waypoint != null)
                {
                    waypoint.Title = newName;
                    InvokeResendWaypoints();
                    InvokeRebuildMapComponents();
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                _player.Entity.Api.Logger.Error($"[aeportalsnet] Exception in UpdateWaypointName: {e.Message}");
                return false;
            }
        }

        public bool RemoveWaypoint(BlockPos pos)
        {
            if (!IsValid) return false;

            try
            {
                var waypoint = _waypointMapLayer.Waypoints.FirstOrDefault(w =>
                    w.OwningPlayerUid == _player.PlayerUID &&
                    w.Position.AsBlockPos == pos);

                if (waypoint != null)
                {
                    _waypointMapLayer.Waypoints.Remove(waypoint);
                    InvokeResendWaypoints();
                    InvokeRebuildMapComponents();
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                _player.Entity.Api.Logger.Error($"[aeportalsnet] Exception in RemoveWaypoint: {e.Message}");
                return false;
            }
        }

        // НОВЫЙ МЕТОД: удалить все метки в указанной позиции для всех игроков
        public bool RemoveWaypointForAllPlayers(BlockPos pos)
        {
            if (_waypointMapLayer == null) return false;

            try
            {
                _player.Entity.Api.Logger.Notification($"[aeportalsnet] Removing waypoints at {pos} for all players");
                
                bool removed = false;
                // Находим все метки в этой позиции для всех игроков
                var waypointsToRemove = _waypointMapLayer.Waypoints
                    .Where(w => w.Position.AsBlockPos == pos)
                    .ToList();

                foreach (var waypoint in waypointsToRemove)
                {
                    _waypointMapLayer.Waypoints.Remove(waypoint);
                    removed = true;
                    _player.Entity.Api.Logger.Notification($"[aeportalsnet] Removed waypoint '{waypoint.Title}' for player {waypoint.OwningPlayerUid}");
                }

                if (removed)
                {
                    InvokeResendWaypoints();
                    InvokeRebuildMapComponents();
                    _player.Entity.Api.Logger.Notification($"[aeportalsnet] Waypoints removed and map updated");
                }
                return removed;
            }
            catch (Exception e)
            {
                _player.Entity.Api.Logger.Error($"[aeportalsnet] Exception in RemoveWaypointForAllPlayers: {e.Message}");
                return false;
            }
        }
    }
}
