using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

namespace RTLForge.EditorTools
{
    internal class FontGenerationResult
    {
        public TMP_FontAsset Asset;
        public string AssetPath;
        public bool Success;
        public string Error;
        public int RequestedCharacters;
        public int UniqueCharacters;
        public string UniqueCharactersList = string.Empty;
        public string AddedCharacters = string.Empty;
        public int AddedCount;
        public bool AllCharactersAdded;
        public string MissingCharacters = string.Empty;
        public int MissingCount;
    }

    /// <summary>
    /// Shared generation core for the RTL Forge editor windows. Wraps TMP's native
    /// font asset creation (CreateFontAsset + TryAddCharacters with font features,
    /// then baked to a Static atlas). Both the Font Generator window and the Arabic
    /// Setup window call this — the creation logic lives only here.
    /// </summary>
    internal static class RTLForgeFontGeneration
    {
        internal const string OutputFolder = "Assets/RTLForge/GeneratedFonts";

        /// <summary>Removes whitespace/duplicates; returns requested (non-whitespace) count.</summary>
        internal static string DedupeCharacters(string characterSet, out int requestedCount)
        {
            requestedCount = 0;
            if (string.IsNullOrEmpty(characterSet)) return string.Empty;

            var seen = new HashSet<char>();
            var sb = new StringBuilder();
            foreach (var ch in characterSet.Replace("\r", string.Empty))
            {
                if (char.IsWhiteSpace(ch)) continue;
                requestedCount++;
                if (seen.Add(ch)) sb.Append(ch);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Generates a static-atlas TMP Font Asset from a font and a character set
        /// using TMP's native APIs only.
        /// </summary>
        internal static FontGenerationResult Generate(Font font, string characterSet, string assetName)
        {
            return Generate(font, characterSet, assetName, null);
        }

        /// <summary>
        /// Generates a static-atlas TMP Font Asset from a font and a character set,
        /// optionally configuring its fallbackFontAssetTable.
        /// </summary>
        internal static FontGenerationResult Generate(Font font, string characterSet, string assetName, IList<TMP_FontAsset> fallbackFonts)
        {
            var result = new FontGenerationResult();

            if (font == null)
            {
                result.Error = "No font selected. Pick a .ttf or .otf font first.";
                return result;
            }

            string unique = DedupeCharacters(characterSet, out int requested);
            result.RequestedCharacters = requested;
            result.UniqueCharacters = unique.Length;
            result.UniqueCharactersList = unique;

            if (unique.Length == 0)
            {
                result.Error = "Character set is empty after removing whitespace.";
                return result;
            }

            // Create in Dynamic mode (required by TryAddCharacters), then bake to Static.
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                font,
                samplingPointSize: 90,
                atlasPadding: 9,
                renderMode: GlyphRenderMode.SDFAA,
                atlasWidth: 1024,
                atlasHeight: 1024,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (fontAsset == null)
            {
                result.Error = "TMP could not load the font face. Check that the font's Import Settings have \"Include Font Data\" enabled.";
                return result;
            }

            fontAsset.name = assetName;
            EnsureFolders();
            string assetPath = OutputFolder + "/" + assetName + ".asset";
            AssetDatabase.CreateAsset(fontAsset, assetPath);

            // TMP creates the atlas texture and material; register them as sub-assets.
            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            if (fontAsset.material != null)
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            result.AllCharactersAdded = fontAsset.TryAddCharacters(unique, out string missingCharacters, includeFontFeatures: true);
            result.MissingCharacters = missingCharacters ?? string.Empty;
            result.MissingCount = result.MissingCharacters.Length;

            var missingSet = new HashSet<char>(result.MissingCharacters);
            var addedSb = new StringBuilder();
            foreach (char c in unique)
            {
                if (!missingSet.Contains(c))
                    addedSb.Append(c);
            }
            result.AddedCharacters = addedSb.ToString();
            result.AddedCount = result.AddedCharacters.Length;

            // Configure fallback font asset table if provided
            if (fallbackFonts != null && fallbackFonts.Count > 0)
            {
                if (fontAsset.fallbackFontAssetTable == null)
                    fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();

                foreach (var fb in fallbackFonts)
                {
                    if (fb != null && fb != fontAsset && !fontAsset.fallbackFontAssetTable.Contains(fb))
                    {
                        fontAsset.fallbackFontAssetTable.Add(fb);
                    }
                }
            }

            // Bake to a static atlas: no further glyph generation at build/run time.
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            result.Asset = fontAsset;
            result.AssetPath = assetPath;
            result.Success = true;
            EditorGUIUtility.PingObject(fontAsset);
            return result;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/RTLForge"))
                AssetDatabase.CreateFolder("Assets", "RTLForge");
            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/RTLForge", "GeneratedFonts");
        }
    }
}
