using Shared.Enums;
using Windows.UI.Xaml.Controls;

namespace XboxGamingBar.Data
{
    internal class LegionSteamInputModeProperty : WidgetToggleProperty
    {
        public LegionSteamInputModeProperty(ToggleSwitch inUI, Page inOwner) : base(false, Function.Settings_LegionSteamInputMode, inUI, inOwner)
        {
        }
    }
}
