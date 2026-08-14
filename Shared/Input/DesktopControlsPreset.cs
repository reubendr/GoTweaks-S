namespace Shared.Input
{
    /// <summary>
    /// Built-in Desktop Controls layout (Steam Input desktop companion):
    /// RS = mouse, LS = scroll, LT = right click, RT = left click,
    /// B = Esc, X = Enter, Y = Tab, LB/RB = browser back/forward.
    /// D-Pad and A are left unmapped so Steam Input's XInput desktop layout owns them.
    /// Mouse clicks and stick cursor are driven helper-side (elevated InputInjector).
    /// </summary>
    public static class DesktopControlsPreset
    {
        public const string EnableMappingsJson = @"{
            ""DPadUp"":{""Type"":0,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0},
            ""DPadDown"":{""Type"":0,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0},
            ""DPadLeft"":{""Type"":0,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0},
            ""DPadRight"":{""Type"":0,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0},
            ""LSClick"":{""Type"":1,""GamepadAction"":0,""KeyboardKeys"":[227],""MouseButton"":0},
            ""A"":{""Type"":0,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0},
            ""B"":{""Type"":1,""GamepadAction"":0,""KeyboardKeys"":[41],""MouseButton"":0},
            ""X"":{""Type"":1,""GamepadAction"":0,""KeyboardKeys"":[40],""MouseButton"":0},
            ""Y"":{""Type"":1,""GamepadAction"":0,""KeyboardKeys"":[43],""MouseButton"":0},
            ""LB"":{""Type"":1,""GamepadAction"":0,""KeyboardKeys"":[226,80],""MouseButton"":0},
            ""RB"":{""Type"":1,""GamepadAction"":0,""KeyboardKeys"":[226,79],""MouseButton"":0},
            ""LT"":{""Type"":2,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":1},
            ""RT"":{""Type"":2,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0}
        }";

        public const string DisableMappingsJson = @"{
            ""DPadUp"":{""Type"":0,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0},
            ""DPadDown"":{""Type"":0,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0},
            ""DPadLeft"":{""Type"":0,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0},
            ""DPadRight"":{""Type"":0,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0},
            ""LSClick"":{""Type"":0,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0},
            ""A"":{""Type"":0,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0},
            ""B"":{""Type"":0,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0},
            ""X"":{""Type"":0,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0},
            ""Y"":{""Type"":0,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0},
            ""LB"":{""Type"":0,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0},
            ""RB"":{""Type"":0,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0},
            ""LT"":{""Type"":0,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0},
            ""RT"":{""Type"":0,""GamepadAction"":0,""KeyboardKeys"":[],""MouseButton"":0}
        }";
    }
}
