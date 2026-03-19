using System;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace aeportalsnet
{
    public class GuiDialogPortalName : GuiDialog
    {
        private string currentName;
        private BEportal portalEntity;
        private string newName;
        private GuiElementTextInput textInput;
        private bool nameChanged = false;

        public override string ToggleKeyCombinationCode => null;
        public override double DrawOrder => 0.2;
        public override bool UnregisterOnClose => true;

        public GuiDialogPortalName(ICoreClientAPI capi, BEportal portal, string name) : base(capi)
        {
            portalEntity = portal;
            currentName = name;
            newName = name;
            
            ComposeDialog();
        }

        private void ComposeDialog()
        {
            int width = 500;
            int height = 180;
            
            int buttonOffsetX = 80;
            
            ElementBounds dialogBounds = ElementBounds.Fixed(EnumDialogArea.CenterMiddle, 0, 0, width, height);
            
            ElementBounds titleBounds = ElementBounds.Fixed(0, 15, width - 20, 30)
                .WithAlignment(EnumDialogArea.CenterFixed);
            
            ElementBounds subtitleBounds = ElementBounds.Fixed(0, 50, width - 40, 30)
                .WithAlignment(EnumDialogArea.CenterFixed);
            
            ElementBounds inputBounds = ElementBounds.Fixed(0, 85, width - 40, 30)
                .WithAlignment(EnumDialogArea.CenterFixed);
            
            ElementBounds okButtonBounds = ElementBounds.Fixed(buttonOffsetX, 125, 100, 30);
            ElementBounds cancelButtonBounds = ElementBounds.Fixed(buttonOffsetX + 120, 125, 100, 30);

            SingleComposer = capi.Gui
                .CreateCompo("portalnamedialog", dialogBounds)
                .AddShadedDialogBG(ElementBounds.Fill)
                .AddDialogTitleBar("Имя портала", OnTitleBarClose)
                .BeginChildElements()
                    .AddStaticText("Введите имя портала:", CairoFont.WhiteDetailText(), subtitleBounds)
                    .AddTextInput(inputBounds, OnTextChanged, CairoFont.WhiteDetailText(), "nameInput")
                    .AddButton("ОК", OnOkClick, okButtonBounds)
                    .AddButton("ОТМЕНА", OnCancelClick, cancelButtonBounds)
                .EndChildElements()
                .Compose();

            textInput = SingleComposer.GetTextInput("nameInput");
            if (textInput != null)
            {
                textInput.SetValue(currentName);
            }
        }

        private void OnTextChanged(string text)
        {
            newName = text;
            if (newName != currentName)
            {
                nameChanged = true;
            }
        }

        private bool OnOkClick()
        {
            SaveAndClose();
            return true;
        }

        private bool OnCancelClick()
        {
            TryClose();
            return true;
        }

        private void OnTitleBarClose()
        {
            if (nameChanged && !string.IsNullOrEmpty(newName) && newName != currentName)
            {
                SaveName();
            }
            TryClose();
        }

        private void SaveAndClose()
        {
            if (!string.IsNullOrEmpty(newName) && newName != currentName)
            {
                SaveName();
            }
            TryClose();
        }

        private void SaveName()
        {
            try
            {
                PortalNameMessage message = new PortalNameMessage
                {
                    X = portalEntity.Pos.X,
                    Y = portalEntity.Pos.Y,
                    Z = portalEntity.Pos.Z,
                    PortalName = newName
                };
                
                var channel = capi.Network.GetChannel("aeportalsnet");
                if (channel != null)
                {
                    channel.SendPacket(message);
                }
            }
            catch (Exception e)
            {
                capi.Logger.Error("Error sending portal name: " + e.Message);
            }
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
        }
    }
}
