using NLog;
using Shared.Input;
using Shared.Data;
using Shared.Enums;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using XboxGamingBarHelper.Core;
using XboxGamingBarHelper.Devices;
using XboxGamingBarHelper.Performance;

namespace XboxGamingBarHelper.Devices.Libraries.Legion
{
    internal partial class LegionManager
    {
        private int joystickAsMouseMode = 0;  // 0=Disabled, 1=Left Stick, 2=Right Stick
        private int joystickMouseSens = 50;   // 10-100

        /// <summary>
        /// Sets joystick as mouse mode (0=Disabled, 1=Left Stick, 2=Right Stick).
        /// </summary>
        public void SetJoystickAsMouseMode(int mode)
        {
            joystickAsMouseMode = mode;

            if (LegionDesktopControls?.Value == true)
            {
                // Desktop Controls uses elevated helper-side injection for cursor/scroll.
                ApplyJoystickAsMouse(true, false, joystickMouseSens);
                ApplyJoystickAsMouse(false, false, joystickMouseSens);
                Program.StartDesktopAdminMouse(mode, joystickMouseSens);
                return;
            }

            Program.StopDesktopAdminMouse();

            // Always disable every stick that should be off instead of trusting the
            // tracked previous mode: the firmware keeps mouse mode across helper
            // restarts and dropped writes (pad disconnected mid-apply), so a stale
            // previousMode leaves a stick stuck as mouse with no way for "off" to
            // clear it (field report: right stick moving the cursor while the UI
            // showed joystick-as-mouse off).
            if (mode != 1)
            {
                ApplyJoystickAsMouse(true, false, joystickMouseSens);
            }
            if (mode != 2)
            {
                ApplyJoystickAsMouse(false, false, joystickMouseSens);
            }

            // Enable new stick if not disabled
            if (mode == 1)
            {
                ApplyJoystickAsMouse(true, true, joystickMouseSens);
            }
            else if (mode == 2)
            {
                ApplyJoystickAsMouse(false, true, joystickMouseSens);
            }
        }

        /// <summary>
        /// Re-sends the current joystick-as-mouse state to both halves. Called when a
        /// half reconnects: a disable/enable written while it was offline never reached
        /// its firmware, leaving the physical state diverged from the UI.
        /// </summary>
        public void ReapplyJoystickAsMouseState()
        {
            int mode = joystickAsMouseMode;
            Logger.Info($"Reapplying joystick-as-mouse state after controller reconnect: mode={mode}");
            ApplyJoystickAsMouse(true, mode == 1, joystickMouseSens);
            ApplyJoystickAsMouse(false, mode == 2, joystickMouseSens);
        }

        /// <summary>
        /// Sets joystick mouse sensitivity (10-100).
        /// </summary>
        public void SetJoystickMouseSens(int sensitivity)
        {
            joystickMouseSens = sensitivity;
            if (LegionDesktopControls?.Value == true)
            {
                Program.StartDesktopAdminMouse(joystickAsMouseMode, joystickMouseSens);
                return;
            }
            // Only apply if a joystick is active
            if (joystickAsMouseMode == 1)
            {
                ApplyJoystickAsMouse(true, true, sensitivity);
            }
            else if (joystickAsMouseMode == 2)
            {
                ApplyJoystickAsMouse(false, true, sensitivity);
            }
        }

        /// <summary>
        /// Applies joystick as mouse setting via HID command.
        /// </summary>
        private void ApplyJoystickAsMouse(bool isLeft, bool enabled, int sensitivity)
        {
            try
            {
                using var controller = new LegionGoController();
                if (!controller.Connect())
                {
                    Logger.Warn("Cannot set joystick as mouse: controller not connected");
                    return;
                }

                var ctrl = isLeft ? Controller.Left : Controller.Right;
                bool success = controller.SetJoystickAsMouse(ctrl, enabled, sensitivity);
                if (success)
                {
                    Logger.Info($"{(isLeft ? "Left" : "Right")} joystick as mouse: enabled={enabled}, sensitivity={sensitivity}");
                }
                else
                {
                    Logger.Error($"Failed to set {(isLeft ? "left" : "right")} joystick as mouse");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error setting joystick as mouse: {ex.Message}");
            }
        }

        // Helper-side button behaviors the firmware can't express, keyed by the
        // LegionButtonMonitor edge button. Two kinds:
        //  - System actions (Touch Keyboard, Task Manager, ... - HotkeyAction ids):
        //    fired on press by Program.HotkeyHandlers' ButtonEdge dispatch.
        //  - Scroll repeat: buttons mapped to a mouse scroll direction scroll
        //    continuously while held (field request: firmware scroll mappings are
        //    one-press-one-notch). Value is the 1-based mouse code (4=Up, 5=Down,
        //    6=Left, 7=Right).
        // Registered by SetButtonMappingAdvanced / SetLegionButtonMapping, which
        // firmware-CLEAR the button so it emits nothing OS-visible while the
        // helper drives the behavior.
        private readonly object helperSideMappingLock = new object();
        private readonly System.Collections.Generic.Dictionary<Labs.LegionInputButton, int> systemActionRemaps =
            new System.Collections.Generic.Dictionary<Labs.LegionInputButton, int>();
        private readonly System.Collections.Generic.Dictionary<Labs.LegionInputButton, int> scrollRepeatRemaps =
            new System.Collections.Generic.Dictionary<Labs.LegionInputButton, int>();
        private readonly System.Collections.Generic.Dictionary<Labs.LegionInputButton, int> mouseClickRemaps =
            new System.Collections.Generic.Dictionary<Labs.LegionInputButton, int>();

        internal void ClearHelperSideMouseClickMappings()
        {
            lock (helperSideMappingLock) { mouseClickRemaps.Clear(); }
        }

        internal void SetHelperSideMouseClickMapping(Labs.LegionInputButton button, int mouseButton)
        {
            lock (helperSideMappingLock) { mouseClickRemaps[button] = mouseButton; }
            Logger.Info($"Helper-side mouse click for {button}: button={mouseButton}");
        }

        internal bool TryGetMouseClickRemap(Labs.LegionInputButton button, out int mouseButton)
        {
            lock (helperSideMappingLock) { return mouseClickRemaps.TryGetValue(button, out mouseButton); }
        }

        internal static Labs.LegionInputButton? EdgeButtonForGamepadButton(GamepadButton button)
        {
            switch (button)
            {
                case GamepadButton.A: return Labs.LegionInputButton.A;
                case GamepadButton.B: return Labs.LegionInputButton.B;
                case GamepadButton.X: return Labs.LegionInputButton.X;
                case GamepadButton.Y: return Labs.LegionInputButton.Y;
                case GamepadButton.LT: return Labs.LegionInputButton.LeftTrigger;
                case GamepadButton.RT: return Labs.LegionInputButton.RightTrigger;
                case GamepadButton.LB: return Labs.LegionInputButton.LeftShoulder;
                case GamepadButton.RB: return Labs.LegionInputButton.RightShoulder;
                case GamepadButton.LSClick: return Labs.LegionInputButton.LeftThumb;
                case GamepadButton.RSClick: return Labs.LegionInputButton.RightThumb;
                case GamepadButton.DPadUp: return Labs.LegionInputButton.DpadUp;
                case GamepadButton.DPadDown: return Labs.LegionInputButton.DpadDown;
                case GamepadButton.DPadLeft: return Labs.LegionInputButton.DpadLeft;
                case GamepadButton.DPadRight: return Labs.LegionInputButton.DpadRight;
                case GamepadButton.Start: return Labs.LegionInputButton.Start;
                case GamepadButton.Select: return Labs.LegionInputButton.Back;
                default: return null;
            }
        }

        /// <summary>
        /// Hard off-switch for Desktop Controls when a game is running. Must disable firmware
        /// sticks and helper injection even if LegionDesktopControls was already false.
        /// </summary>
        public void ForceSuspendDesktopControlsForGame()
        {
            // Hold-for-mouse is an intentional in-game overlay — don't tear it down when
            // auto-disable re-asserts every poll tick.
            if (Program.IsLegionLHoldMouseActive())
                return;

            Program.StopDesktopAdminMouse();
            ClearHelperSideMouseClickMappings();
            joystickAsMouseMode = 0;

            ApplyJoystickAsMouse(true, false, joystickMouseSens);
            ApplyJoystickAsMouse(false, false, joystickMouseSens);

            // Reset mappings before clearing the flag so OnDesktopControlsChanged sees disable JSON.
            LegionGamepadMapping.ForceSetValue(DesktopControlsPreset.DisableMappingsJson);
            if (LegionDesktopControls.Value)
                LegionDesktopControls.ForceSetValue(false);
            else
                OnDesktopControlsChanged(false);

            LegionJoystickAsMouseMode.ForceSetValue(0);

            ApplyJoystickAsMouse(true, false, joystickMouseSens);
            ApplyJoystickAsMouse(false, false, joystickMouseSens);
            Logger.Info("ForceSuspendDesktopControlsForGame: desktop controls fully suspended");
        }

        public void ForceRestoreDesktopControlsAfterGame()
        {
            LegionJoystickAsMouseMode.ForceSetValue(2);
            LegionGamepadMapping.ForceSetValue(DesktopControlsPreset.EnableMappingsJson);
            LegionDesktopControls.ForceSetValue(true);
            ReapplyLegionSteamFirmwareMappings();
            Logger.Info("ForceRestoreDesktopControlsAfterGame: desktop controls restored");
        }

        /// <summary>
        /// SteamOS-style hold Legion L: apply desktop mouse layout without toggling
        /// the Desktop Controls property (works in games with desktop controls off).
        /// </summary>
        public void ApplyHoldMouseOverlay(bool desktopAlreadyOn)
        {
            SuppressFrontButtonFirmwareForHoldMouse();
            ApplyJoystickAsMouse(true, false, joystickMouseSens);
            ApplyJoystickAsMouse(false, false, joystickMouseSens);

            if (!desktopAlreadyOn)
            {
                ApplyGamepadButtonMappings(DesktopControlsPreset.EnableMappingsJson, forceHelperSideMouse: true);
                try { ControllerEmulation.Viiper.ViiperEmulationManager.SetDesktopControlsActive(true); }
                catch (Exception ex) { Logger.Warn($"Hold-mouse emu neutralize threw: {ex.Message}"); }
            }
        }

        /// <summary>Restore mappings after a hold-for-mouse session ends.</summary>
        public void RestoreAfterHoldMouseOverlay(bool desktopWasOn, string savedMappingJson, int savedJoystickMode)
        {
            RestoreFrontButtonFirmwareAfterHoldMouse();

            if (desktopWasOn) return;

            ClearHelperSideMouseClickMappings();
            ApplyGamepadButtonMappings(string.IsNullOrEmpty(savedMappingJson)
                ? DesktopControlsPreset.DisableMappingsJson
                : savedMappingJson);
            SetJoystickAsMouseMode(savedJoystickMode);
            try { ControllerEmulation.Viiper.ViiperEmulationManager.SetDesktopControlsActive(false); }
            catch (Exception ex) { Logger.Warn($"Hold-mouse emu restore threw: {ex.Message}"); }
        }

        public void OnLegionLHoldForMouseChanged(bool enabled)
        {
            try
            {
                Settings.LocalSettingsHelper.SetValue("LegionL_HoldForMouse", enabled);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to persist LegionL_HoldForMouse: {ex.Message}");
            }

            Labs.LegionButtonMonitor.Current?.ConfigureLegionLHoldForMouse(enabled);
            if (enabled)
            {
                Labs.LegionButtonMonitor.Current?.ConfigureButtonLongPress("L", false, 0, "");
                if (Program.IsLegionLHoldMouseActive())
                    Program.EndLegionLHoldMouseSession();
            }
        }

        /// <summary>
        /// Called when Desktop Controls is toggled. Re-applies mappings through the admin or
        /// firmware path and starts/stops the elevated mouse poll loop.
        /// </summary>
        public void OnDesktopControlsChanged(bool enabled)
        {
            if (enabled)
            {
                ApplyGamepadButtonMappings(LegionGamepadMapping.Value);
                SetJoystickAsMouseMode(2);
            }
            else
            {
                Program.StopDesktopAdminMouse();
                ClearHelperSideMouseClickMappings();

                ApplyGamepadButtonMappings(LegionGamepadMapping.Value);

                // Force off immediately; widget profile restore will re-send the saved
                // Global/per-game JoystickAsMouseMode if the user had legacy firmware mouse on.
                joystickAsMouseMode = 0;
                SetJoystickAsMouseMode(0);
            }
        }

        internal void SetHelperSideButtonMapping(Labs.LegionInputButton button, int? systemAction, int? scrollCode)
        {
            lock (helperSideMappingLock)
            {
                if (systemAction.HasValue) systemActionRemaps[button] = systemAction.Value;
                else systemActionRemaps.Remove(button);
                if (scrollCode.HasValue) scrollRepeatRemaps[button] = scrollCode.Value;
                else scrollRepeatRemaps.Remove(button);
            }
            Logger.Info($"Helper-side button mapping for {button}: system={(systemAction.HasValue ? systemAction.Value.ToString() : "-")}, scrollRepeat={(scrollCode.HasValue ? scrollCode.Value.ToString() : "-")}");
        }

        internal bool TryGetSystemActionRemap(Labs.LegionInputButton button, out int action)
        {
            lock (helperSideMappingLock) { return systemActionRemaps.TryGetValue(button, out action); }
        }

        internal bool TryGetScrollRepeatRemap(Labs.LegionInputButton button, out int scrollCode)
        {
            lock (helperSideMappingLock) { return scrollRepeatRemaps.TryGetValue(button, out scrollCode); }
        }

        /// <summary>
        /// Edge-stream identity of each remappable button. Physical mapping verified on
        /// hardware 2026-07-13 (Y1=ExtraL1, Y2=ExtraL2, Y3=ExtraR1, M1=ExtraRM1,
        /// M2=ExtraR3, M3=ExtraR2); Desktop/Page arrive as Mode/Share aux bits.
        /// </summary>
        internal static Labs.LegionInputButton EdgeButtonForSlot(int buttonIndex)
        {
            switch (buttonIndex)
            {
                case 0: return Labs.LegionInputButton.ExtraL1;   // Y1
                case 1: return Labs.LegionInputButton.ExtraL2;   // Y2
                case 2: return Labs.LegionInputButton.ExtraR1;   // Y3
                case 3: return Labs.LegionInputButton.ExtraRM1;  // M1
                case 4: return Labs.LegionInputButton.ExtraR3;   // M2
                case 5: return Labs.LegionInputButton.ExtraR2;   // M3
                default: return Labs.LegionInputButton.None;
            }
        }

        /// <summary>
        /// Applies gamepad button mappings from JSON.
        /// JSON format: {"LSClick":{"Type":1,"GamepadAction":3,"KeyboardKeys":[],"MouseButton":0},...}
        /// </summary>
        public void ApplyGamepadButtonMappings(string json, bool forceHelperSideMouse = false)
        {
            if (string.IsNullOrEmpty(json))
            {
                Logger.Debug("No gamepad button mappings to apply");
                return;
            }

            bool desktopAdminMouse = forceHelperSideMouse || (LegionDesktopControls?.Value == true);
            if (desktopAdminMouse)
                ClearHelperSideMouseClickMappings();

            try
            {
                using var controller = new LegionGoController();
                if (!controller.Connect())
                {
                    Logger.Warn("Cannot apply gamepad button mappings: controller not connected");
                    return;
                }

                // Parse the outer JSON to get button->mapping entries
                // Format: {"LSClick":{...},"A":{...},...}
                var buttonMatches = System.Text.RegularExpressions.Regex.Matches(json, "\"(\\w+)\"\\s*:\\s*(\\{[^}]+\\})");

                foreach (System.Text.RegularExpressions.Match match in buttonMatches)
                {
                    string buttonName = match.Groups[1].Value;
                    string mappingJson = match.Groups[2].Value;

                    // Try to parse the button name to GamepadButton enum
                    if (!Enum.TryParse<GamepadButton>(buttonName, out var button))
                    {
                        Logger.Warn($"Unknown gamepad button: {buttonName}");
                        continue;
                    }

                    // Parse the mapping using existing ButtonMappingParser
                    var (type, gamepadAction, keyboardKeys, mouseButton) = ButtonMappingParser.Parse(mappingJson);

                    // Desktop Controls: mouse clicks are injected helper-side (elevated InputInjector)
                    // so they work in admin windows. Clear firmware mapping for those buttons.
                    if (desktopAdminMouse && type == 2 && mouseButton >= 0 && mouseButton <= 2)
                    {
                        var edgeButton = EdgeButtonForGamepadButton(button);
                        controller.ClearGamepadButtonMapping(button);
                        System.Threading.Thread.Sleep(HID_COMMAND_DELAY_MS);
                        if (edgeButton.HasValue)
                            SetHelperSideMouseClickMapping(edgeButton.Value, mouseButton);
                        Logger.Info($"Applied helper-side desktop mouse for {buttonName}: mouseButton={mouseButton}");
                        continue;
                    }

                    // Apply the mapping
                    if (type == 0 && gamepadAction == 0)
                    {
                        // Reset button to default: first clear, then remap to itself
                        // Step 1: Clear the existing mapping
                        controller.ClearGamepadButtonMapping(button);
                        System.Threading.Thread.Sleep(HID_COMMAND_DELAY_MS); // Delay between clear and remap

                        // Step 2: Map button to itself (default behavior)
                        // This is needed for all buttons including sticks to restore axis properly
                        controller.SetGamepadButtonMappingAdvanced(button, MappingType.Gamepad, new byte[] { (byte)button });
                        System.Threading.Thread.Sleep(HID_COMMAND_DELAY_MS); // Delay after remap before next button (fixes stick range issues)
                        Logger.Info($"Reset gamepad button {buttonName} to default (cleared then mapped to self: 0x{(byte)button:X2})");
                    }
                    else
                    {
                        var mappingType = (MappingType)(type + 1); // 0->Gamepad(1), 1->Keyboard(2), 2->Mouse(3)
                        byte[] mappings;

                        if (type == 0)
                        {
                            // Gamepad: use RemapActionHelper to convert dropdown index to HID button code
                            var action = RemapActionHelper.GetByIndex(gamepadAction);
                            mappings = new byte[] { (byte)action };
                        }
                        else if (type == 1)
                        {
                            // Keyboard: use raw key codes
                            mappings = keyboardKeys?.Select(k => (byte)k).ToArray() ?? Array.Empty<byte>();
                        }
                        else
                        {
                            // Mouse: convert dropdown index (0=Left, 1=Right, 2=Middle) to HID code (0x01, 0x02, 0x03)
                            mappings = new byte[] { (byte)(mouseButton + 1) };
                        }

                        if (mappings.Length > 0)
                        {
                            controller.SetGamepadButtonMappingAdvanced(button, mappingType, mappings);
                            System.Threading.Thread.Sleep(HID_COMMAND_DELAY_MS); // Delay after each mapping for firmware to process
                            Logger.Info($"Applied gamepad button {buttonName} mapping: type={mappingType}, values=[{string.Join(",", mappings.Select(b => $"0x{b:X2}"))}]");
                        }
                    }
                }

                Logger.Info($"Applied gamepad button mappings successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error applying gamepad button mappings: {ex.Message}");
            }
        }

        /// <summary>
        /// While Legion L is held for mouse mode, silence stock Desktop/Page firmware
        /// shortcuts (Win+D / Win+Tab) that would otherwise fire for the whole hold.
        /// </summary>
        public void SuppressFrontButtonFirmwareForHoldMouse()
        {
            try
            {
                using var controller = new LegionGoController();
                if (!controller.Connect())
                {
                    Logger.Warn("Hold-mouse: cannot suppress front buttons — controller not connected");
                    return;
                }

                controller.DisableGamepadButtonMapping(GamepadButton.DesktopButton);
                System.Threading.Thread.Sleep(HID_COMMAND_DELAY_MS);
                controller.DisableGamepadButtonMapping(GamepadButton.PageButton);
                Logger.Info("Hold-mouse: suppressed Desktop/Page firmware shortcuts");
            }
            catch (Exception ex)
            {
                Logger.Warn($"SuppressFrontButtonFirmwareForHoldMouse failed: {ex.Message}");
            }
        }

        /// <summary>Restore Desktop/Page firmware after a hold-for-mouse session.</summary>
        public void RestoreFrontButtonFirmwareAfterHoldMouse()
        {
            try
            {
                RestoreSingleFrontButtonMapping(LegionButtonDesktop?.Value, GamepadButton.DesktopButton);
                System.Threading.Thread.Sleep(HID_COMMAND_DELAY_MS);
                RestoreSingleFrontButtonMapping(LegionButtonPage?.Value, GamepadButton.PageButton);
                ReapplyLegionSteamFirmwareMappings();
            }
            catch (Exception ex)
            {
                Logger.Warn($"RestoreFrontButtonFirmwareAfterHoldMouse failed: {ex.Message}");
            }
        }

        private void RestoreSingleFrontButtonMapping(string mappingJson, GamepadButton button)
        {
            if (!string.IsNullOrEmpty(mappingJson))
            {
                var (type, gamepadAction, keyboardKeys, mouseButton) = ButtonMappingParser.Parse(mappingJson);
                SetLegionButtonMapping(
                    button,
                    type,
                    ButtonMappingParser.GetMappingValues(type, gamepadAction, keyboardKeys, mouseButton),
                    out _);
                return;
            }

            using var controller = new LegionGoController();
            if (!controller.Connect()) return;
            controller.SetGamepadButtonMappingAdvanced(button, MappingType.Gamepad, new byte[] { (byte)button });
        }

    }
}
