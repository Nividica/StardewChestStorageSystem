using StardewModdingAPI;

namespace ChestStorageSystem
{
    public class Configuration
    {
        public enum WidthModes
        {
            Regular = 0,
            Extended = 1,
            Full = 2,
        }

        public SButton OpenUIKey { get; set; } = SButton.B;

        public WidthModes WidthMode { get; set; } = WidthModes.Extended;

        public bool InvertShiftTransfer { get; set; } = false;

        public bool BackgroundEffects { get; set; } = true;
    }
}
