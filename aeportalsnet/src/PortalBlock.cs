using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.API.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace aeportalsnet
{
    public class PortalBlock : Block
    {
        public override void OnBlockPlaced(IWorldAccessor world, BlockPos pos, ItemStack stack)
        {
            base.OnBlockPlaced(world, pos, stack);
            
            if (world.Side == EnumAppSide.Server)
            {
                world.RegisterCallback((dt) =>
                {
                    BEportal be = world.BlockAccessor.GetBlockEntity(pos) as BEportal;
                    if (be != null)
                    {
                        if (stack.Attributes != null && stack.Attributes.HasAttribute("portalName"))
                        {
                            be.PortalName = stack.Attributes.GetString("portalName", "portal_" + pos.X + "_" + pos.Y + "_" + pos.Z);
                        }
                        else
                        {
                            be.PortalName = "portal_" + pos.X + "_" + pos.Y + "_" + pos.Z;
                        }
                        
                        IPlayer byPlayer = world.NearestPlayer(pos.X, pos.Y, pos.Z);
                        if (byPlayer != null)
                        {
                            be.OwnerUID = byPlayer.PlayerUID;
                            be.OwnerName = byPlayer.PlayerName;
                            
                            world.Logger.Notification($"Portal owner set to {byPlayer.PlayerName} at placement");
                        }
                        
                        be.MarkDirty();
                    }
                }, 100);
            }
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = base.OnPickBlock(world, pos);
            
            BEportal be = world.BlockAccessor.GetBlockEntity(pos) as BEportal;
            if (be != null && !string.IsNullOrEmpty(be.PortalName))
            {
                stack.Attributes.SetString("portalName", be.PortalName);
            }
            
            return stack;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BEportal be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEportal;
            if (be != null)
            {
                be.OnBlockInteract(byPlayer);
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            bool shouldRemove = false;
            string ownerUID = null;
            string portalName = null;
            
            if (world.Side == EnumAppSide.Server)
            {
                BEportal be = world.BlockAccessor.GetBlockEntity(pos) as BEportal;
                
                if (be != null)
                {
                    portalName = be.PortalName;
                    ownerUID = be.OwnerUID;
                    
                    if (byPlayer != null)
                    {
                        if (!be.CanBreak(byPlayer))
                        {
                            (world as ICoreServerAPI)?.SendMessage(byPlayer, GlobalConstants.GeneralChatGroup,
                                "Вы не можете сломать чужой портал!", EnumChatType.Notification);
                            return;
                        }
                        
                        if (byPlayer.HasPrivilege("root") && ownerUID != byPlayer.PlayerUID)
                        {
                            shouldRemove = true;
                        }
                    }
                }
            }

            BEportal beForRemoval = world.BlockAccessor.GetBlockEntity(pos) as BEportal;
            if (beForRemoval != null)
            {
                PortalRegistry.RemovePortal(beForRemoval.PortalName, pos);
                
                if (shouldRemove && !string.IsNullOrEmpty(ownerUID) && !string.IsNullOrEmpty(portalName))
                {
                    var modSystem = world.Api.ModLoader.GetModSystem<PortalModSystem>();
                    var portalManager = modSystem?.GetPlayerPortalManager();
                    portalManager?.RemovePlayerPortal(ownerUID, portalName);
                }
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            string baseInfo = base.GetPlacedBlockInfo(world, pos, forPlayer);
            StringBuilder sb = new StringBuilder();
            
            if (!string.IsNullOrEmpty(baseInfo))
            {
                sb.AppendLine(baseInfo);
            }
            
            BEportal be = world.BlockAccessor.GetBlockEntity(pos) as BEportal;
            if (be != null)
            {
                sb.AppendLine(Lang.Get("Название: {0}", be.PortalName));
                
                if (!string.IsNullOrEmpty(be.OwnerName))
                {
                    sb.AppendLine(Lang.Get("Владелец: {0}", be.OwnerName));
                }
                else if (!string.IsNullOrEmpty(be.OwnerUID))
                {
                    IPlayer owner = world.PlayerByUid(be.OwnerUID);
                    if (owner != null)
                    {
                        sb.AppendLine(Lang.Get("Владелец: {0}", owner.PlayerName));
                    }
                }
                
                if (forPlayer != null && forPlayer.HasPrivilege("root"))
                {
                    sb.AppendLine(Lang.Get("(Администратор) Вы можете сломать этот портал"));
                }
            }
            
            return sb.ToString();
        }
    }
}
