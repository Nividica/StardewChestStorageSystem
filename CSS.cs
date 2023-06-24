using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace ChestStorageSystem
{
    public class CSS : Mod
    {
        private static IMonitor Logger;

        public static void Log(string msg, LogLevel level = LogLevel.Debug)
        {
            Logger?.Log(msg, level);
        }

        public Configuration Config { get; private set; }

        public override void Entry(IModHelper smapi)
        {
            // Read in the config
            Config = smapi.ReadConfig<Configuration>();

            // Set the logger
            Logger = this.Monitor;

            // Listen for events
            smapi.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            smapi.Events.Input.ButtonPressed += this.OnButtonPressed;
        }

        public void ResetConfig()
        {
            Config = new Configuration();
            this.Monitor.VerboseLog("Configuation was reset");
        }

        public void SaveConfig()
        {
            this.Monitor.VerboseLog("Attempting to save configuation");
            this.Helper.WriteConfig(Config);
            this.Monitor.VerboseLog("Configuation saved");
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // Integrate with GMCM
            Integrations.GenericModConfigMenu.Register(this);
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            // Ignore if player hasn't loaded a save yet
            // Or if there is no key bound
            if (!Context.IsWorldReady || Config.OpenUIKey == SButton.None)
            {
                return;
            }

            // Does the user want to open the UI?
            if (e.Button == Config.OpenUIKey && Context.IsPlayerFree && Game1.activeClickableMenu is null)
            {
                Game1.activeClickableMenu = new Menus.AggregationMenu(this);
                //Game1.activeClickableMenu = new Menus.TextureExplorerMenu();
            }
        }

    }
}
