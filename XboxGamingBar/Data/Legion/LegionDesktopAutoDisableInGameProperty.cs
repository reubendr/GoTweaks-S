using Shared.Enums;
using Windows.UI.Xaml.Controls;

namespace XboxGamingBar.Data
{
    internal class LegionDesktopAutoDisableInGameProperty : WidgetToggleProperty
    {
        public LegionDesktopAutoDisableInGameProperty(ToggleSwitch inUI, Page inOwner)
            : base(true, Function.LegionDesktopAutoDisableInGame, inUI, inOwner)
        {
        }
    }
}
