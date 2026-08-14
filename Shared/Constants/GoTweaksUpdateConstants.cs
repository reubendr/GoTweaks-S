namespace Shared.Constants
{
    /// <summary>
    /// Self-update source for this fork build. Point at your GitHub repo if you
    /// ever publish sideload releases; leave check-on-start off for personal builds.
    /// </summary>
    public static class GoTweaksUpdateConstants
    {
        public const string GitHubRepoPath = "reubendr/GoTweaks-S";
        public const string GitHubRepoUrl = "https://github.com/reubendr/GoTweaks-S";

        /// <summary>Personal fork: don't ping GitHub every launch unless opted in.</summary>
        public const bool CheckOnStartDefault = false;
    }
}
