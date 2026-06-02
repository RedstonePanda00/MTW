using UnityEngine;
using Verse;

namespace NCL.Worm
{
    // Reveals text character-by-character; click the text area to skip to the end.
    public class TypewriterTextDisplay
    {
        public const int CharsPerTick = 2;

        private string fullText = string.Empty;
        private int visibleChars;
        private bool complete;
        private Vector2 scrollPos;

        public bool IsComplete => complete || (fullText != null && visibleChars >= fullText.Length);

        public string FullText => fullText ?? string.Empty;

        public string VisibleText
        {
            get
            {
                if (fullText.NullOrEmpty())
                {
                    return string.Empty;
                }

                return fullText.Substring(0, Mathf.Min(visibleChars, fullText.Length));
            }
        }

        public void SetFullText(string text)
        {
            fullText = text ?? string.Empty;
            visibleChars = 0;
            complete = fullText.Length == 0;
            scrollPos = Vector2.zero;
        }

        public void Tick()
        {
            if (complete || fullText.NullOrEmpty())
            {
                return;
            }

            visibleChars = Mathf.Min(visibleChars + CharsPerTick, fullText.Length);
            if (visibleChars >= fullText.Length)
            {
                complete = true;
            }
        }

        public void SkipToEnd()
        {
            visibleChars = fullText?.Length ?? 0;
            complete = true;
        }

        public void Reset()
        {
            SetFullText(string.Empty);
        }

        // Draws wrapped text in rect. Returns true if the player clicked to skip.
        public bool Draw(Rect rect)
        {
            if (Event.current.type == EventType.Layout)
            {
                Tick();
            }

            if (!complete && !fullText.NullOrEmpty())
            {
                TooltipHandler.TipRegion(rect, "NCL_Typewriter_ClickToSkip".Translate());
            }

            if (Widgets.ButtonInvisible(rect))
            {
                if (!IsComplete)
                {
                    SkipToEnd();
                    return true;
                }
            }

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Small;
            string visible = VisibleText;
            float contentHeight = Mathf.Max(24f, Text.CalcHeight(visible, rect.width - 20f));
            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, contentHeight);
            Widgets.BeginScrollView(rect, ref scrollPos, viewRect);
            Widgets.Label(new Rect(0f, 0f, viewRect.width, contentHeight), visible);
            Widgets.EndScrollView();
            Text.Font = oldFont;
            return false;
        }
    }
}
