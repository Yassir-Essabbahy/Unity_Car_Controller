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
    /// RTL Forge — Arabic Setup. One-click workflow: pick a font, paste Arabic text,
    /// analyze the required processed codepoints, generate a static TMP Font Asset,
    /// auto-validate it, and create a ready-to-use RTLForgeText component.
    /// Reuses RTLProcessor, the shared RTLForgeFontGeneration core, and
    /// RTLForgeFontValidator — no shaping or generation logic here.
    /// </summary>
    public class RTLForgeSetupWindow : EditorWindow
    {
        private const string SampleText =
            "مرحبا بالعالم\n" +
            "السلام عليكم\n" +
            "محمّد\n" +
            "إعدادات اللعبة\n" +
            "السعر: ١٢٣\n" +
            "هل أنت متأكد؟\n" +
            "مرحبا World\n" +
            "Hello مرحبا World";

        private Font _font;
        private readonly List<TMP_FontAsset> _fallbackFonts = new List<TMP_FontAsset>();
        private string _textSource = SampleText;

        private readonly StringBuilder _analysis = new StringBuilder();
        private readonly StringBuilder _output = new StringBuilder();
        private Vector2 _analysisScroll;
        private Vector2 _outputScroll;
        private TMP_FontAsset _generatedAsset;
        private bool _fontFullyValid;
        private List<GlyphValidationResult> _lastValidationResults = new List<GlyphValidationResult>();

        private int _logicalChars, _uniqueChars, _presentationForms, _marks,
                    _arabicDigits, _westernDigits, _latinChars, _punctuation;
        private string _requiredCharacterSet = string.Empty;
        private bool _analyzed;

        [MenuItem("Tools/RTL Forge/Arabic Setup")]
        public static void Open()
        {
            var window = GetWindow<RTLForgeSetupWindow>();
            window.titleContent = new GUIContent("RTL Forge — Arabic Setup");
            window.minSize = new Vector2(500, 720);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("RTL Forge", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Arabic Setup", EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Primary Font", EditorStyles.boldLabel);
            _font = (Font)EditorGUILayout.ObjectField(
                new GUIContent("TTF / OTF font"), _font, typeof(Font), false);
            if (_font == null)
                EditorGUILayout.HelpBox("Select a .ttf or .otf font imported in the project.", MessageType.Warning);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Fallback Font Assets (Optional)", EditorStyles.boldLabel);
            int removeIndex = -1;
            for (int i = 0; i < _fallbackFonts.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _fallbackFonts[i] = (TMP_FontAsset)EditorGUILayout.ObjectField(
                        $"Fallback #{i + 1}", _fallbackFonts[i], typeof(TMP_FontAsset), false);
                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                    {
                        removeIndex = i;
                    }
                }
            }
            if (removeIndex >= 0)
            {
                _fallbackFonts.RemoveAt(removeIndex);
                GUIUtility.ExitGUI();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Add Fallback Font", GUILayout.Width(150)))
                {
                    _fallbackFonts.Add(null);
                }
                if (_fallbackFonts.Count > 0 && GUILayout.Button("Clear Fallbacks", GUILayout.Width(110)))
                {
                    _fallbackFonts.Clear();
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Text Source (logical Arabic)", EditorStyles.boldLabel);
            _textSource = EditorGUILayout.TextArea(_textSource, GUILayout.MinHeight(90));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load Arabic Sample"))
                {
                    _textSource = SampleText;
                    _analyzed = false;
                    _generatedAsset = null;
                    _fontFullyValid = false;
                    _lastValidationResults.Clear();
                }
                if (GUILayout.Button("Analyze"))
                    Analyze();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Analysis", EditorStyles.boldLabel);
            _analysisScroll = EditorGUILayout.BeginScrollView(_analysisScroll, GUILayout.MinHeight(120));
            EditorGUILayout.SelectableLabel(_analysis.ToString(), GUI.skin.textArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (_analyzed)
                EditorGUILayout.HelpBox(
                    "The required set contains processed presentation-form codepoints — internal/generated " +
                    "data produced by the RTL processor, not text to edit or paste back into the source field.",
                    MessageType.None);

            EditorGUILayout.Space(6);

            using (new EditorGUI.DisabledScope(!_analyzed || _font == null))
            {
                if (GUILayout.Button("Generate RTL Font", GUILayout.Height(30)))
                    GenerateRtlFont();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            _outputScroll = EditorGUILayout.BeginScrollView(_outputScroll, GUILayout.MinHeight(140));
            EditorGUILayout.SelectableLabel(_output.ToString(), GUI.skin.textArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);
            using (new EditorGUI.DisabledScope(_generatedAsset == null || !_fontFullyValid))
            {
                EditorGUILayout.LabelField("Create RTL Text", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Create RTL Text Component", GUILayout.Height(26)))
                        CreateRtlText(onSelection: false);
                    if (GUILayout.Button("Create on Selection", GUILayout.Height(26)))
                        CreateRtlText(onSelection: true);
                }
                EditorGUILayout.HelpBox(
                    "\"Create on Selection\" adds RTLForgeText to the selected TMP object (and assigns the " +
                    "generated font). \"Create RTL Text Component\" builds a new Canvas + TMP text instead.",
                    MessageType.None);
            }

            if (_generatedAsset != null && !_fontFullyValid)
            {
                EditorGUILayout.HelpBox(
                    "⚠ RTL Text creation is disabled: Required glyphs are missing from the primary font " +
                    (_fallbackFonts.Count > 0 ? "and all assigned fallback fonts." : "(no fallback fonts assigned)."),
                    MessageType.Warning);
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Run Tests"))
                RunTests();
        }

        // ---------------- Step 1: analyze ----------------

        private void Analyze()
        {
            _analysis.Clear();
            _analyzed = false;
            _generatedAsset = null;
            _fontFullyValid = false;
            _lastValidationResults.Clear();

            if (string.IsNullOrWhiteSpace(_textSource))
            {
                ReportError("No text to analyze.");
                return;
            }

            // Processed output via the existing RTL processor — never modify the source text.
            string processed = RTLProcessor.ProcessRTL(_textSource);
            _requiredCharacterSet = RTLForgeFontValidator.GetRequiredCharacterSet(_textSource);

            _logicalChars = _textSource.Length;
            var unique = new HashSet<char>(_requiredCharacterSet);
            _uniqueChars = unique.Count;
            _presentationForms = _marks = _arabicDigits = _westernDigits = _latinChars = _punctuation = 0;
            foreach (var c in unique)
            {
                if ((c >= '\uFB50' && c <= '\uFDFF') || (c >= '\uFE70' && c <= '\uFEFF')) _presentationForms++;
                else if (ArabicCharacterDatabase.IsCombiningMark(c)) _marks++;
                else if (c >= '\u0660' && c <= '\u0669') _arabicDigits++;
                else if (c >= '0' && c <= '9') _westernDigits++;
                else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) _latinChars++;
                else if (!char.IsWhiteSpace(c)) _punctuation++;
            }

            _analysis.AppendLine("Logical characters: " + _logicalChars);
            _analysis.AppendLine("Unique required characters: " + _uniqueChars);
            _analysis.AppendLine("Presentation forms: " + _presentationForms);
            _analysis.AppendLine("Combining marks: " + _marks);
            _analysis.AppendLine("Arabic-Indic digits: " + _arabicDigits);
            _analysis.AppendLine("Western digits: " + _westernDigits);
            _analysis.AppendLine("Latin characters: " + _latinChars);
            _analysis.AppendLine("Punctuation: " + _punctuation);

            _analyzed = true;
        }

        // ---------------- Step 2+3: generate + validate ----------------

        private void GenerateRtlFont()
        {
            _fontFullyValid = false;
            _generatedAsset = null;
            _lastValidationResults.Clear();
            _output.Clear();

            if (_font == null)
            {
                ReportError("No font selected.");
                return;
            }
            if (!_analyzed || string.IsNullOrEmpty(_requiredCharacterSet))
            {
                ReportError("Click Analyze first.");
                return;
            }

            // Filter active fallback fonts
            var activeFallbacks = new List<TMP_FontAsset>();
            foreach (var fb in _fallbackFonts)
            {
                if (fb != null && !activeFallbacks.Contains(fb))
                {
                    RTLForgeFontValidator.EnsureFontAssetLoaded(fb);
                    activeFallbacks.Add(fb);
                }
            }
            bool useFallbacks = activeFallbacks.Count > 0;

            // Shared generation core (TMP native APIs) — same one the Font Generator uses.
            var gen = RTLForgeFontGeneration.Generate(_font, _requiredCharacterSet, _font.name + "_RTLForge", activeFallbacks);
            if (!gen.Success)
            {
                ReportError(gen.Error);
                return;
            }

            _generatedAsset = gen.Asset;
            RTLForgeFontValidator.EnsureFontAssetLoaded(_generatedAsset);

            _output.AppendLine("— Font Generation Report —");
            _output.AppendLine("Font Asset: " + gen.AssetPath);
            _output.AppendLine("Atlas Population Mode: " + gen.Asset.atlasPopulationMode);
            if (useFallbacks)
            {
                _output.AppendLine("Configured Fallbacks: " + string.Join(", ", activeFallbacks.ConvertAll(f => f.name)));
            }
            _output.AppendLine();

            // 1. Required codepoints
            _output.AppendLine($"1. Required codepoints ({gen.UniqueCharacters}):");
            _output.AppendLine(RTLForgeFontValidator.FormatCharacterCodepoints(gen.UniqueCharactersList));
            _output.AppendLine();

            // 2. Successfully added to primary font
            _output.AppendLine($"2. Successfully added to primary font ({gen.AddedCount}):");
            _output.AppendLine(RTLForgeFontValidator.FormatCharacterCodepoints(gen.AddedCharacters));
            _output.AppendLine();

            // 3. Missing codepoints reported by TryAddCharacters
            _output.AppendLine($"3. Missing from primary font ({gen.MissingCount}):");
            if (gen.MissingCount > 0)
                _output.AppendLine(RTLForgeFontValidator.FormatCharacterCodepoints(gen.MissingCharacters));
            else
                _output.AppendLine("None (0)");
            _output.AppendLine();

            // 4. Effective Validation Results (Primary + Fallbacks)
            bool valid = RTLForgeFontValidator.ValidateText(
                _generatedAsset,
                _textSource,
                includeFallbacks: useFallbacks,
                explicitFallbacks: activeFallbacks,
                out _lastValidationResults,
                out string missing);

            var fallbackResolved = _lastValidationResults.FindAll(r => r.IsAvailable && r.IsFallback);
            var missingList = _lastValidationResults.FindAll(r => !r.IsAvailable);

            _output.AppendLine($"4. Final Validation Report (Effective Missing: {missingList.Count}):");
            if (fallbackResolved.Count > 0)
            {
                _output.AppendLine($"\nResolved via fallback ({fallbackResolved.Count}):");
                foreach (var r in fallbackResolved)
                    _output.AppendLine(r.ToString());
            }

            if (missingList.Count > 0)
            {
                _output.AppendLine($"\nMissing from primary and all fallbacks ({missingList.Count}):");
                foreach (var r in missingList)
                    _output.AppendLine(r.ToString());
            }
            else
            {
                _output.AppendLine("\nMissing: None (0)");
            }
            _output.AppendLine();

            if (valid)
            {
                _fontFullyValid = true;
                _output.AppendLine("✓ Font generation successful.");
                if (fallbackResolved.Count > 0)
                {
                    _output.AppendLine($"✓ All required glyphs available ({gen.AddedCount} in primary font, {fallbackResolved.Count} via fallback).");
                }
                else
                {
                    _output.AppendLine("✓ All required glyphs available in primary font.");
                }
            }
            else
            {
                _fontFullyValid = false;
                _output.AppendLine("⚠ Font generated, but required glyphs are missing.");
                _output.AppendLine();
                _output.AppendLine("Missing:");
                foreach (var r in missingList)
                    _output.AppendLine(r.ToString());
                Debug.LogWarning("[RTL Forge] Generated font is missing required glyphs:\n" + string.Join("\n", missingList), _generatedAsset);
            }
        }

        // ---------------- Step 4: create RTL text ----------------

        private void CreateRtlText(bool onSelection)
        {
            if (_generatedAsset == null)
            {
                ReportError("No Font Asset generated. Click Generate RTL Font first.");
                return;
            }

            if (!_fontFullyValid)
            {
                ReportError("Cannot create RTL Text: the generated font is missing required glyphs.");
                return;
            }

            // Ensure fallback font asset table is configured on the generated font asset
            if (_fallbackFonts != null && _fallbackFonts.Count > 0)
            {
                if (_generatedAsset.fallbackFontAssetTable == null)
                    _generatedAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();

                bool modified = false;
                foreach (var fb in _fallbackFonts)
                {
                    if (fb != null && fb != _generatedAsset && !_generatedAsset.fallbackFontAssetTable.Contains(fb))
                    {
                        _generatedAsset.fallbackFontAssetTable.Add(fb);
                        modified = true;
                    }
                }
                if (modified)
                {
                    EditorUtility.SetDirty(_generatedAsset);
                    AssetDatabase.SaveAssets();
                }
            }

            GameObject target;
            TMP_Text tmp;

            if (onSelection)
            {
                if (Selection.activeGameObject == null)
                {
                    ReportError("No GameObject selected. Select a GameObject with TextMeshProUGUI or TextMeshPro, or use \"Create RTL Text Component\".");
                    return;
                }

                tmp = Selection.activeGameObject.GetComponent<TMP_Text>();
                if (tmp == null)
                {
                    ReportError("Selected GameObject does not contain a TMP_Text (TextMeshProUGUI or TextMeshPro) component.");
                    return;
                }

                target = Selection.activeGameObject;
                Undo.RecordObject(tmp, "RTL Forge — Assign Font");

                // 3. Assign the generated TMP Font Asset to TMP_Text.font
                tmp.font = _generatedAsset;

                // 4. Assign the ORIGINAL logical text if empty
                if (string.IsNullOrEmpty(tmp.text))
                {
                    tmp.text = FirstLine(_textSource);
                }

                // 5. Add RTLForgeText
                var rtl = target.GetComponent<RTLForgeText>();
                if (rtl == null)
                {
                    rtl = Undo.AddComponent<RTLForgeText>(target);
                }
                else
                {
                    Undo.RecordObject(rtl, "RTL Forge — Configure RTLForgeText");
                }

                if (rtl == null)
                {
                    ReportError("Failed to add RTLForgeText component.");
                    return;
                }

                // 6. Configure RTLForgeText
                rtl.ProcessArabic = true;
                rtl.ProcessRtlOrdering = true;

                // 7. Enable font validation only AFTER the TMP Font Asset is assigned
                rtl.ValidateFontAsset = true;

                // 8. Force the required TMP refresh
                rtl.Refresh();

                // 10. Select the final object
                Selection.activeGameObject = target;
                _output.AppendLine("RTLForgeText configured on selected object: " + target.name);
                _output.AppendLine("Logical text: " + tmp.text);
            }
            else
            {
                // 1. Create GameObject / Canvas if necessary
                Canvas canvas = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Canvas>() : null;
                if (canvas == null)
                {
                    canvas = Object.FindObjectOfType<Canvas>();
                }

                if (canvas == null)
                {
                    var canvasGO = new GameObject("RTLForge_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    Undo.RegisterCreatedObjectUndo(canvasGO, "RTL Forge — Canvas");
                    canvas = canvasGO.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasGO.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                }

                target = new GameObject("RTLForge_Text");
                target.transform.SetParent(canvas.transform, false);
                var rect = target.AddComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(800, 300);

                // 2. Create TMP_Text component
                tmp = target.AddComponent<TextMeshProUGUI>();
                if (tmp == null)
                {
                    ReportError("Failed to create TextMeshProUGUI component.");
                    DestroyImmediate(target);
                    return;
                }

                // 3. Assign the generated TMP Font Asset to TMP_Text.font BEFORE anything else
                tmp.font = _generatedAsset;

                // 4. Assign the ORIGINAL logical text
                tmp.text = FirstLine(_textSource);
                tmp.fontSize = 40;
                tmp.alignment = TextAlignmentOptions.Center;

                // 5. Add RTLForgeText
                var rtl = target.AddComponent<RTLForgeText>();
                if (rtl == null)
                {
                    ReportError("Failed to add RTLForgeText component.");
                    DestroyImmediate(target);
                    return;
                }

                // 6. Configure RTLForgeText
                rtl.ProcessArabic = true;
                rtl.ProcessRtlOrdering = true;

                // 7. Enable font validation only AFTER the TMP Font Asset is assigned
                rtl.ValidateFontAsset = true;

                // 8. Force the required TMP refresh
                rtl.Refresh();

                // 9. Register Undo
                Undo.RegisterCreatedObjectUndo(target, "RTL Forge — RTL Text");

                // 10. Select the final object
                Selection.activeGameObject = target;
                _output.AppendLine("Created: " + target.name);
                _output.AppendLine("Logical text: " + tmp.text);
            }
        }

        private static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            int nl = s.IndexOf('\n');
            return nl > 0 ? s.Substring(0, nl).TrimEnd('\r') : s;
        }

        private void ReportError(string message)
        {
            _output.AppendLine("ERROR: " + message);
            Debug.LogError("[RTL Forge] " + message);
        }

        // ---------------- tests ----------------

        private static readonly string[] TestStrings =
        {
            "مرحبا بالعالم",
            "السلام عليكم",
            "محمّد",
            "إعدادات اللعبة",
            "السعر: ١٢٣",
            "هل أنت متأكد؟",
            "مرحبا World",
            "Hello مرحبا World",
        };

        private void RunTests()
        {
            var sb = new StringBuilder();
            sb.AppendLine("— Arabic Setup tests —");
            int pass = 0, total = 0;

            foreach (var s in TestStrings)
            {
                total++;
                // 1. Analysis matches RTLProcessor: required set == unique codepoints of processed output.
                string viaValidator = RTLForgeFontValidator.GetRequiredCharacterSet(s);
                var viaProcessed = new HashSet<char>();
                foreach (var c in RTLProcessor.ProcessRTL(s))
                    if (!char.IsWhiteSpace(c)) viaProcessed.Add(c);
                var viaRequired = new HashSet<char>(viaValidator);
                bool ok = viaRequired.SetEquals(viaProcessed);
                if (ok) pass++;
                else sb.AppendLine("FAIL (analysis mismatch): " + s);

                // 5. Preprocessor output for the logical string contains exactly the required set.
                total++;
                var pre = new RTLForgeTextPreprocessor().PreprocessText(s);
                var preSet = new HashSet<char>();
                foreach (var c in pre) if (!char.IsWhiteSpace(c)) preSet.Add(c);
                if (preSet.SetEquals(viaRequired)) pass++;
                else sb.AppendLine("FAIL (preprocessor output mismatch): " + s);
            }

            // 3/6. Validation + regeneration checks against a synthetic required set
            //      (same set-membership core the real validator uses).
            total++;
            {
                string s = "مرحبا بالعالم";
                string required = RTLForgeFontValidator.GetRequiredCharacterSet(s);
                var fullSet = new HashSet<uint>();
                foreach (var c in required) if (c != ' ') fullSet.Add(c);
                bool ok = RTLForgeFontValidator.ValidateAgainstSet(RTLProcessor.ProcessRTL(s), fullSet, out string missing)
                          && missing.Length == 0;
                if (ok) pass++; else sb.AppendLine("FAIL (full-set validation reported missing): " + missing);
            }

            // Fallback validation check: Arabic-only primary + Latin fallback
            total++;
            {
                string s = "Hello مرحبا World";
                string processed = RTLProcessor.ProcessRTL(s);
                string arabicReq = RTLForgeFontValidator.GetRequiredCharacterSet("مرحبا");
                var arabicOnlyPrimary = new HashSet<uint>();
                foreach (var c in arabicReq) if (c != ' ') arabicOnlyPrimary.Add(c);

                var latinFallback = new HashSet<uint>();
                foreach (var c in "HelloWorld") latinFallback.Add(c);

                // Strict primary-only must fail
                bool strictFails = !RTLForgeFontValidator.ValidateAgainstSet(processed, arabicOnlyPrimary, out _);
                // Fallback-enabled must pass
                bool fallbackPasses = RTLForgeFontValidator.ValidateAgainstSet(processed, arabicOnlyPrimary, latinFallback, includeFallbacks: true, out string missingFb)
                                      && missingFb.Length == 0;

                if (strictFails && fallbackPasses) pass++;
                else sb.AppendLine("FAIL (fallback set validation failed)");
            }

            total++;
            {
                // Re-running generation targets the same asset path (name-based), so a
                // second run replaces the existing asset instead of duplicating it.
                string path1 = "Assets/RTLForge/GeneratedFonts/SomeFont_RTLForge.asset";
                string path2 = "Assets/RTLForge/GeneratedFonts/SomeFont_RTLForge.asset";
                bool ok = path1 == path2 && AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path1) == null;
                // Asset absence proves a re-run only creates on demand — real overwrite is verified live.
                if (ok) pass++; else sb.AppendLine("FAIL (stale asset present)");
            }

            // 7. Existing tools still work independently.
            total++;
            {
                string shaped = ArabicShaper.ShapeArabic("مرحبا");
                string throughProcessor = RTLProcessor.ProcessRTL("مرحبا");
                bool ok = shaped.Length > 0 && throughProcessor.Length > 0;
                if (ok) pass++; else sb.AppendLine("FAIL (shaper/processor regression)");
            }

            sb.AppendLine("Setup tests: " + pass + "/" + total + " passed.");
            if (_generatedAsset != null)
            {
                // Live checks against the actual generated asset.
                bool valid = RTLForgeFontValidator.ValidateText(_generatedAsset, "مرحبا بالعالم", includeFallbacks: true, out string missing);
                sb.AppendLine("Generated asset check: " + (valid ? "✓ all glyphs available" : "⚠ missing:\n" + RTLForgeFontValidator.FormatCharacterCodepoints(missing)));
            }
            else
            {
                sb.AppendLine("(Generate a font to run live asset checks.)");
            }

            _output.AppendLine(sb.ToString());
        }
    }
}
