using UnityEngine;

namespace UIU.Simulator.UI
{
    /// <summary>
    /// Centralized MVP UI palette for black / white / bright-orange branding.
    /// Screen UI only — world feedback (scanner LEDs) stays separate.
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color Black = new Color(0f, 0f, 0f, 1f);
        public static readonly Color White = Color.white;
        public static readonly Color Grey = new Color(0.7f, 0.7f, 0.7f, 1f);
        public static readonly Color BrightOrange = new Color(1f, 0.55f, 0.1f, 1f);
        public static readonly Color Red = new Color(1f, 0.4f, 0.4f, 1f);

        /// <summary>Success / completed activity status (not used for accents or buttons).</summary>
        public static readonly Color Success = new Color(0.28f, 0.82f, 0.42f, 1f);

        /// <summary>Danger / missed activity status — alias of <see cref="Red"/>.</summary>
        public static readonly Color Danger = Red;
    }
}
