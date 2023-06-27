using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StardewModdingAPI;

namespace ChestStorageSystem.Integrations
{
    internal class GenericModConfigMenu
    {
        public static bool IsRegistered { get; private set; }

        public static void Register(CSS css)
        {
            css.Monitor.VerboseLog("Attempting to register with GenericModConfigMenu");
            if (IsRegistered)
            {
                css.Monitor.VerboseLog("Already registered with GenericModConfigMenu");
                return;
            }

            // Attempt to load the API
            IGenericModConfigMenuApi gmcm = css.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null)
            {
                css.Monitor.VerboseLog("GenericModConfigMenu API was not found");
                return;
            }

            // Register
            gmcm.Register(css.ModManifest, css.ResetConfig, css.SaveConfig, false);

            // OpenUI keybind
            gmcm.AddKeybind(
                mod: css.ModManifest,
                name: () => "Open UI Key",
                getValue: () => css.Config.OpenUIKey,
                setValue: (key) => css.Config.OpenUIKey = key
            );

            // Aggro Window Width
            Type TWidthModes = typeof(Configuration.WidthModes);
            gmcm.AddTextOption(
                mod: css.ModManifest,
                name: () => "Prefered Width",
                getValue: () => Enum.GetName(TWidthModes, css.Config.WidthMode),
                setValue: (name) => css.Config.WidthMode = (Configuration.WidthModes)Enum.Parse(TWidthModes, name),
                allowedValues: Enum.GetNames(TWidthModes)
            );

            // Invert Shift Behavior
            gmcm.AddBoolOption(
                mod: css.ModManifest,
                name: () => "Click Item To Transfer",
                getValue: () => css.Config.InvertShiftTransfer,
                setValue: (invert) => css.Config.InvertShiftTransfer = invert
            );

            // BG FX
            gmcm.AddBoolOption(
                mod: css.ModManifest,
                name: () => "Background FX",
                getValue: () => css.Config.BackgroundEffects,
                setValue: (enabled) => css.Config.BackgroundEffects = enabled,
                tooltip: () => "Note: The \"Show Menu Background\" option overrides this."
            );

            IsRegistered = true;
            css.Monitor.VerboseLog("GenericModConfigMenu registration complete");
        }
    }
}
