using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using TMPro;

namespace RTLForge.EditorTools
{
    /// <summary>
    /// RTL Forge — Font Generator (proof of concept).
    /// Creates a static-atlas TextMeshPro Font Asset from a TTF/OTF font and an
    /// Arabic character set. Atlas generation and glyph rendering are handled
    /// entirely by TMP's native APIs.
    ///
    /// Note on Static mode: TMP's TryAddCharacters() only runs while the asset is
    /// Dynamic, so the asset is created Dynamic, populated, then flipped to Static
    /// (the standard way to bake a static atlas via public API).
    /// </summary>
    public class RTLForgeFontGeneratorWindow : EditorWindow
    {
        private const string DefaultCharacterSet = "ابتثجحخدذرزسشصضطظعغفقكلمنهويءأإآةى٠١٢٣٤٥٦٧٨٩،؟: ";

        private const string ArabicTestText =
            "مرحبا بالعالم\n" +
            "اللغة العربية جميلة\n" +
            "محمّد\n" +
            "إعدادات اللعبة\n" +
            "السعر: ١٢٣\n" +
            "هل أنت متأكد؟";

        private Font _font;
        private string _characterSet = DefaultCharacterSet;
        private readonly StringBuilder _status = new StringBuilder();
        private Vector2 _statusScroll;
        private TMP_FontAsset _lastGenerated;

        [MenuItem("Tools/RTL Forge/Font Generator")]
        public static void Open()
        {
            var window = GetWindow<RTLForgeFontGeneratorWindow>();
            window.titleContent = new GUIContent("RTL Forge — Font Generator");
            window.minSize = new Vector2(440, 520);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var font = (Font)EditorGUILayout.ObjectField(
                new GUIContent("Font (.ttf / .otf)"), _font, typeof(Font), false);
            if (EditorGUI.EndChangeCheck())
                _font = font;

            if (_font != null)
                EditorGUILayout.HelpBox("Selected: " + _font.name, MessageType.Info);
            else
                EditorGUILayout.HelpBox("Select a .ttf or .otf font imported in the project.", MessageType.Warning);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Character Set", EditorStyles.boldLabel);
            _characterSet = EditorGUILayout.TextArea(_characterSet, GUILayout.MinHeight(80));

            EditorGUILayout.LabelField("Population Mode: Static", EditorStyles.miniLabel);

            EditorGUILayout.Space(6);

            using (new EditorGUI.DisabledScope(_font == null || string.IsNullOrWhiteSpace(_characterSet)))
            {
                if (GUILayout.Button("Generate TMP Font Asset", GUILayout.Height(28)))
                    Generate();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            _statusScroll = EditorGUILayout.BeginScrollView(_statusScroll, GUILayout.MinHeight(90));
            EditorGUILayout.TextArea(_status.ToString(), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Arabic Verification", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Verifies GLYPH AVAILABILITY only (atlas + missing-glyph boxes + numerals + punctuation). " +
                "It does NOT verify Arabic text shaping / RTL layout — TMP has no built-in bidi or shaping " +
                "engine, so joined-letter rendering and right-to-left ordering are handled by a separate " +
                "RTL system in a later phase.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(_lastGenerated == null))
            {
                if (GUILayout.Button("Create Test Text"))
                    CreateTestText();
            }
            if (_lastGenerated == null)
                EditorGUILayout.LabelField("Generate a Font Asset first.", EditorStyles.miniLabel);
        }

        private void Generate()
        {
            _status.Clear();

            if (_font == null)
            {
                Fail("No font selected. Pick a .ttf or .otf font first.");
                return;
            }
            if (string.IsNullOrWhiteSpace(_characterSet))
            {
                Fail("Character set is empty.");
                return;
            }

            // Shared generation core (TMP native APIs; creation logic lives in the utility).
            var gen = RTLForgeFontGeneration.Generate(_font, _characterSet, _font.name + "_RTLForge");
            if (!gen.Success)
            {
                Fail(gen.Error);
                return;
            }
            TMP_FontAsset fontAsset = gen.Asset;
            string assetPath = gen.AssetPath;

            int missingCount = string.IsNullOrEmpty(gen.MissingCharacters) ? 0 : gen.MissingCharacters.Length;
            int addedCount = gen.UniqueCharacters - missingCount;

            _lastGenerated = fontAsset;

            _status.AppendLine("Requested characters: " + gen.RequestedCharacters);
            _status.AppendLine("Unique characters: " + gen.UniqueCharacters);
            _status.AppendLine("Successfully added: " + addedCount);
            _status.AppendLine("Missing characters: " + (missingCount == 0 ? "none" : missingCount + "  →  " + gen.MissingCharacters));
            _status.AppendLine("Atlas texture count: " + fontAsset.atlasTextureCount);
            _status.AppendLine("Glyph count: " + fontAsset.glyphTable.Count);
            _status.AppendLine("Font features: " + (fontAsset.fontFeatureTable != null && fontAsset.fontFeatureTable.glyphPairAdjustmentRecords.Count > 0 ? "imported" : "none found in font"));
            _status.AppendLine("Population mode: Static");
            _status.AppendLine(gen.AllCharactersAdded
                ? "Generation successful."
                : "Generation completed WITH MISSING GLYPHS — see missing characters above.");
            _status.AppendLine("Font Asset: " + assetPath);

            if (!gen.AllCharactersAdded)
                Debug.LogWarning("[RTL Forge] Missing characters in font \"" + _font.name + "\": " + gen.MissingCharacters, fontAsset);
        }

        /// <summary>
        /// Creates a simple Canvas + TextMeshProUGUI in the open scene using the
        /// generated Font Asset, to visually check glyph availability.
        /// </summary>
        private void CreateTestText()
        {
            if (_lastGenerated == null) return;

            var canvasGO = new GameObject("RTLForge_ArabicTest_Canvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            var textGO = new GameObject("ArabicTestText");
            textGO.transform.SetParent(canvasGO.transform, false);

            var rect = textGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(700, 400);

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.font = _lastGenerated;
            tmp.text = ArabicTestText;
            tmp.fontSize = 36;
            tmp.alignment = TextAlignmentOptions.Right;
            tmp.textWrappingMode = TextWrappingModes.Normal;

            Undo.RegisterCreatedObjectUndo(canvasGO, "RTL Forge — Arabic Test Text");
            Selection.activeGameObject = textGO;

            _status.AppendLine("Test object created in open scene: " + canvasGO.name);
            _status.AppendLine("Check for missing-glyph boxes. Shaping/RTL order is NOT validated here.");
        }

        private void Fail(string message)
        {
            _status.AppendLine("ERROR: " + message);
            Debug.LogError("[RTL Forge] " + message);
        }
    }
}
