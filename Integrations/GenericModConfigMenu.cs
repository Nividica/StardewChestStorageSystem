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
                css.ModManifest,
                () => css.Config.OpenUIKey,
                (key) => css.Config.OpenUIKey = key,
                () => "Open UI Key"
            );

            IsRegistered = true;
            css.Monitor.VerboseLog("GenericModConfigMenu registration complete");
        }
    }
}
