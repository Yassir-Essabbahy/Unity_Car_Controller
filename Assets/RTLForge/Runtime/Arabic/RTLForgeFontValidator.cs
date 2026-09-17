using System.Collections.Generic;
using System.Text;
using TMPro;

namespace RTLForge.Arabic
{
    /// <summary>
    /// Validates that a TMP Font Asset contains every glyph required by the
    /// PROCESSED form of Arabic text (shaping turns base letters into
    /// Presentation Forms-B codepoints, so the processed output is what matters).
    ///
    /// Architecture: ArabicShaper → RTLProcessor → this validator → TMP Font Asset.
    /// Contains no shaping rules of its own; RTLProcessor is the source of truth.
    /// </summary>
    /// <summary>
    /// Represents the validation outcome and supplying font for a specific codepoint.
    /// </summary>
    public struct GlyphValidationResult
    {
        public uint Codepoint;
        public string Character;
        public bool IsAvailable;
        public bool IsFallback;
        public TMP_FontAsset SupplyingFont;

        public override string ToString()
        {
            string cp = $"U+{Codepoint:X4}";
            if (!IsAvailable)
                return $"{Character} ({cp}) — MISSING (not found in primary or any fallback)";
            if (IsFallback && SupplyingFont != null)
                return $"{Character} ({cp}) — resolved via fallback: {SupplyingFont.name}";
            return $"{Character} ({cp}) — primary: {(SupplyingFont != null ? SupplyingFont.name : "Primary")}";
        }
    }

    /// <summary>
    /// Validates that a TMP Font Asset contains every glyph required by the
    /// PROCESSED form of Arabic text (shaping turns base letters into
    /// Presentation Forms-B codepoints, so the processed output is what matters).
    ///
    /// Architecture: ArabicShaper → RTLProcessor → this validator → TMP Font Asset.
    /// Contains no shaping rules of its own; RTLProcessor is the source of truth.
    /// </summary>
    public static class RTLForgeFontValidator
    {
        /// <summary>
        /// Shapes + reorders the logical text, then checks the TMP Font Asset
        /// for every codepoint of the processed result (primary asset only).
        /// </summary>
        public static bool ValidateText(TMP_FontAsset fontAsset, string logicalText, out string missingCharacters)
        {
            return ValidateText(fontAsset, logicalText, out missingCharacters, out _);
        }

        /// <summary>
        /// Shapes + reorders the logical text, then checks the TMP Font Asset
        /// for every codepoint of the processed result (primary asset only), returning both formatted string and list of codepoints.
        /// </summary>
        public static bool ValidateText(TMP_FontAsset fontAsset, string logicalText, out string missingCharacters, out List<uint> missingCodepoints)
        {
            missingCharacters = string.Empty;
            missingCodepoints = new List<uint>();
            if (fontAsset == null)
            {
                missingCharacters = "<no Font Asset assigned>";
                return false;
            }
            if (string.IsNullOrEmpty(logicalText)) return true;

            return ValidateProcessed(fontAsset, RTLProcessor.ProcessRTL(logicalText), out missingCharacters, out missingCodepoints);
        }

        /// <summary>
        /// Validates logical text optionally searching fontAsset.fallbackFontAssetTable.
        /// </summary>
        public static bool ValidateText(TMP_FontAsset fontAsset, string logicalText, bool includeFallbacks, out string missingCharacters)
        {
            return ValidateText(fontAsset, logicalText, includeFallbacks, null, out _, out missingCharacters);
        }

        /// <summary>
        /// Validates logical text optionally searching font fallbacks and explicit fallback list, returning per-glyph results.
        /// </summary>
        public static bool ValidateText(
            TMP_FontAsset fontAsset,
            string logicalText,
            bool includeFallbacks,
            IList<TMP_FontAsset> explicitFallbacks,
            out List<GlyphValidationResult> results,
            out string missingCharacters)
        {
            results = new List<GlyphValidationResult>();
            missingCharacters = string.Empty;
            if (fontAsset == null)
            {
                missingCharacters = "<no Font Asset assigned>";
                return false;
            }
            if (string.IsNullOrEmpty(logicalText)) return true;

            return ValidateProcessed(fontAsset, RTLProcessor.ProcessRTL(logicalText), includeFallbacks, explicitFallbacks, out results, out missingCharacters);
        }

        /// <summary>Checks an already-processed (shaped + reordered) string (primary asset only).</summary>
        public static bool ValidateProcessed(TMP_FontAsset fontAsset, string processedText, out string missingCharacters)
        {
            return ValidateProcessed(fontAsset, processedText, out missingCharacters, out _);
        }

        /// <summary>Checks an already-processed (shaped + reordered) string (primary asset only), returning missing codepoints list.</summary>
        public static bool ValidateProcessed(TMP_FontAsset fontAsset, string processedText, out string missingCharacters, out List<uint> missingCodepoints)
        {
            missingCharacters = string.Empty;
            missingCodepoints = new List<uint>();
            if (fontAsset == null)
            {
                missingCharacters = "<no Font Asset assigned>";
                return false;
            }
            if (string.IsNullOrEmpty(processedText)) return true;

            // No fallback search: a fallback asset satisfying the glyph would mask
            // that the validated asset itself is missing it.
            bool allFound = fontAsset.HasCharacters(processedText, out uint[] missingCodes, searchFallbacks: false, tryAddCharacter: false);
            if (allFound || missingCodes == null || missingCodes.Length == 0) return true;

            var unique = new HashSet<uint>(missingCodes);
            var sb = new StringBuilder();
            foreach (uint code in unique)
            {
                missingCodepoints.Add(code);
                sb.Append(char.ConvertFromUtf32(unchecked((int)code)));
            }
            missingCharacters = sb.ToString();
            return false;
        }

        /// <summary>
        /// Validates processed text optionally walking the fallback chain, returning detailed per-codepoint resolution results.
        /// </summary>
        public static bool ValidateProcessed(
            TMP_FontAsset fontAsset,
            string processedText,
            bool includeFallbacks,
            IList<TMP_FontAsset> explicitFallbacks,
            out List<GlyphValidationResult> results,
            out string missingCharacters)
        {
            results = new List<GlyphValidationResult>();
            missingCharacters = string.Empty;
            if (fontAsset == null)
            {
                missingCharacters = "<no Font Asset assigned>";
                return false;
            }
            if (string.IsNullOrEmpty(processedText)) return true;

            EnsureFontAssetLoaded(fontAsset);
            if (explicitFallbacks != null)
            {
                foreach (var fb in explicitFallbacks)
                    EnsureFontAssetLoaded(fb);
            }

            if (!includeFallbacks && (explicitFallbacks == null || explicitFallbacks.Count == 0))
            {
                bool ok = ValidateProcessed(fontAsset, processedText, out missingCharacters, out List<uint> missingCodes);
                var missingSet = new HashSet<uint>(missingCodes);
                var seen = new HashSet<uint>();
                for (int i = 0; i < processedText.Length; i += char.IsSurrogatePair(processedText, i) ? 2 : 1)
                {
                    uint code = unchecked((uint)char.ConvertToUtf32(processedText, i));
                    if (code == '\n' || code == '\r' || code == '\t' || code == ' ' || !seen.Add(code)) continue;
                    bool isMissing = missingSet.Contains(code);
                    results.Add(new GlyphValidationResult
                    {
                        Codepoint = code,
                        Character = char.ConvertFromUtf32(unchecked((int)code)),
                        IsAvailable = !isMissing,
                        IsFallback = false,
                        SupplyingFont = !isMissing ? fontAsset : null
                    });
                }
                return ok;
            }

            var missingSb = new StringBuilder();
            var seenCp = new HashSet<uint>();
            bool allAvailable = true;

            for (int i = 0; i < processedText.Length; i += char.IsSurrogatePair(processedText, i) ? 2 : 1)
            {
                uint code = unchecked((uint)char.ConvertToUtf32(processedText, i));
                if (code == '\n' || code == '\r' || code == '\t' || code == ' ' || !seenCp.Add(code)) continue;

                TMP_FontAsset supplyingFont = FindSupplyingFont(fontAsset, code, explicitFallbacks);
                bool available = supplyingFont != null;
                bool isFallback = available && supplyingFont != fontAsset;

                if (!available)
                {
                    allAvailable = false;
                    missingSb.Append(char.ConvertFromUtf32(unchecked((int)code)));
                }

                results.Add(new GlyphValidationResult
                {
                    Codepoint = code,
                    Character = char.ConvertFromUtf32(unchecked((int)code)),
                    IsAvailable = available,
                    IsFallback = isFallback,
                    SupplyingFont = supplyingFont
                });
            }

            missingCharacters = missingSb.ToString();
            return allAvailable;
        }

        /// <summary>
        /// Recursively searches for the font asset (primary or fallback) supplying the given Unicode codepoint.
        /// </summary>
        public static TMP_FontAsset FindSupplyingFont(
            TMP_FontAsset primaryFont,
            uint unicode,
            IList<TMP_FontAsset> explicitFallbacks = null)
        {
            if (primaryFont != null && FontHasCharacter(primaryFont, unicode))
            {
                return primaryFont;
            }

            var visited = new HashSet<TMP_FontAsset>();
            if (primaryFont != null) visited.Add(primaryFont);

            // Check explicit fallbacks first
            if (explicitFallbacks != null)
            {
                foreach (var fb in explicitFallbacks)
                {
                    var found = SearchFallbackRecursive(fb, unicode, visited);
                    if (found != null) return found;
                }
            }

            // Check primary font's fallback table
            if (primaryFont != null && primaryFont.fallbackFontAssetTable != null)
            {
                foreach (var fb in primaryFont.fallbackFontAssetTable)
                {
                    var found = SearchFallbackRecursive(fb, unicode, visited);
                    if (found != null) return found;
                }
            }

            return null;
        }

        private static TMP_FontAsset SearchFallbackRecursive(
            TMP_FontAsset font,
            uint unicode,
            HashSet<TMP_FontAsset> visited)
        {
            if (font == null || !visited.Add(font)) return null;

            if (FontHasCharacter(font, unicode))
            {
                return font;
            }

            if (font.fallbackFontAssetTable != null)
            {
                foreach (var subFb in font.fallbackFontAssetTable)
                {
                    var found = SearchFallbackRecursive(subFb, unicode, visited);
                    if (found != null) return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Ensures the font asset's internal character lookup tables are initialized and populated from disk.
        /// </summary>
        public static void EnsureFontAssetLoaded(TMP_FontAsset font)
        {
            if (font == null) return;
            if (font.characterLookupTable == null || font.characterLookupTable.Count == 0)
            {
                font.ReadFontAssetDefinition();
            }
        }

        /// <summary>
        /// Checks if a font asset contains a character without searching its fallbacks or altering dynamic atlas.
        /// Pre-initializes font asset character lookup tables to guarantee consistent results on first query.
        /// Uses TMP_FontAsset.HasCharacter(int) which accepts a 32-bit codepoint and queries m_CharacterLookupDictionary
        /// directly by uint key.
        /// Note: All current RTL Forge characters (Arabic presentation forms, marks, Latin, digits, punctuation)
        /// reside in the Basic Multilingual Plane (BMP, codepoints <= 0xFFFF).
        /// </summary>
        public static bool FontHasCharacter(TMP_FontAsset font, uint unicode)
        {
            if (font == null) return false;
            EnsureFontAssetLoaded(font);
            if (font.characterLookupTable != null && font.characterLookupTable.ContainsKey(unicode))
                return true;
            return font.HasCharacter((int)unicode);
        }

        /// <summary>
        /// Formats characters into "Character (U+XXXX)" representation.
        /// </summary>
        public static string FormatCharacterCodepoints(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder();
            var seen = new HashSet<int>();
            for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
            {
                int codePoint = char.ConvertToUtf32(text, i);
                if (seen.Add(codePoint))
                {
                    string charStr = char.ConvertFromUtf32(codePoint);
                    sb.AppendLine($"{charStr} (U+{codePoint:X4})");
                }
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Formats a list of codepoints into "Character (U+XXXX)" representation.
        /// </summary>
        public static string FormatCharacterCodepoints(IEnumerable<uint> codepoints)
        {
            if (codepoints == null) return string.Empty;
            var sb = new StringBuilder();
            var seen = new HashSet<uint>();
            foreach (uint code in codepoints)
            {
                if (seen.Add(code))
                {
                    string charStr = char.ConvertFromUtf32(unchecked((int)code));
                    sb.AppendLine($"{charStr} (U+{code:X4})");
                }
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// The exact characters needed by the processed output — paste into the
        /// Font Generator's character set and regenerate to fix missing glyphs.
        /// </summary>
        public static string GetRequiredCharacterSet(string logicalText)
        {
            string processed = RTLProcessor.ProcessRTL(logicalText);
            var seen = new HashSet<char>();
            var sb = new StringBuilder();
            foreach (var c in processed)
            {
                if (c == '\n' || c == '\r' || c == '\t' || !seen.Add(c)) continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        // ---- set-based core, shared by ValidateProcessed path semantics and the automated tests ----

        /// <summary>Missing-glyph detection against a plain set of available codepoints.
        /// Whitespace/control characters are not glyph requirements.
        /// Public so editor windows can test the validator with synthetic font sets.</summary>
        public static bool ValidateAgainstSet(string processedText, ICollection<uint> availableCodepoints, out string missingCharacters)
        {
            return ValidateAgainstSet(processedText, availableCodepoints, null, includeFallbacks: false, out missingCharacters);
        }

        /// <summary>
        /// Missing-glyph detection supporting primary and fallback codepoint sets.
        /// </summary>
        public static bool ValidateAgainstSet(
            string processedText,
            ICollection<uint> primaryCodepoints,
            ICollection<uint> fallbackCodepoints,
            bool includeFallbacks,
            out string missingCharacters)
        {
            var primary = primaryCodepoints != null ? new HashSet<uint>(primaryCodepoints) : new HashSet<uint>();
            var fallback = fallbackCodepoints != null ? new HashSet<uint>(fallbackCodepoints) : new HashSet<uint>();
            var seen = new HashSet<char>();
            var sb = new StringBuilder();

            foreach (var c in processedText)
            {
                if (c == '\n' || c == '\r' || c == '\t' || c == ' ' || !seen.Add(c)) continue;
                bool inPrimary = primary.Contains(c);
                bool inFallback = includeFallbacks && fallback.Contains(c);
                if (!inPrimary && !inFallback)
                {
                    sb.Append(c);
                }
            }

            missingCharacters = sb.ToString();
            return missingCharacters.Length == 0;
        }
    }
}
