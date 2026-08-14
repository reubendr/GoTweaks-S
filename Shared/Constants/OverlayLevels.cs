namespace Shared.Constants
{
    /// <summary>
    /// RTSS performance overlay levels (0=Off, 1=FPS only, 2=Basic, 3=Detailed, 4=Full).
    /// v2 inserted FPS-only at level 1; legacy saved levels 1–3 map to 2–4.
    /// </summary>
    public static class OverlayLevels
    {
        public const int Max = 4;
        public const string MigrationKey = "OverlayLevels_v2_Migrated";

        public static string GetShortName(int level)
        {
            switch (level)
            {
                case 0: return "Off";
                case 1: return "FPS";
                case 2: return "Basic";
                case 3: return "Detailed";
                case 4: return "Full";
                default: return "Off";
            }
        }

        /// <summary>Migrate legacy levels (1=Basic, 2=Detailed, 3=Full) to v2.</summary>
        public static int MigrateSavedLevel(int level)
        {
            if (level <= 0) return 0;
            if (level <= 3) return level + 1;
            return level > Max ? Max : level;
        }
    }
}
