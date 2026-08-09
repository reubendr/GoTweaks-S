using System;
using System.Collections.Generic;
using System.Linq;
using Windows.System;

namespace Shared.Input
{
    /// <summary>
    /// USB HID keyboard usage codes (0x04–0xE7, numpad, media) for controller→keyboard remapping.
    /// Single source of truth for widget pickers and saved profile strings.
    /// </summary>
    public static class HidKeyboardCatalog
    {
        public const string PlaceholderLabel = "+ Key";

        private static readonly (int Code, string Label)[] Entries =
        {
            // Letters
            (0x04, "A"), (0x05, "B"), (0x06, "C"), (0x07, "D"), (0x08, "E"), (0x09, "F"),
            (0x0A, "G"), (0x0B, "H"), (0x0C, "I"), (0x0D, "J"), (0x0E, "K"), (0x0F, "L"),
            (0x10, "M"), (0x11, "N"), (0x12, "O"), (0x13, "P"), (0x14, "Q"), (0x15, "R"),
            (0x16, "S"), (0x17, "T"), (0x18, "U"), (0x19, "V"), (0x1A, "W"), (0x1B, "X"),
            (0x1C, "Y"), (0x1D, "Z"),
            // Numbers
            (0x1E, "1"), (0x1F, "2"), (0x20, "3"), (0x21, "4"), (0x22, "5"), (0x23, "6"),
            (0x24, "7"), (0x25, "8"), (0x26, "9"), (0x27, "0"),
            // Control / punctuation
            (0x28, "Enter"), (0x29, "Esc"), (0x2A, "Backspace"), (0x2B, "Tab"), (0x2C, "Space"),
            (0x2D, "-"), (0x2E, "="), (0x2F, "["), (0x30, "]"), (0x31, "\\"),
            (0x33, ";"), (0x34, "'"), (0x35, "`"), (0x36, ","), (0x37, "."), (0x38, "/"),
            (0x39, "CapsLock"),
            // Function
            (0x3A, "F1"), (0x3B, "F2"), (0x3C, "F3"), (0x3D, "F4"), (0x3E, "F5"), (0x3F, "F6"),
            (0x40, "F7"), (0x41, "F8"), (0x42, "F9"), (0x43, "F10"), (0x44, "F11"), (0x45, "F12"),
            // Navigation / lock
            (0x46, "PrtSc"), (0x47, "ScrLk"), (0x48, "Pause"),
            (0x49, "Ins"), (0x4A, "Home"), (0x4B, "PgUp"),
            (0x4C, "Del"), (0x4D, "End"), (0x4E, "PgDn"),
            (0x4F, "Right"), (0x50, "Left"), (0x51, "Down"), (0x52, "Up"),
            // Numpad
            (0x53, "NumLock"), (0x54, "Num /"), (0x55, "Num *"), (0x56, "Num -"), (0x57, "Num +"),
            (0x58, "Num Enter"),
            (0x59, "Num 1"), (0x5A, "Num 2"), (0x5B, "Num 3"), (0x5C, "Num 4"), (0x5D, "Num 5"),
            (0x5E, "Num 6"), (0x5F, "Num 7"), (0x60, "Num 8"), (0x61, "Num 9"), (0x62, "Num 0"),
            (0x63, "Num ."),
            // Media (HID keyboard page)
            (0x7F, "VolMute"), (0x80, "VolUp"), (0x81, "VolDown"),
            // Modifiers
            (0xE0, "LCtrl"), (0xE1, "LShift"), (0xE2, "LAlt"), (0xE3, "LWin"),
            (0xE4, "RCtrl"), (0xE5, "RShift"), (0xE6, "RAlt"), (0xE7, "RWin"),
        };

        private static readonly Dictionary<int, string> CodeToLabel;
        private static readonly Dictionary<string, int> LabelToCode;

        private static readonly Dictionary<int, int> VirtualKeyToHid;

        static HidKeyboardCatalog()
        {
            CodeToLabel = new Dictionary<int, string>();
            LabelToCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            VirtualKeyToHid = new Dictionary<int, int>();

            foreach (var (code, label) in Entries)
            {
                CodeToLabel[code] = label;
                LabelToCode[label] = code;
            }

            // Aliases for saved profiles / alternate spellings
            void Alias(string alias, string canonical) =>
                LabelToCode[alias] = LabelToCode[canonical];

            Alias("Escape", "Esc");
            Alias("Delete", "Del");
            Alias("Insert", "Ins");
            Alias("PrintScr", "PrtSc");
            Alias("PrintScreen", "PrtSc");
            Alias("ScrollLock", "ScrLk");
            Alias("PageUp", "PgUp");
            Alias("PageDown", "PgDn");
            Alias("LeftBracket", "[");
            Alias("RightBracket", "]");
            Alias("Backslash", "\\");
            Alias("Semicolon", ";");
            Alias("Apostrophe", "'");
            Alias("Quote", "'");
            Alias("Grave", "`");
            Alias("Comma", ",");
            Alias("Period", ".");
            Alias("Slash", "/");
            Alias("Minus", "-");
            Alias("Equals", "=");
            Alias("LMeta", "LWin");
            Alias("RMeta", "RWin");
            Alias("NumPad /", "Num /");
            Alias("NumPad *", "Num *");
            Alias("NumPad -", "Num -");
            Alias("NumPad +", "Num +");
            Alias("NumPad Enter", "Num Enter");
            Alias("NumPad .", "Num .");
            for (int d = 0; d <= 9; d++)
                Alias($"NumPad {d}", $"Num {d}");

            // Windows virtual-key codes (int) → USB HID usage codes.
            for (int vk = 0x41; vk <= 0x5A; vk++)
                VirtualKeyToHid[vk] = 0x04 + (vk - 0x41);
            for (int vk = 0x31; vk <= 0x39; vk++)
                VirtualKeyToHid[vk] = 0x1E + (vk - 0x31);
            VirtualKeyToHid[0x30] = 0x27;
            for (int vk = 0x70; vk <= 0x7B; vk++)
                VirtualKeyToHid[vk] = 0x3A + (vk - 0x70);
            for (int vk = 0x61; vk <= 0x69; vk++)
                VirtualKeyToHid[vk] = 0x59 + (vk - 0x61);
            VirtualKeyToHid[0x60] = 0x62;

            void MapVk(int vk, int hid) => VirtualKeyToHid[vk] = hid;
            MapVk(0x0D, 0x28); MapVk(0x1B, 0x29); MapVk(0x08, 0x2A); MapVk(0x09, 0x2B);
            MapVk(0x20, 0x2C); MapVk(0xBD, 0x2D); MapVk(0xBB, 0x2E);
            MapVk(0xDB, 0x2F); MapVk(0xDD, 0x30); MapVk(0xDC, 0x31);
            MapVk(0xBA, 0x33); MapVk(0xDE, 0x34); MapVk(0xC0, 0x35);
            MapVk(0xBC, 0x36); MapVk(0xBE, 0x37); MapVk(0xBF, 0x38);
            MapVk(0x14, 0x39); MapVk(0x2C, 0x46); MapVk(0x91, 0x47); MapVk(0x13, 0x48);
            MapVk(0x2D, 0x49); MapVk(0x24, 0x4A); MapVk(0x21, 0x4B);
            MapVk(0x2E, 0x4C); MapVk(0x23, 0x4D); MapVk(0x22, 0x4E);
            MapVk(0x27, 0x4F); MapVk(0x25, 0x50); MapVk(0x28, 0x51); MapVk(0x26, 0x52);
            MapVk(0x90, 0x53); MapVk(0x6F, 0x54); MapVk(0x6A, 0x55);
            MapVk(0x6D, 0x56); MapVk(0x6B, 0x57); MapVk(0x6E, 0x63);
            MapVk(0xA2, 0xE0); MapVk(0xA3, 0xE4); MapVk(0xA0, 0xE1); MapVk(0xA1, 0xE5);
            MapVk(0xA4, 0xE2); MapVk(0xA5, 0xE6); MapVk(0x5B, 0xE3); MapVk(0x5C, 0xE7);
            MapVk(0x11, 0xE0); MapVk(0x10, 0xE1); MapVk(0x12, 0xE2); // generic ctrl/shift/alt
            MapVk(0xAD, 0x7F); MapVk(0xAF, 0x80); MapVk(0xAE, 0x81);
        }

        public static IReadOnlyList<string> ComboLabels { get; } =
            new[] { PlaceholderLabel }.Concat(Entries.Select(e => e.Label)).ToList();

        public static string[][] KeyboardLayoutRows { get; } =
        {
            new[] { "Esc", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
                    "PrtSc", "ScrLk", "Pause" },
            new[] { "`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "Backspace" },
            new[] { "Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]", "\\" },
            new[] { "CapsLock", "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'", "Enter" },
            new[] { "LShift", "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/", "RShift" },
            new[] { "LCtrl", "LWin", "LAlt", "Space", "RAlt", "RWin", "RCtrl", "Left", "Up", "Down", "Right" },
            new[] { "Ins", "Home", "PgUp", "Del", "End", "PgDn" },
            new[] { "NumLock", "Num /", "Num *", "Num -", "Num 7", "Num 8", "Num 9", "Num +",
                    "Num 4", "Num 5", "Num 6", "Num Enter", "Num 1", "Num 2", "Num 3", "Num 0", "Num ." },
            new[] { "VolMute", "VolUp", "VolDown" },
        };

        public static string GetDisplayName(int code) =>
            CodeToLabel.TryGetValue(code, out var label) ? label : $"0x{code:X2}";

        public static int GetCodeFromDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == PlaceholderLabel) return 0;
            return LabelToCode.TryGetValue(name.Trim(), out var code) ? code : 0;
        }

        public static int GetCodeFromComboItem(object item)
        {
            if (item is string label) return GetCodeFromDisplayName(label);
            return 0;
        }

        public static bool TryGetCodeFromVirtualKey(VirtualKey vk, out int code) =>
            VirtualKeyToHid.TryGetValue((int)vk, out code);
    }
}
