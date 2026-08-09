using Microsoft.Gaming.XboxGameBar;
using Microsoft.Gaming.XboxGameBar.Input;
using Microsoft.UI.Xaml.Controls;
using NLog;
using Shared.Data;
using Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.System.Power;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml.Input;
using System.Runtime.InteropServices;
using Windows.UI;
using XboxGamingBar.Data;
using XboxGamingBar.Event;
using XboxGamingBar.IPC;
using XboxGamingBar.QuickSettings;
using Shared.Enums;

namespace XboxGamingBar
{
    public sealed partial class GamingWidget
    {
        // Storage for selected keys (used for hotkeys, Legion buttons, scroll wheel, custom shortcuts)
        private Dictionary<string, List<int>> _selectedKeys = new Dictionary<string, List<int>>();
        private List<int> _customShortcutKeys = new List<int>();

        private List<int> GetSelectedKeys(string keyName)
        {
            if (!_selectedKeys.ContainsKey(keyName))
                _selectedKeys[keyName] = new List<int>();
            return _selectedKeys[keyName];
        }

        private void AddKeyToSelection(string keyName, int keyCode, ItemsControl keyTags, ComboBox keyComboBox, Action onKeysChanged = null)
        {
            var keys = GetSelectedKeys(keyName);
            if (keys.Count >= 5) return; // Max 5 keys
            if (!keys.Contains(keyCode) && keyCode > 0)
            {
                keys.Add(keyCode);
                UpdateKeyTagsDisplay(keyName, keyTags, onKeysChanged);
                onKeysChanged?.Invoke();
            }
            if (keyComboBox != null)
                keyComboBox.SelectedIndex = 0;
        }

        private void RemoveKeyFromSelection(string keyName, int keyCode, ItemsControl keyTags, Action onKeysChanged = null)
        {
            var keys = GetSelectedKeys(keyName);
            keys.Remove(keyCode);
            UpdateKeyTagsDisplay(keyName, keyTags, onKeysChanged);
            onKeysChanged?.Invoke();
        }

        private void UpdateKeyTagsDisplay(string keyName, ItemsControl keyTags, Action onKeysChanged = null)
        {
            if (keyTags == null) return;
            keyTags.Items.Clear();
            var keys = GetSelectedKeys(keyName);
            foreach (var key in keys)
            {
                var tagBorder = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 60, 60, 60)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 0, 4, 0)
                };
                var tagPanel = new StackPanel { Orientation = Orientation.Horizontal };
                var keyText = new TextBlock
                {
                    Text = GetKeyDisplayName(key),
                    Foreground = new SolidColorBrush(Windows.UI.Colors.White),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var removeButton = new Button
                {
                    Content = "×",
                    FontSize = 10,
                    Padding = new Thickness(4, 0, 0, 0),
                    Background = new SolidColorBrush(Windows.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 180, 180, 180)),
                    VerticalAlignment = VerticalAlignment.Center,
                    MinWidth = 0,
                    MinHeight = 0
                };
                int keyCode = key;
                removeButton.Click += (s, e) => RemoveKeyFromSelection(keyName, keyCode, keyTags, onKeysChanged);
                tagPanel.Children.Add(keyText);
                tagPanel.Children.Add(removeButton);
                tagBorder.Child = tagPanel;
                keyTags.Items.Add(tagBorder);
            }
        }

        private string GetKeysAsString(string keyName)
        {
            var keys = GetSelectedKeys(keyName);
            if (keys.Count == 0) return "";
            return string.Join("+", keys.Select(k => GetKeyDisplayName(k)));
        }

        private void LoadKeysFromString(string keyName, string keysString, ItemsControl keyTags)
        {
            var keys = GetSelectedKeys(keyName);
            keys.Clear();
            if (!string.IsNullOrEmpty(keysString))
            {
                var parts = keysString.Split('+');
                foreach (var part in parts)
                {
                    int keyCode = GetKeyCodeFromDisplayName(part.Trim());
                    if (keyCode > 0)
                        keys.Add(keyCode);
                }
            }
            UpdateKeyTagsDisplay(keyName, keyTags);
        }

        private int GetKeyCodeFromDisplayName(string name) =>
            Shared.Input.HidKeyboardCatalog.GetCodeFromDisplayName(name);

        // Hotkey key selection handlers
        private void HotkeyMenuAKey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isLoadingHotkeys || HotkeyMenuAKeyComboBox?.SelectedIndex <= 0) return;
            int keyCode = GetKeyCodeFromComboBox(HotkeyMenuAKeyComboBox);
            AddKeyToSelection("HotkeyMenuA", keyCode, HotkeyMenuAKeyTags, HotkeyMenuAKeyComboBox, () => SaveHotkeyKeys("MenuA", "HotkeyMenuA"));
        }

        private void HotkeyMenuBKey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isLoadingHotkeys || HotkeyMenuBKeyComboBox?.SelectedIndex <= 0) return;
            int keyCode = GetKeyCodeFromComboBox(HotkeyMenuBKeyComboBox);
            AddKeyToSelection("HotkeyMenuB", keyCode, HotkeyMenuBKeyTags, HotkeyMenuBKeyComboBox, () => SaveHotkeyKeys("MenuB", "HotkeyMenuB"));
        }

        private void HotkeyMenuXKey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isLoadingHotkeys || HotkeyMenuXKeyComboBox?.SelectedIndex <= 0) return;
            int keyCode = GetKeyCodeFromComboBox(HotkeyMenuXKeyComboBox);
            AddKeyToSelection("HotkeyMenuX", keyCode, HotkeyMenuXKeyTags, HotkeyMenuXKeyComboBox, () => SaveHotkeyKeys("MenuX", "HotkeyMenuX"));
        }

        private void HotkeyMenuYKey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isLoadingHotkeys || HotkeyMenuYKeyComboBox?.SelectedIndex <= 0) return;
            int keyCode = GetKeyCodeFromComboBox(HotkeyMenuYKeyComboBox);
            AddKeyToSelection("HotkeyMenuY", keyCode, HotkeyMenuYKeyTags, HotkeyMenuYKeyComboBox, () => SaveHotkeyKeys("MenuY", "HotkeyMenuY"));
        }

        private void HotkeyMenuDpadUpKey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isLoadingHotkeys || HotkeyMenuDpadUpKeyComboBox?.SelectedIndex <= 0) return;
            int keyCode = GetKeyCodeFromComboBox(HotkeyMenuDpadUpKeyComboBox);
            AddKeyToSelection("HotkeyMenuDpadUp", keyCode, HotkeyMenuDpadUpKeyTags, HotkeyMenuDpadUpKeyComboBox, () => SaveHotkeyKeys("MenuDpadUp", "HotkeyMenuDpadUp"));
        }

        private void HotkeyMenuDpadDownKey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isLoadingHotkeys || HotkeyMenuDpadDownKeyComboBox?.SelectedIndex <= 0) return;
            int keyCode = GetKeyCodeFromComboBox(HotkeyMenuDpadDownKeyComboBox);
            AddKeyToSelection("HotkeyMenuDpadDown", keyCode, HotkeyMenuDpadDownKeyTags, HotkeyMenuDpadDownKeyComboBox, () => SaveHotkeyKeys("MenuDpadDown", "HotkeyMenuDpadDown"));
        }

        private void HotkeyMenuDpadLeftKey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isLoadingHotkeys || HotkeyMenuDpadLeftKeyComboBox?.SelectedIndex <= 0) return;
            int keyCode = GetKeyCodeFromComboBox(HotkeyMenuDpadLeftKeyComboBox);
            AddKeyToSelection("HotkeyMenuDpadLeft", keyCode, HotkeyMenuDpadLeftKeyTags, HotkeyMenuDpadLeftKeyComboBox, () => SaveHotkeyKeys("MenuDpadLeft", "HotkeyMenuDpadLeft"));
        }

        private void HotkeyMenuDpadRightKey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isLoadingHotkeys || HotkeyMenuDpadRightKeyComboBox?.SelectedIndex <= 0) return;
            int keyCode = GetKeyCodeFromComboBox(HotkeyMenuDpadRightKeyComboBox);
            AddKeyToSelection("HotkeyMenuDpadRight", keyCode, HotkeyMenuDpadRightKeyTags, HotkeyMenuDpadRightKeyComboBox, () => SaveHotkeyKeys("MenuDpadRight", "HotkeyMenuDpadRight"));
        }

        private void SaveHotkeyKeys(string hotkeyName, string keyStorageName)
        {
            var keysString = GetKeysAsString(keyStorageName);
            ApplicationData.Current.LocalSettings.Values[$"Hotkey_{hotkeyName}_Key"] = keysString;
            Logger.Info($"Hotkey {hotkeyName} keys saved: {keysString}");

            // Sync updated config to helper so its XInput monitor uses the new key
            SendControllerHotkeyConfigToHelper();
        }

        // Legion L/R key selection handlers
        private void LegionLKeyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LegionLKeyComboBox?.SelectedIndex <= 0) return;
            int keyCode = GetKeyCodeFromComboBox(LegionLKeyComboBox);
            AddKeyToSelection("LegionL", keyCode, LegionLKeyTags, LegionLKeyComboBox, SaveLegionLKeys);
        }

        private void LegionRKeyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LegionRKeyComboBox?.SelectedIndex <= 0) return;
            int keyCode = GetKeyCodeFromComboBox(LegionRKeyComboBox);
            AddKeyToSelection("LegionR", keyCode, LegionRKeyTags, LegionRKeyComboBox, SaveLegionRKeys);
        }

        private void SaveLegionLKeys()
        {
            var keysString = GetKeysAsString("LegionL");
            ApplicationData.Current.LocalSettings.Values["LegionL_Shortcut"] = keysString;
            SaveLegionRemapSettings();
            ApplyLegionButtonConfig(true);
        }

        private void SaveLegionRKeys()
        {
            var keysString = GetKeysAsString("LegionR");
            ApplicationData.Current.LocalSettings.Values["LegionR_Shortcut"] = keysString;
            SaveLegionRemapSettings();
            ApplyLegionButtonConfig(false);
        }

        // Long-press variants of the Legion L/R key pickers.
        private void LegionLLongKeyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = FindName("LegionLLongKeyComboBox") as ComboBox;
            if (combo?.SelectedIndex <= 0) return;
            int keyCode = GetKeyCodeFromComboBox(combo);
            AddKeyToSelection("LegionLLong", keyCode, FindName("LegionLLongKeyTags") as ItemsControl, combo, SaveLegionLLongKeys);
        }

        private void LegionRLongKeyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = FindName("LegionRLongKeyComboBox") as ComboBox;
            if (combo?.SelectedIndex <= 0) return;
            int keyCode = GetKeyCodeFromComboBox(combo);
            AddKeyToSelection("LegionRLong", keyCode, FindName("LegionRLongKeyTags") as ItemsControl, combo, SaveLegionRLongKeys);
        }

        private void SaveLegionLLongKeys()
        {
            ApplicationData.Current.LocalSettings.Values["LegionL_LongShortcut"] = GetKeysAsString("LegionLLong");
            SaveLegionRemapSettings();
            ApplyLegionButtonLongConfig(true);
        }

        private void SaveLegionRLongKeys()
        {
            ApplicationData.Current.LocalSettings.Values["LegionR_LongShortcut"] = GetKeysAsString("LegionRLong");
            SaveLegionRemapSettings();
            ApplyLegionButtonLongConfig(false);
        }

        // Scroll wheel key selection handlers
        private void ScrollKeyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScrollKeyComboBox?.SelectedIndex <= 0) return;
            int keyCode = GetKeyCodeFromComboBox(ScrollKeyComboBox);
            AddKeyToSelection("Scroll", keyCode, ScrollKeyTags, ScrollKeyComboBox, SaveScrollKeys);
        }

        private void ScrollClickKeyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScrollClickKeyComboBox?.SelectedIndex <= 0) return;
            int keyCode = GetKeyCodeFromComboBox(ScrollClickKeyComboBox);
            AddKeyToSelection("ScrollClick", keyCode, ScrollClickKeyTags, ScrollClickKeyComboBox, SaveScrollClickKeys);
        }

        private void SaveScrollKeys()
        {
            var keysString = GetKeysAsString("Scroll");
            ApplicationData.Current.LocalSettings.Values["Scroll_Shortcut"] = keysString;
            SaveScrollRemapSettings();
            ApplyScrollWheelConfig("Scroll");
        }

        private void SaveScrollClickKeys()
        {
            var keysString = GetKeysAsString("ScrollClick");
            ApplicationData.Current.LocalSettings.Values["ScrollClick_Shortcut"] = keysString;
            SaveScrollRemapSettings();
            ApplyScrollWheelConfig("Click");
        }

        // Custom shortcut key selection handler
        private void CustomShortcutKey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CustomShortcutKeyComboBox?.SelectedIndex <= 0) return;
            int keyCode = GetKeyCodeFromComboBox(CustomShortcutKeyComboBox);
            if (_customShortcutKeys.Count < 5 && !_customShortcutKeys.Contains(keyCode) && keyCode > 0)
            {
                _customShortcutKeys.Add(keyCode);
                UpdateCustomShortcutKeyTags();
            }
            CustomShortcutKeyComboBox.SelectedIndex = 0;
        }

        private void UpdateCustomShortcutKeyTags()
        {
            if (CustomShortcutKeyTags == null) return;
            CustomShortcutKeyTags.Items.Clear();
            foreach (var key in _customShortcutKeys)
            {
                var tagBorder = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 60, 60, 60)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 0, 4, 0)
                };
                var tagPanel = new StackPanel { Orientation = Orientation.Horizontal };
                var keyText = new TextBlock
                {
                    Text = GetKeyDisplayName(key),
                    Foreground = new SolidColorBrush(Windows.UI.Colors.White),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var removeButton = new Button
                {
                    Content = "×",
                    FontSize = 10,
                    Padding = new Thickness(4, 0, 0, 0),
                    Background = new SolidColorBrush(Windows.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 180, 180, 180)),
                    VerticalAlignment = VerticalAlignment.Center,
                    MinWidth = 0,
                    MinHeight = 0
                };
                int keyCode = key;
                removeButton.Click += (s, args) => { _customShortcutKeys.Remove(keyCode); UpdateCustomShortcutKeyTags(); };
                tagPanel.Children.Add(keyText);
                tagPanel.Children.Add(removeButton);
                tagBorder.Child = tagPanel;
                CustomShortcutKeyTags.Items.Add(tagBorder);
            }
        }

        private string GetCustomShortcutKeysString()
        {
            if (_customShortcutKeys.Count == 0) return "";
            return string.Join("+", _customShortcutKeys.Select(k => GetKeyDisplayName(k)));
        }

    }
}
