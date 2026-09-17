using UnityEngine;

namespace RTLForge.Arabic
{
    /// <summary>
    /// TMP text preprocessor that runs Arabic shaping + RTL reordering.
    /// Assigned to TMP_Text.textPreprocessor by <see cref="RTLForgeText"/>.
    ///
    /// TMP invokes PreprocessText(m_text) — the logical string — during text info
    /// generation, so the source text is never overwritten and no recursion occurs.
    ///
    /// FONT REQUIREMENT: the processed string uses Arabic Presentation Forms-B
    /// codepoints (U+FE70..U+FEFF). The TMP Font Asset must contain those glyphs —
    /// generate it with the RTL Forge Font Generator using the "Copy Character Set
    /// For Font Generator" workflow from the Arabic Text Test window.
    /// </summary>
    public class RTLForgeTextPreprocessor : TMPro.ITextPreprocessor
    {
        /// <summary>Apply Arabic contextual shaping (isolated/initial/medial/final).</summary>
        public bool ProcessArabic = true;

        /// <summary>Apply RTL/bidi run reordering for LTR renderers like TMP.</summary>
        public bool ProcessRtlOrdering = true;

        /// <summary>Raised on every TMP layout pass with the logical text — used by
        /// RTLForgeText for cached font validation on text changes.</summary>
        public event System.Action<string> Preprocessed;

        public string PreprocessText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (!ProcessArabic && !ProcessRtlOrdering) return text;
            if (IsAlreadyProcessed(text)) return text; // don't re-shape presentation forms

            Preprocessed?.Invoke(text);

            if (!ProcessArabic) return text; // reordering alone is not meaningful yet
            if (!ProcessRtlOrdering) return ArabicShaper.ShapeArabic(text);
            return RTLProcessor.ProcessRTL(text);
        }

        /// <summary>
        /// TMP feeds this only the logical string, but guard against shaped input
        /// anyway (e.g. a user pasting presentation forms): shaped text has no
        /// base Arabic letters (U+0621..U+064A) left to process.
        /// </summary>
        private static bool IsAlreadyProcessed(string text)
        {
            foreach (var c in text)
                if (ArabicCharacterDatabase.IsArabicLetter(c)) return false;
            return true;
        }
    }
}
