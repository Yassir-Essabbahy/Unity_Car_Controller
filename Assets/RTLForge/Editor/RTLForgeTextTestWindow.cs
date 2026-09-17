using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RTLForge.Arabic;

namespace RTLForge.EditorTools
{
    /// <summary>
    /// RTL Forge — RTLForgeText component test window.
    /// Shows Original (logical) → Processed → what TMP renders, runs the standard
    /// case list, and can cycle text changes on a live RTLForgeText instance.
    /// </summary>
    public class RTLForgeTextTestWindow : EditorWindow
    {
        private static readonly string[] TestStrings =
        {
            "مرحبا بالعالم",
            "السلام عليكم",
            "محمّد",
            "إعدادات اللعبة",
            "السعر: ١٢٣",
            "هل أنت متأكد؟",
            "مرحبا World",
            "Hello مرحبا",
            "Hello مرحبا World",
        };

        private static readonly string[] CycleStrings =
        {
            "مرحبا",
            "السلام عليكم",
            "السعر: ١٢٣",
            "Hello مرحبا",
        };

        private string _rawText = "مرحبا بالعالم";
        private readonly StringBuilder _output = new StringBuilder();
        private Vector2 _scroll;
        private int _cycleIndex;

        [MenuItem("Tools/RTL Forge/RTLForgeText Test")]
        public static void Open()
        {
            var window = GetWindow<RTLForgeTextTestWindow>();
            window.titleContent = new GUIContent("RTL Forge — RTLForgeText Test");
            window.minSize = new Vector2(480, 560);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Original Logical Text", EditorStyles.boldLabel);
            _rawText = EditorGUILayout.TextArea(_rawText, GUILayout.MinHeight(50));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Processed Text", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(
                string.IsNullOrEmpty(_rawText) ? "—" : RTLProcessor.ProcessRTL(_rawText),
                GUI.skin.textArea, GUILayout.MinHeight(50));

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Run Case List"))
                    RunCaseList();
                if (GUILayout.Button("Create TMP Test Object"))
                    CreateTestObject();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Runtime Text-Change Test (Play Mode)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Enter Play Mode with a selected GameObject that has RTLForgeText + TMP_Text. " +
                "Each press assigns a new logical string via tmp.text and reads back what the " +
                "component reports — verifying repeated reprocessing and that the original " +
                "string stays intact.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!Application.isPlaying || Selection.activeGameObject == null))
                {
                    if (GUILayout.Button("Cycle Text On Selection"))
                        CycleTextOnSelection();
                }
                if (GUILayout.Button("Create Play-Mode Test Object"))
                    CreatePlayModeTestObject();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.SelectableLabel(_output.ToString(), GUI.skin.textArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void RunCaseList()
        {
            _output.AppendLine("— RTLForgeText case list —");
            foreach (var s in TestStrings)
            {
                string processed = RTLProcessor.ProcessRTL(s);
                string viaPreprocessor = new RTLForgeTextPreprocessor().PreprocessText(s);
                // Preprocessor path must match the direct pipeline exactly.
                bool consistent = processed == viaPreprocessor;
                _output.AppendLine((consistent ? "OK   " : "FAIL ") + s);
                _output.AppendLine("      processed: " + processed + (consistent ? "" : "  (preprocessor mismatch!)"));
            }
            _output.AppendLine();
        }

        private void CreateTestObject()
        {
            TMP_FontAsset fontAsset = FindGeneratedFontAsset();

            var canvasGO = new GameObject("RTLForgeText_DebugCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            // Original label — plain TMP, logical string, for visual comparison.
            CreateLabel(canvasGO.transform, new Vector2(0, 120), "TMP output (raw, no RTLForgeText)",
                _rawText, fontAsset, withRtlForge: false);
            // Processed label — has the component, so rendering goes through the preprocessor.
            CreateLabel(canvasGO.transform, new Vector2(0, -40), "TMP output (RTLForgeText)",
                _rawText, fontAsset, withRtlForge: true);
            CreateLabel(canvasGO.transform, new Vector2(0, -200), "Processed representation",
                RTLProcessor.ProcessRTL(_rawText), fontAsset, withRtlForge: false);

            Undo.RegisterCreatedObjectUndo(canvasGO, "RTL Forge — RTLForgeText Test Object");
            Selection.activeGameObject = canvasGO;
            _output.AppendLine("Test object created" + (fontAsset != null
                ? " with " + fontAsset.name
                : " (no generated font asset found — presentation-form glyphs may be missing)."));
        }

        private void CreatePlayModeTestObject()
        {
            TMP_FontAsset fontAsset = FindGeneratedFontAsset();

            var canvasGO = new GameObject("RTLForgeText_CycleTest", typeof(Canvas), typeof(CanvasScaler));
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var go = new GameObject("CycleText");
            go.transform.SetParent(canvasGO.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(800, 200);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null) tmp.font = fontAsset;
            tmp.fontSize = 40;
            tmp.text = CycleStrings[0];
            var rtl = go.AddComponent<RTLForgeText>();
            Selection.activeGameObject = go;

            _output.AppendLine("Play-mode cycle test object created. Use 'Cycle Text On Selection'.");
        }

        private void CycleTextOnSelection()
        {
            var rtl = Selection.activeGameObject.GetComponent<RTLForgeText>();
            if (rtl == null)
            {
                _output.AppendLine("FAIL: selected object has no RTLForgeText component.");
                return;
            }

            string next = CycleStrings[_cycleIndex % CycleStrings.Length];
            _cycleIndex++;

            // Normal developer usage: plain tmp.text assignment.
            var tmp = Selection.activeGameObject.GetComponent<TMP_Text>();
            tmp.text = next;

            // The component reprocesses transparently; original must stay logical.
            string original = rtl.OriginalText;
            string processed = rtl.ProcessedText;
            bool originalIntact = original == next;
            bool processedCorrect = processed == RTLProcessor.ProcessRTL(next);

            _output.AppendLine((originalIntact && processedCorrect ? "OK   " : "FAIL ") +
                "assigned \"" + next + "\"" +
                " | original intact: " + originalIntact +
                " | processed correct: " + processedCorrect +
                (originalIntact && processedCorrect ? "" : "\n      original: " + original + "\n      processed: " + processed));

            // Verify tmp.text was never overwritten with presentation forms.
            if (tmp.text != next)
                _output.AppendLine("FAIL: tmp.text was modified by processing! Value: " + tmp.text);
        }

        private static void CreateLabel(Transform parent, Vector2 pos, string name,
            string text, TMP_FontAsset fontAsset, bool withRtlForge)
        {
            var go = new GameObject(name.Replace(' ', '_'));
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(900, 140);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null) tmp.font = fontAsset;
            tmp.text = text;
            tmp.fontSize = 32;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            if (withRtlForge) go.AddComponent<RTLForgeText>();
        }

        private static TMP_FontAsset FindGeneratedFontAsset()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/RTLForge/GeneratedFonts" }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) return asset;
            }
            return null;
        }
    }
}
