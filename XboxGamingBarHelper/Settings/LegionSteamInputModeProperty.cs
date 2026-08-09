using Shared.Enums;
using XboxGamingBarHelper.Core;
using XboxGamingBarHelper.Labs;

namespace XboxGamingBarHelper.Settings
{
    internal class LegionSteamInputModeProperty : HelperProperty<bool, SettingsManager>
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public LegionSteamInputModeProperty(SettingsManager inManager) : base(false, null, Function.Settings_LegionSteamInputMode, inManager)
        {
        }

        protected override void NotifyPropertyChanged(string propertyName = "")
        {
            base.NotifyPropertyChanged(propertyName);

            Logger.Info($"Legion Steam Input mode changed to {Value}. Re-applying physical XInput state.");
            if (Value)
            {
                // Steam Input + Legion L/R + back buttons mode: force physical XInput ON.
                LegionButtonMonitor.Current?.SetPhysicalXInputEnabled(true);
            }
            else
            {
                // Revert to the last requested physical XInput state (driven by LegionOsReportingDisabled).
                bool lastRequested = LegionButtonMonitor.Current?.RequestedPhysicalXInputEnabled ?? true;
                LegionButtonMonitor.Current?.SetPhysicalXInputEnabled(lastRequested);
            }
        }
    }
}
