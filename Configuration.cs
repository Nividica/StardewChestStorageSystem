using StardewModdingAPI;

namespace ChestStorageSystem
{
    public class Configuration
    {
        public enum WidthModes
        {
            Small = 0,
            Extended = 1,
            Full = 2,
            Default = Extended,
        }

        public SButton OpenUIKey { get; set; } = SButton.B;

        public WidthModes WidthMode { get; set; } = WidthModes.Default;

        public bool InvertShiftTransfer { get; set; } = false;
    }
}
