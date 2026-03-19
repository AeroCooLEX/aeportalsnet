using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace aeportalsnet
{
    public class GuiDialogPortalSelection : GuiDialog
    {
        private System.Collections.Generic.List<string> portalNames;
        private int selectedIndex = -1;
        private int currentPage = 0;
        private int portalsPerPage = 5;
        private int totalPages;
        private CairoFont normalFont;
        private CairoFont selectedFont;
        private bool isOpen = false;
        private long soundListenerId = -1;

        public override string ToggleKeyCombinationCode => null;
        public override double DrawOrder => 0.2;
        public override bool UnregisterOnClose => true;

        public GuiDialogPortalSelection(ICoreClientAPI capi, System.Collections.Generic.List<string> names) : base(capi)
        {
            portalNames = names;
            totalPages = (int)Math.Ceiling((double)portalNames.Count / portalsPerPage);
            
            normalFont = CairoFont.WhiteDetailText();
            selectedFont = CairoFont.WhiteDetailText().WithColor(GuiStyle.ActiveButtonTextColor);
            
            ComposeDialog();
            isOpen = true;
            
            StartSoundExtension();
        }

        private void StartSoundExtension()
        {
            capi.World.PlaySoundAt(new AssetLocation("game:sounds/block/teleporter"), 0, 0, 0, null, false, 16f);
            
            soundListenerId = capi.Event.RegisterCallback((dt) =>
            {
                if (isOpen)
                {
                    capi.World.PlaySoundAt(new AssetLocation("game:sounds/block/teleporter"), 0, 0, 0, null, false, 16f);
                }
            }, 2000);
        }

        private void ComposeDialog()
        {
            int width = 550;
            int height = 400;
            
            ElementBounds dialogBounds = ElementBounds.Fixed(EnumDialogArea.CenterMiddle, 0, 0, width, height);
            
            string pageInfo = totalPages > 1 ? $" (страница {currentPage + 1} из {totalPages})" : "";
            ElementBounds titleBounds = ElementBounds.Fixed(0, 45, width - 40, 35)
                .WithAlignment(EnumDialogArea.CenterFixed);
            
            var composer = capi.Gui
                .CreateCompo("portalselection", dialogBounds)
                .AddShadedDialogBG(ElementBounds.Fill)
                .AddDialogTitleBar("Выберите портал", OnTitleBarClose)
                .BeginChildElements();
            
            composer.AddStaticText($"Ваши порталы:{pageInfo}", CairoFont.WhiteDetailText().WithFontSize(16), titleBounds);
            
            int startIndex = currentPage * portalsPerPage;
            int endIndex = Math.Min(startIndex + portalsPerPage, portalNames.Count);
            int yOffset = 90;
            int buttonWidth = width - 120;
            int buttonX = (width - buttonWidth) / 2;
            
            for (int i = startIndex; i < endIndex; i++)
            {
                int index = i;
                string name = portalNames[i];
                
                string displayName = name.Length > 30 ? name.Substring(0, 27) + "..." : name;
                
                ElementBounds entryBounds = ElementBounds.Fixed(buttonX, yOffset, buttonWidth, 35);
                
                CairoFont font = (i == selectedIndex) ? selectedFont : normalFont;
                
                composer.AddButton(displayName, () => OnPortalClicked(index), entryBounds, font.WithFontSize(14), EnumButtonStyle.Normal);
                
                yOffset += 40;
            }

            if (totalPages > 1)
            {
                int navY = height - 90;
                int navButtonWidth = 100;
                int navButtonHeight = 35;
                int spacing = 20;
                int totalNavWidth = navButtonWidth * 2 + spacing;
                int startX = (width - totalNavWidth) / 2;
                
                ElementBounds prevButtonBounds = ElementBounds.Fixed(startX, navY, navButtonWidth, navButtonHeight);
                composer.AddButton("< Назад", OnPrevPageClick, prevButtonBounds, CairoFont.WhiteDetailText().WithFontSize(14), EnumButtonStyle.Normal);
                
                ElementBounds nextButtonBounds = ElementBounds.Fixed(startX + navButtonWidth + spacing, navY, navButtonWidth, navButtonHeight);
                composer.AddButton("Вперед >", OnNextPageClick, nextButtonBounds, CairoFont.WhiteDetailText().WithFontSize(14), EnumButtonStyle.Normal);
            }

            int cancelY = height - 45;
            ElementBounds cancelButtonBounds = ElementBounds.Fixed((width - 120) / 2, cancelY, 120, 35);
            composer.AddButton("Отмена", OnCancelClick, cancelButtonBounds, CairoFont.WhiteDetailText().WithFontSize(14), EnumButtonStyle.Normal);
            
            composer.EndChildElements();
            SingleComposer = composer.Compose();
        }

        private bool OnPrevPageClick()
        {
            if (currentPage > 0)
            {
                currentPage--;
                RecreateDialog();
            }
            return true;
        }

        private bool OnNextPageClick()
        {
            if (currentPage < totalPages - 1)
            {
                currentPage++;
                RecreateDialog();
            }
            return true;
        }

        private bool OnPortalClicked(int index)
        {
            selectedIndex = index;
            TeleportToSelectedPortal();
            return true;
        }

        private void RecreateDialog()
        {
            int width = 550;
            int height = 400;
            
            ElementBounds dialogBounds = ElementBounds.Fixed(EnumDialogArea.CenterMiddle, 0, 0, width, height);
            
            string pageInfo = totalPages > 1 ? $" (страница {currentPage + 1} из {totalPages})" : "";
            ElementBounds titleBounds = ElementBounds.Fixed(0, 45, width - 40, 35)
                .WithAlignment(EnumDialogArea.CenterFixed);
            
            var composer = capi.Gui
                .CreateCompo("portalselection", dialogBounds)
                .AddShadedDialogBG(ElementBounds.Fill)
                .AddDialogTitleBar("Выберите портал", OnTitleBarClose)
                .BeginChildElements();
            
            composer.AddStaticText($"Ваши порталы:{pageInfo}", CairoFont.WhiteDetailText().WithFontSize(16), titleBounds);
            
            int startIndex = currentPage * portalsPerPage;
            int endIndex = Math.Min(startIndex + portalsPerPage, portalNames.Count);
            int yOffset = 90;
            int buttonWidth = width - 120;
            int buttonX = (width - buttonWidth) / 2;
            
            for (int i = startIndex; i < endIndex; i++)
            {
                int index = i;
                string name = portalNames[i];
                string displayName = name.Length > 30 ? name.Substring(0, 27) + "..." : name;
                
                ElementBounds entryBounds = ElementBounds.Fixed(buttonX, yOffset, buttonWidth, 35);
                
                CairoFont font = (i == selectedIndex) ? selectedFont : normalFont;
                
                composer.AddButton(displayName, () => OnPortalClicked(index), entryBounds, font.WithFontSize(14), EnumButtonStyle.Normal);
                
                yOffset += 40;
            }

            if (totalPages > 1)
            {
                int navY = height - 90;
                int navButtonWidth = 100;
                int navButtonHeight = 35;
                int spacing = 20;
                int totalNavWidth = navButtonWidth * 2 + spacing;
                int startX = (width - totalNavWidth) / 2;
                
                ElementBounds prevButtonBounds = ElementBounds.Fixed(startX, navY, navButtonWidth, navButtonHeight);
                composer.AddButton("< Назад", OnPrevPageClick, prevButtonBounds, CairoFont.WhiteDetailText().WithFontSize(14), EnumButtonStyle.Normal);
                
                ElementBounds nextButtonBounds = ElementBounds.Fixed(startX + navButtonWidth + spacing, navY, navButtonWidth, navButtonHeight);
                composer.AddButton("Вперед >", OnNextPageClick, nextButtonBounds, CairoFont.WhiteDetailText().WithFontSize(14), EnumButtonStyle.Normal);
            }

            int cancelY = height - 45;
            ElementBounds cancelButtonBounds = ElementBounds.Fixed((width - 120) / 2, cancelY, 120, 35);
            composer.AddButton("Отмена", OnCancelClick, cancelButtonBounds, CairoFont.WhiteDetailText().WithFontSize(14), EnumButtonStyle.Normal);
            
            composer.EndChildElements();
            SingleComposer = composer.Compose();
        }

        private void TeleportToSelectedPortal()
        {
            if (selectedIndex >= 0 && selectedIndex < portalNames.Count)
            {
                capi.World.PlaySoundAt(new AssetLocation("game:sounds/block/teleporter"), 0, 0, 0, null, false, 16f);
                
                PortalTeleportMessage message = new PortalTeleportMessage
                {
                    PortalName = portalNames[selectedIndex]
                };
                
                capi.Network.GetChannel("aeportalsnet").SendPacket(message);
                
                capi.Event.RegisterCallback((dt) => CloseDialog(), 500);
            }
        }

        private bool OnCancelClick()
        {
            CloseDialog();
            return true;
        }

        private void OnTitleBarClose()
        {
            CloseDialog();
        }

        private void CloseDialog()
        {
            if (isOpen)
            {
                isOpen = false;
                
                if (soundListenerId != -1)
                {
                    capi.Event.UnregisterCallback(soundListenerId);
                    soundListenerId = -1;
                }
                
                PortalDialogClosedMessage message = new PortalDialogClosedMessage();
                capi.Network.GetChannel("aeportalsnet").SendPacket(message);
                TryClose();
            }
        }

        public override void OnGuiClosed()
        {
            if (isOpen)
            {
                isOpen = false;
                
                if (soundListenerId != -1)
                {
                    capi.Event.UnregisterCallback(soundListenerId);
                    soundListenerId = -1;
                }
                
                PortalDialogClosedMessage message = new PortalDialogClosedMessage();
                capi.Network.GetChannel("aeportalsnet").SendPacket(message);
            }
            base.OnGuiClosed();
        }
    }
}
