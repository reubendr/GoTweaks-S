namespace Shared.Constants
{
    /// <summary>Human-readable Legion back-button names with physical location hints.</summary>
    public static class LegionButtonLabels
    {
        public static string GetDisplayName(string buttonId)
        {
            switch (buttonId)
            {
                case "Y1": return "Y1 · back L upper";
                case "Y2": return "Y2 · back L lower";
                case "Y3": return "Y3 · back R upper";
                case "M1": return "M1 · side grip L";
                case "M2": return "M2 · side grip R";
                case "M3": return "M3 · back R lower";
                case "Desktop": return "Desktop";
                case "Page": return "Page";
                default: return buttonId;
            }
        }
    }
}
