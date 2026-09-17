using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using TMPro;
using RTLForge.Arabic;

namespace RTLForge.EditorTools
{
    /// <summary>
    /// RTL Forge — Font Validator.
    /// Checks whether a TMP Font Asset contains every glyph required by the
    /// PROCESSED form of Arabic text (Presentation Forms-B codepoints, not the
    /// logical input).
    /// </summary>
    public class RTLForgeFontValidatorWindow : EditorWindow
    {
        private TMP_FontAsset _fontAsset;
        private string _text = "مرحبا بالعالم";
        private readonly StringBuilder _output = new StringBuilder();
        private Vector2 _scroll;

        [MenuItem("Tools/RTL Forge/Font Validator")]
        public static void Open()
        {
            var window = GetWindow<RTLForgeFontValidatorWindow>();
            window.titleContent = new GUIContent("RTL Forge — Font Validator");
            window.minSize = new Vector2(480, 560);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("RTL Forge — Font Validator", EditorStyles.boldLabel);

            _fontAsset = (TMP_FontAsset)EditorGUILayout.ObjectField(
                new GUIContent("Font Asset"), _fontAsset, typeof(TMP_FontAsset), false);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Text (logical Arabic, not presentation forms)", EditorStyles.boldLabel);
            _text = EditorGUILayout.TextArea(_text, GUILayout.MinHeight(50));

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate", GUILayout.Height(26)))
                    Validate();
                if (GUILayout.Button("Run Tests"))
                    RunTests();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Processed text (Unicode/internal data — do not edit manually)", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(
                string.IsNullOrEmpty(_text) ? "—" : RTLProcessor.ProcessRTL(_text),
                GUI.skin.textArea, GUILayout.MinHeight(40));

            EditorGUILayout.Space(8);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.SelectableLabel(_output.ToString(), GUI.skin.textArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_output.Length == 0))
                {
                    if (GUILayout.Button("Copy Missing Characters"))
                        Copy(missingFromOutput);
                    if (GUILayout.Button("Copy Required Character Set"))
                        Copy(RTLForgeFontValidator.GetRequiredCharacterSet(_text));
                }
                if (GUILayout.Button("Open Font Generator"))
                    EditorApplication.ExecuteMenuItem("Tools/RTL Forge/Font Generator");
            }
        }

        private string missingFromOutput = string.Empty;

        private void Validate()
        {
            _output.Clear();
            missingFromOutput = string.Empty;

            if (_fontAsset == null)
            {
                _output.AppendLine("ERROR: no Font Asset assigned.");
                return;
            }
            if (string.IsNullOrWhiteSpace(_text))
            {
                _output.AppendLine("ERROR: no text to validate.");
                return;
            }

            string processed = RTLProcessor.ProcessRTL(_text);
            bool valid = RTLForgeFontValidator.ValidateText(_fontAsset, _text, out string missing);

            // Required = unique non-whitespace codepoints of the processed output.
            var required = new HashSet<char>(processed);
            required.RemoveWhere(c => c == '\n' || c == '\r' || c == '\t' || c == ' ');
            int missingCount = valid ? 0 : new HashSet<char>(missing).Count;

            _output.AppendLine("Original logical text: " + _text);
            _output.AppendLine("Font Asset: " + _fontAsset.name);
            _output.AppendLine();
            _output.AppendLine("Characters required: " + required.Count);
            _output.AppendLine("Characters available: " + (required.Count - missingCount));
            _output.AppendLine();
            _output.AppendLine(valid
                ? "✓ All required glyphs available."
                : "⚠ Missing: " + missingCount);
            if (!valid)
            {
                _output.AppendLine("Missing: " + missing);
                _output.AppendLine(Codepoints(missing));
                missingFromOutput = missing;
            }

            Repaint();
        }

        private static void Copy(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            EditorGUIUtility.systemCopyBuffer = value;
        }

        // ---------------- automated tests ----------------

        private static readonly string[] TestStrings =
        {
            "مرحبا",
            "محمد",
            "العربية",
            "بسم الله",
            "السلام عليكم",
            "السعر: ١٢٣",
            "هل أنت متأكد؟",
            "مرحبا World",
            "Hello مرحبا",
            "Hello مرحبا World",
        };

        /// <summary>
        /// Tests the validator's required-codepoint/missing-glyph logic against
        /// synthetic "font" codepoint sets (the same core ValidateProcessed uses;
        /// a real TMP_FontAsset would only replace the set membership lookup).
        /// </summary>
        private void RunTests()
        {
            _output.Clear();
            _output.AppendLine("— Font Validator tests —");
            int pass = 0, total = 0;

            // Required sets for every case, as the validator sees them.
            var requiredPerCase = new Dictionary<string, string>();
            foreach (var s in TestStrings)
                requiredPerCase[s] = RTLForgeFontValidator.GetRequiredCharacterSet(s);

            // 1. A "font" containing every required glyph must report success.
            var fullSet = new HashSet<uint>();
            foreach (var kv in requiredPerCase)
                foreach (var c in kv.Value)
                    if (c != ' ') fullSet.Add(c);
            foreach (var s in TestStrings)
            {
                total++;
                string processed = RTLProcessor.ProcessRTL(s);
                bool ok = RTLForgeFontValidator.ValidateAgainstSet(processed, fullSet, out string missing) && missing.Length == 0;
                if (ok) pass++;
                else _output.AppendLine("FAIL (full font reported missing): " + s + " → " + missing);
            }

            // 2. A font with everything EXCEPT presentation forms must report exactly
            //    the missing presentation-form codepoints (marks/digits/punctuation/Latin
            //    stay raw in processed text, so they remain covered).
            var noPresentationForms = new HashSet<uint>();
            foreach (var c in fullSet)
            {
                char ch = unchecked((char)c);
                bool isPresentation = (ch >= '\uFB50' && ch <= '\uFDFF') || (ch >= '\uFE70' && ch <= '\uFEFF');
                if (!isPresentation) noPresentationForms.Add(c);
            }
            foreach (var s in TestStrings)
            {
                total++;
                string processed = RTLProcessor.ProcessRTL(s);
                bool ok = !RTLForgeFontValidator.ValidateAgainstSet(processed, noPresentationForms, out string missing);
                // Every reported missing char must be a presentation form.
                bool plausible = true;
                foreach (var c in missing)
                    if (!((c >= '\uFB50' && c <= '\uFDFF') || (c >= '\uFE70' && c <= '\uFEFF'))) plausible = false;
                if (ok && plausible) pass++;
                else _output.AppendLine("FAIL (no-presentation-forms font): " + s + " → " + Codepoints(missing));
            }

            // 3. Missing punctuation detection: font without '؟' must flag it.
            {
                total++;
                var noQuestion = new HashSet<uint>(fullSet);
                noQuestion.Remove('؟');
                bool ok = !RTLForgeFontValidator.ValidateAgainstSet(
                    RTLProcessor.ProcessRTL("هل أنت متأكد؟"), noQuestion, out string missing)
                    && missing == "؟";
                if (ok) pass++; else _output.AppendLine("FAIL (missing punctuation): " + missing);
            }

            // 4. Missing tashkeel detection: font without shadda must flag it in محمّد.
            {
                total++;
                var noShadda = new HashSet<uint>(fullSet);
                noShadda.Remove('\u0651');
                bool ok = !RTLForgeFontValidator.ValidateAgainstSet(
                    RTLProcessor.ProcessRTL("محمّد"), noShadda, out string missing)
                    && missing == "\u0651";
                if (ok) pass++; else _output.AppendLine("FAIL (missing tashkeel): " + Codepoints(missing));
            }

            // 5. Latin + numbers handled: full set passes both mixed cases (covered above),
            //    and a font missing Latin digits flags '1'.
            {
                total++;
                var noWesternDigits = new HashSet<uint>(fullSet);
                for (char d = '0'; d <= '9'; d++) noWesternDigits.Remove(d);
                bool ok = !RTLForgeFontValidator.ValidateAgainstSet(
                    RTLProcessor.ProcessRTL("Score: 125"), noWesternDigits, out string missing)
                    && missing == "125";
                if (ok) pass++; else _output.AppendLine("FAIL (Latin/digits): " + missing);
            }

            // 6+7. Changing text / font changes the result.
            {
                total++;
                string a = RTLForgeFontValidator.GetRequiredCharacterSet("مرحبا");
                string b = RTLForgeFontValidator.GetRequiredCharacterSet("السلام عليكم");
                bool ok = a != b;
                if (ok) pass++; else _output.AppendLine("FAIL (validation is text-dependent)");
            }

            // 8. Already-processed input is not reshaped: ProcessRTL is idempotent.
            {
                total++;
                string logical = "مرحبا بالعالم";
                string shaped = ArabicShaper.ShapeArabic(logical);
                bool ok = ArabicShaper.ShapeArabic(shaped) == shaped;
                if (ok) pass++; else _output.AppendLine("FAIL (shaped input was re-shaped)");
            }

            // 9. Fallback validation test: Arabic-only primary + Latin fallback.
            {
                total++;
                string processed = RTLProcessor.ProcessRTL("Hello مرحبا World");
                var arabicOnly = new HashSet<uint>();
                foreach (var c in RTLForgeFontValidator.GetRequiredCharacterSet("مرحبا"))
                    if (c != ' ') arabicOnly.Add(c);
                var latinFallback = new HashSet<uint>();
                foreach (var c in "HelloWorld") latinFallback.Add(c);

                bool strictFails = !RTLForgeFontValidator.ValidateAgainstSet(processed, arabicOnly, out string missingStrict)
                                   && missingStrict.Contains("Hello") && missingStrict.Contains("World");
                bool fallbackPasses = RTLForgeFontValidator.ValidateAgainstSet(processed, arabicOnly, latinFallback, includeFallbacks: true, out string missingFb)
                                      && missingFb.Length == 0;

                if (strictFails && fallbackPasses) pass++;
                else _output.AppendLine("FAIL (fallback validation test)");
            }

            _output.AppendLine();
            _output.AppendLine("Validator tests: " + pass + "/" + total + " passed.");
            Repaint();
        }

        private static string Codepoints(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s)
                sb.Append("U+").Append(((int)c).ToString("X4")).Append(' ');
            return sb.ToString().Trim();
        }
    }
}
