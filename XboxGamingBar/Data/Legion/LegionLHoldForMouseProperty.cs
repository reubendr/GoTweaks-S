using Shared.Enums;
using Windows.UI.Xaml.Controls;

namespace XboxGamingBar.Data
{
    internal class LegionLHoldForMouseProperty : WidgetToggleProperty
    {
        public LegionLHoldForMouseProperty(ToggleSwitch inUI, Page inOwner)
            : base(false, Function.LegionLHoldForMouse, inUI, inOwner)
        {
        }
    }
}
