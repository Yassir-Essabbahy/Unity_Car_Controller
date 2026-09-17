using System.Collections.Generic;

namespace RTLForge.Arabic
{
    /// <summary>
    /// Unicode mappings for the basic Arabic alphabet (U+0621..U+064A) to their
    /// contextual forms in Arabic Presentation Forms-B (U+FE70..U+FEFF), plus the
    /// mandatory lam-alef ligatures.
    ///
    /// Presentation Forms-B layout: each dual-joining letter occupies four
    /// consecutive codepoints (isolated, final, initial, medial); each
    /// right-joining letter two (isolated, final).
    /// </summary>
    public static class ArabicCharacterDatabase
    {
        /// <summary>Lam + alef variant → (isolated, final) mandatory ligature.</summary>
        public static readonly Dictionary<char, (char Isolated, char Final)> LamAlefLigatures =
            new Dictionary<char, (char, char)>
            {
                ['\u0627'] = ('\uFEFB', '\uFEFC'), // لا
                ['\u0623'] = ('\uFEF7', '\uFEF8'), // لأ
                ['\u0625'] = ('\uFEF9', '\uFEFA'), // لإ
                ['\u0622'] = ('\uFEF5', '\uFEF6'), // لآ
            };

        public static readonly Dictionary<char, ArabicCharacter> Characters =
            new Dictionary<char, ArabicCharacter>();

        static ArabicCharacterDatabase()
        {
            // Non-joining
            Add('\u0621', JoiningType.NonJoining, '\uFE80', '\uFE80', '\uFE80', '\uFE80'); // ء

            // Right-joining (isolated, final only)
            Add('\u0622', JoiningType.RightJoining, '\uFE81', '\uFE81', '\uFE81', '\uFE82'); // آ
            Add('\u0623', JoiningType.RightJoining, '\uFE83', '\uFE83', '\uFE83', '\uFE84'); // أ
            Add('\u0624', JoiningType.RightJoining, '\uFE85', '\uFE85', '\uFE85', '\uFE86'); // ؤ
            Add('\u0625', JoiningType.RightJoining, '\uFE87', '\uFE87', '\uFE87', '\uFE88'); // إ
            Add('\u0627', JoiningType.RightJoining, '\uFE8D', '\uFE8D', '\uFE8D', '\uFE8E'); // ا
            Add('\u0629', JoiningType.RightJoining, '\uFE93', '\uFE93', '\uFE93', '\uFE94'); // ة
            Add('\u062F', JoiningType.RightJoining, '\uFEA9', '\uFEA9', '\uFEA9', '\uFEAA'); // د
            Add('\u0630', JoiningType.RightJoining, '\uFEAB', '\uFEAB', '\uFEAB', '\uFEAC'); // ذ
            Add('\u0631', JoiningType.RightJoining, '\uFEAD', '\uFEAD', '\uFEAD', '\uFEAE'); // ر
            Add('\u0632', JoiningType.RightJoining, '\uFEAF', '\uFEAF', '\uFEAF', '\uFEB0'); // ز
            Add('\u0648', JoiningType.RightJoining, '\uFEED', '\uFEED', '\uFEED', '\uFEEE'); // و
            Add('\u0649', JoiningType.RightJoining, '\uFEEF', '\uFEEF', '\uFEEF', '\uFEF0'); // ى

            // Dual-joining (isolated, final, initial, medial = 4 consecutive codepoints)
            Add4('\u0626', '\uFE89'); // ئ
            Add4('\u0628', '\uFE8F'); // ب
            Add4('\u062A', '\uFE95'); // ت
            Add4('\u062B', '\uFE99'); // ث
            Add4('\u062C', '\uFE9D'); // ج
            Add4('\u062D', '\uFEA1'); // ح
            Add4('\u062E', '\uFEA5'); // خ
            Add4('\u0633', '\uFEB1'); // س
            Add4('\u0634', '\uFEB5'); // ش
            Add4('\u0635', '\uFEB9'); // ص
            Add4('\u0636', '\uFEBD'); // ض
            Add4('\u0637', '\uFEC1'); // ط
            Add4('\u0638', '\uFEC5'); // ظ
            Add4('\u0639', '\uFEC9'); // ع
            Add4('\u063A', '\uFECD'); // غ
            Add4('\u0641', '\uFED1'); // ف
            Add4('\u0642', '\uFED5'); // ق
            Add4('\u0643', '\uFED9'); // ك
            Add4('\u0644', '\uFEDD'); // ل
            Add4('\u0645', '\uFEE1'); // م
            Add4('\u0646', '\uFEE5'); // ن
            Add4('\u0647', '\uFEE9'); // ه
            Add4('\u064A', '\uFEF1'); // ي
        }

        private static void Add(char baseChar, JoiningType joining,
            char isolated, char initial, char medial, char final)
        {
            Characters[baseChar] = new ArabicCharacter
            {
                Base = baseChar,
                Joining = joining,
                Isolated = isolated,
                Initial = initial,
                Medial = medial,
                Final = final,
            };
        }

        private static void Add4(char baseChar, char isolated)
        {
            // Presentation Forms-B consecutive layout: isolated, final, initial, medial.
            Add(baseChar, JoiningType.DualJoining,
                isolated, (char)(isolated + 2), (char)(isolated + 3), (char)(isolated + 1));
        }

        public static bool TryGet(char c, out ArabicCharacter ch) => Characters.TryGetValue(c, out ch);

        public static bool IsArabicLetter(char c) => Characters.ContainsKey(c);

        /// <summary>Combining marks (tashkeel) are transparent for joining.</summary>
        public static bool IsCombiningMark(char c) =>
            (c >= '\u064B' && c <= '\u065F') || c == '\u0670' || (c >= '\u06D6' && c <= '\u06ED');

        public static bool IsArabicDigit(char c) =>
            (c >= '\u0660' && c <= '\u0669') || (c >= '\u06F0' && c <= '\u06F9');
    }
}
