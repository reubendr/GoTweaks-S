using System.Drawing;

namespace XboxGamingBarHelper.RTSS.OSDItems
{
    internal class OSDItemFPS : OSDItem
    {
        public OSDItemFPS() : base("FPS", "FPS", Color.Red)
        {
        }

        public override string GetOSDString(int osdLevel)
        {
            var tc = GetTextColorWithOpacity();
            bool fpsOnly = osdLevel == 1;

            var pm = Program.PresentMonMetrics;
            if (pm != null && pm.IsLive(3000))
            {
                int rendered = pm.RenderedFps;
                int displayed = pm.DisplayedFps;
                int afmf = pm.AfmfFps;
                if (fpsOnly)
                {
                    if (rendered > 0) return $"<C={tc}>{rendered}";
                    return $"<C={tc}><FR>";
                }
                var yellow = ApplyOpacity("FFFF00");
                if (afmf > 0 && displayed > rendered)
                    return $"<C={tc}>{rendered} / {displayed} fps <C={yellow}>[FG] <FT> ms<C={tc}>";
                if (rendered > 0)
                    return $"<C={tc}>{rendered} fps <C={yellow}><FT> ms<C={tc}>";
            }

            if (fpsOnly)
                return $"<C={tc}><FR>";

            var yellowFallback = ApplyOpacity("FFFF00");
            return $"<C={tc}><FR> FPS <C={yellowFallback}><FT> ms<C={tc}>";
        }
    }
}
