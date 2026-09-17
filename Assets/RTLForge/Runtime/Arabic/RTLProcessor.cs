using System.Collections.Generic;
using System.Text;

namespace RTLForge.Arabic
{
    /// <summary>
    /// Minimal RTL reordering for display: shapes Arabic text first, then
    /// reorders runs so a left-to-right renderer (TextMeshPro) shows it correctly.
    ///
    /// This is a simplified Unicode BiDi (UBA) subset with base direction RTL:
    ///  - Arabic runs are reversed (marks stay attached to their base letter)
    ///  - Latin and digit runs keep their internal order
    ///  - Neutral characters (spaces, punctuation) adopt the direction of the
    ///    surrounding run, or RTL when ambiguous.
    ///
    /// It is NOT a complete Unicode BiDi implementation.
    /// </summary>
    public static class RTLProcessor
    {
        private enum Dir { Rtl, Ltr, Neutral }

        /// <summary>Full pipeline: Arabic shaping → RTL/visual reordering.</summary>
        public static string ProcessRTL(string input)
        {
            if (string.IsNullOrEmpty(input)) return input ?? string.Empty;
            return ReorderToVisual(ArabicShaper.ShapeArabic(input));
        }

        internal static string ReorderToVisual(string shaped)
        {
            // Split into clusters (base character + trailing combining marks).
            var clusters = new List<(string Text, Dir Direction)>();
            var current = new StringBuilder();
            foreach (var c in shaped)
            {
                if (ArabicCharacterDatabase.IsCombiningMark(c) && current.Length > 0)
                {
                    current.Append(c); // mark stays with its base cluster
                    continue;
                }
                if (current.Length > 0) clusters.Add((current.ToString(), Classify(current[0])));
                current.Clear().Append(c);
            }
            if (current.Length > 0) clusters.Add((current.ToString(), Classify(current[0])));

            if (clusters.Count == 0) return string.Empty;

            // Resolve neutrals: adopt the surrounding strong direction; RTL when mixed/edges.
            var dirs = new Dir[clusters.Count];
            for (int i = 0; i < clusters.Count; i++) dirs[i] = clusters[i].Direction;
            for (int i = 0; i < clusters.Count; i++)
            {
                if (dirs[i] != Dir.Neutral) continue;
                Dir before = Dir.Rtl, after = Dir.Rtl;
                for (int j = i - 1; j >= 0; j--) if (dirs[j] != Dir.Neutral) { before = dirs[j]; break; }
                for (int j = i + 1; j < dirs.Length; j++) if (dirs[j] != Dir.Neutral) { after = dirs[j]; break; }
                dirs[i] = before == after ? before : Dir.Rtl;
            }

            // Group consecutive clusters of the same direction into runs.
            var runs = new List<(int Start, int End, Dir Direction)>();
            int runStart = 0;
            for (int i = 1; i <= clusters.Count; i++)
            {
                if (i == clusters.Count || dirs[i] != dirs[runStart])
                {
                    runs.Add((runStart, i - 1, dirs[runStart]));
                    runStart = i;
                }
            }

            // Emit runs in reverse order; RTL run contents reversed, LTR kept in order.
            var sb = new StringBuilder(shaped.Length);
            for (int r = runs.Count - 1; r >= 0; r--)
            {
                var (start, end, dir) = runs[r];
                if (dir == Dir.Ltr)
                {
                    for (int i = start; i <= end; i++) sb.Append(clusters[i].Text);
                }
                else
                {
                    for (int i = end; i >= start; i--) sb.Append(clusters[i].Text);
                }
            }
            return sb.ToString();
        }

        private static Dir Classify(char c)
        {
            // Digit sequences (Western and Arabic-Indic) render left-to-right
            // inside RTL text, so they are ordered as LTR runs.
            if (ArabicCharacterDatabase.IsArabicDigit(c)) return Dir.Ltr;
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                return Dir.Ltr;
            if (IsRtlChar(c)) return Dir.Rtl;
            return Dir.Neutral;
        }

        private static bool IsRtlChar(char c) =>
            (c >= '\u0600' && c <= '\u06FF') ||   // Arabic (incl. punctuation ، ؟)
            (c >= '\u0750' && c <= '\u077F') ||
            (c >= '\uFB50' && c <= '\uFDFF') ||   // Presentation Forms-A
            (c >= '\uFE70' && c <= '\uFEFF');     // Presentation Forms-B
    }
}
