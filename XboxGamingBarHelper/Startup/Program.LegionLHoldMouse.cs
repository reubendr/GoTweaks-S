using NLog;
using System;

namespace XboxGamingBarHelper
{
    internal partial class Program
    {
        private static readonly Logger HoldMouseLogger = LogManager.GetCurrentClassLogger();

        private static bool _legionLHoldMouseActive;
        private static bool _holdMouseSavedDesktopControls;
        private static string _holdMouseSavedMapping;
        private static int _holdMouseSavedJoystickMode;

        internal static bool IsLegionLHoldMouseActive() => _legionLHoldMouseActive;

        internal static bool IsDesktopAdminMouseAllowed()
        {
            return legionManager != null
                && (legionManager.LegionDesktopControls.Value || _legionLHoldMouseActive);
        }

        internal static void BeginLegionLHoldMouseSession()
        {
            if (_legionLHoldMouseActive || legionManager == null) return;

            _holdMouseSavedDesktopControls = legionManager.LegionDesktopControls.Value;
            _holdMouseSavedMapping = legionManager.LegionGamepadMapping.Value ?? "";
            _holdMouseSavedJoystickMode = legionManager.LegionJoystickAsMouseMode?.Value ?? 0;

            _legionLHoldMouseActive = true;
            legionManager.ApplyHoldMouseOverlay(_holdMouseSavedDesktopControls);

            int sens = legionManager.LegionJoystickMouseSens?.Value ?? 50;
            Windows.User32.BoostHoldMouseCursorVisibility(4);
            StartDesktopAdminMouse(2, sens);
            HoldMouseLogger.Info("Legion L hold-for-mouse: session started");
        }

        internal static void EndLegionLHoldMouseSession()
        {
            if (!_legionLHoldMouseActive || legionManager == null) return;

            _legionLHoldMouseActive = false;
            Windows.User32.ReleaseHoldMouseCursorBoost();
            StopDesktopAdminMouse();
            legionManager.RestoreAfterHoldMouseOverlay(
                _holdMouseSavedDesktopControls,
                _holdMouseSavedMapping,
                _holdMouseSavedJoystickMode);

            if (_holdMouseSavedDesktopControls)
            {
                int mode = legionManager.LegionJoystickAsMouseMode?.Value ?? 2;
                int sens = legionManager.LegionJoystickMouseSens?.Value ?? 50;
                StartDesktopAdminMouse(mode, sens);
            }

            HoldMouseLogger.Info("Legion L hold-for-mouse: session ended");
        }
    }
}
