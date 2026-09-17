using System.Text;

namespace RTLForge.Arabic
{
    /// <summary>
    /// Minimal Arabic contextual shaper (Unicode Arabic Presentation Forms-B).
    /// Determines isolated / initial / medial / final forms from neighbouring
    /// characters, applies the mandatory lam-alef ligatures, and passes combining
    /// marks (tashkeel) through unchanged. Pure string → string, no TMP dependency.
    /// </summary>
    public static class ArabicShaper
    {
        /// <summary>Shape Arabic letters into their contextual presentation forms.
        /// Non-Arabic characters are passed through unchanged.</summary>
        public static string ShapeArabic(string input)
        {
            if (string.IsNullOrEmpty(input)) return input ?? string.Empty;

            var sb = new StringBuilder(input.Length);
            bool prevConnectsForward = false; // previous letter can connect to the next one

            int i = 0;
            while (i < input.Length)
            {
                char c = input[i];

                if (ArabicCharacterDatabase.IsCombiningMark(c))
                {
                    // Marks are transparent: attached to the previous letter, no joining effect.
                    sb.Append(c);
                    i++;
                    continue;
                }

                if (!ArabicCharacterDatabase.TryGet(c, out var ch))
                {
                    // Space, punctuation, digits, Latin, unknown → breaks joining.
                    sb.Append(c);
                    prevConnectsForward = false;
                    i++;
                    continue;
                }

                // Next letter, skipping combining marks.
                char? nextLetter = NextLetter(input, i + 1, out int nextLetterIndex);

                // Mandatory lam-alef ligature: ل + ا/أ/إ/آ.
                if (c == '\u0644' && nextLetter.HasValue &&
                    ArabicCharacterDatabase.LamAlefLigatures.TryGetValue(nextLetter.Value, out var lig))
                {
                    bool joinPrev = prevConnectsForward && ch.ConnectsToPrevious;
                    sb.Append(joinPrev ? lig.Final : lig.Isolated);

                    // Append any marks that were attached to the consumed alef.
                    for (int m = i + 1; m < nextLetterIndex; m++) sb.Append(input[m]);

                    i = nextLetterIndex + 1;
                    prevConnectsForward = false; // alef never connects forward
                    continue;
                }

                bool connectNext = false;
                if (ch.ConnectsToNext && nextLetter.HasValue &&
                    ArabicCharacterDatabase.TryGet(nextLetter.Value, out var next) &&
                    next.ConnectsToPrevious)
                {
                    connectNext = true;
                }

                bool connectPrev = prevConnectsForward && ch.ConnectsToPrevious;
                sb.Append(ch.GetForm(connectPrev, connectNext));
                // The chain continues only if this dual letter actually connected forward.
                prevConnectsForward = ch.ConnectsToNext && connectNext;
                i++;
            }

            return sb.ToString();
        }

        private static char? NextLetter(string s, int start, out int letterIndex)
        {
            letterIndex = -1;
            for (int j = start; j < s.Length; j++)
            {
                if (ArabicCharacterDatabase.IsCombiningMark(s[j])) continue;
                if (ArabicCharacterDatabase.IsArabicLetter(s[j]))
                {
                    letterIndex = j;
                    return s[j];
                }
                return null; // any non-letter, non-mark character breaks the join
            }
            return null;
        }
    }
}
