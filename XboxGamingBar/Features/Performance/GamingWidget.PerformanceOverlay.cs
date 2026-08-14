using Microsoft.Gaming.XboxGameBar;
using Microsoft.Gaming.XboxGameBar.Input;
using Microsoft.UI.Xaml.Controls;
using NLog;
using Shared.Data;
using Shared.Utilities;
using Shared.Constants;
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

        private void PerformanceOverlayComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PerformanceOverlayComboBox != null && PerformanceOverlaySlider != null)
            {
                // Sync the hidden slider value with the selected combobox item
                int index = PerformanceOverlayComboBox.SelectedIndex;
                if (index >= 0)
                {
                    // (AMD Adrenalin overlay pass-through removed - RTSS is the
                    // only OSD provider.)
                    PerformanceOverlaySlider.Value = index;
                    // Save the setting (but not during initial load)
                    if (!isLoadingPerformanceOverlaySetting)
                    {
                        SavePerformanceOverlaySetting();

                        // Also update current profile's OverlayLevel if SaveOverlayLevel is enabled
                        // This ensures the profile stays in sync with the user's selection
                        if (SaveOverlayLevel && !string.IsNullOrEmpty(currentProfileName))
                        {
                            var profile = GetProfile(currentProfileName);
                            if (profile != null)
                            {
                                profile.OverlayLevel = index;
                                SaveProfileToStorage(currentProfileName, profile);
                                Logger.Debug($"Updated profile '{currentProfileName}' OverlayLevel to {index}");
                            }
                        }
                    }
                }
            }
        }

        private void LoadPerformanceOverlaySetting()
        {
            try
            {
                if (PerformanceOverlayComboBox == null) return;
                isLoadingPerformanceOverlaySetting = true;
                var settings = ApplicationData.Current.LocalSettings;
                MigrateOverlayLevelsV2IfNeeded(settings);
                if (settings.Values.TryGetValue("PerformanceOverlayLevel", out object val) && val is int level)
                {
                    level = Math.Max(0, Math.Min(level, OverlayLevels.Max));
                    if (level >= 0 && level < PerformanceOverlayComboBox.Items.Count)
                    {
                        PerformanceOverlayComboBox.SelectedIndex = level;
                        if (osd != null)
                            osd.SetValue(level);
                        Logger.Debug($"Loaded PerformanceOverlayLevel: {level}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading PerformanceOverlay setting: {ex.Message}");
            }
            finally
            {
                isLoadingPerformanceOverlaySetting = false;
            }
        }

        private void SavePerformanceOverlaySetting()
        {
            try
            {
                if (PerformanceOverlayComboBox == null) return;
                var settings = ApplicationData.Current.LocalSettings;
                int level = PerformanceOverlayComboBox.SelectedIndex;
                settings.Values["PerformanceOverlayLevel"] = level;
                Logger.Debug($"Saved PerformanceOverlayLevel: {level}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving PerformanceOverlay setting: {ex.Message}");
            }
        }

        private void PerformanceOverlaySlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (PerformanceOverlaySlider != null && PerformanceOverlayComboBox != null)
            {
                int newIndex = (int)Math.Round(e.NewValue);
                newIndex = Math.Max(0, Math.Min(newIndex, OverlayLevels.Max));

                if (PerformanceOverlayComboBox.SelectedIndex != newIndex)
                {
                    PerformanceOverlayComboBox.SelectedIndex = newIndex;
                }
            }
        }

        private static void MigrateOverlayLevelsV2IfNeeded(Windows.Storage.ApplicationDataContainer settings)
        {
            if (settings.Values.ContainsKey(OverlayLevels.MigrationKey))
                return;

            if (settings.Values.TryGetValue("PerformanceOverlayLevel", out object val) && val is int level)
                settings.Values["PerformanceOverlayLevel"] = OverlayLevels.MigrateSavedLevel(level);

            ShiftOsdStorageLevel(settings, 3, 4);
            ShiftOsdStorageLevel(settings, 2, 3);
            ShiftOsdStorageLevel(settings, 1, 2);

            settings.Values["OSD_L1_FPS"] = true;
            settings.Values["OSD_L1_Time"] = false;
            settings.Values["OSD_L1_Battery"] = false;
            settings.Values["OSD_L1_Columns"] = 1;
            settings.Values["OSD_L1_Order"] = "FPS";

            settings.Values[OverlayLevels.MigrationKey] = true;
            Logger.Info("Migrated overlay levels to v2 (FPS Only inserted at level 1)");
        }

        private static void ShiftOsdStorageLevel(Windows.Storage.ApplicationDataContainer settings, int fromLevel, int toLevel)
        {
            string prefix = $"OSD_L{fromLevel}_";
            string newPrefix = $"OSD_L{toLevel}_";
            var keys = settings.Values.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            foreach (var key in keys)
            {
                settings.Values[newPrefix + key.Substring(prefix.Length)] = settings.Values[key];
                settings.Values.Remove(key);
            }
        }

    }
}
