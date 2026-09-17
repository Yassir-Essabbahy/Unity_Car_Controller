using UnityEngine;
using TMPro;

namespace RTLForge.Arabic
{
    /// <summary>
    /// Attach to any GameObject with a TMP_Text (TextMeshProUGUI or TextMeshPro).
    /// Installs an RTLForgeTextPreprocessor into TMP's textPreprocessor slot so
    /// Arabic shaping and RTL reordering run automatically on every text render:
    ///
    ///     GetComponent&lt;TMP_Text&gt;().text = "مرحبا بالعالم";
    ///
    /// The original logical string is never modified — TMP calls
    /// ITextPreprocessor.PreprocessText(m_text) itself and keeps the processed
    /// result in its internal text backing array.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("RTL Forge/RTL Forge Text")]
    [ExecuteAlways]
    public class RTLForgeText : MonoBehaviour
    {
        [Tooltip("Apply Arabic contextual shaping (isolated/initial/medial/final forms).")]
        [SerializeField] private bool m_ProcessArabic = true;

        [Tooltip("Apply RTL/bidi run reordering so LTR renderers display Arabic correctly.")]
        [SerializeField] private bool m_ProcessRtlOrdering = true;

        [Tooltip("Validate that the TMP Font Asset contains every glyph required by the processed text. Reports only when the text or font changes.")]
        [SerializeField] private bool m_ValidateFontAsset = false;

        private RTLForgeTextPreprocessor _preprocessor;
        private ITextPreprocessor _previousPreprocessor;
        private TMP_Text _tmpText;
        private bool _installed;

        // Font-validation cache — revalidate only when text or font changes.
        private string _validatedText;
        private TMP_FontAsset _validatedFont;
        private bool _fontValid = true;
        private string _missingCharacters = string.Empty;
        private bool _loggedInvalidFontWarning;

        /// <summary>The logical (unprocessed) source string — same as tmp.text.</summary>
        public string OriginalText => Tmp != null ? Tmp.text : string.Empty;

        /// <summary>The processed string TMP will actually render.</summary>
        public string ProcessedText { get; private set; }

        public bool ProcessArabic
        {
            get => m_ProcessArabic;
            set { m_ProcessArabic = value; SyncFlags(); Refresh(); }
        }

        public bool ProcessRtlOrdering
        {
            get => m_ProcessRtlOrdering;
            set { m_ProcessRtlOrdering = value; SyncFlags(); Refresh(); }
        }

        /// <summary>True when the last font validation passed (or was not run).</summary>
        public bool IsFontValid => _fontValid;

        /// <summary>Missing glyphs reported by the last font validation, or empty.</summary>
        public string MissingCharacters => _missingCharacters;

        public bool ValidateFontAsset
        {
            get => m_ValidateFontAsset;
            set { m_ValidateFontAsset = value; _validatedText = null; _validatedFont = null; _loggedInvalidFontWarning = false; Refresh(); }
        }

        private TMP_Text Tmp
        {
            get
            {
                if (_tmpText == null) _tmpText = GetComponent<TMP_Text>();
                return _tmpText;
            }
        }

        private void OnEnable()
        {
            Install();
            Refresh();
        }

        private void OnDisable()
        {
            Uninstall();
        }

#if UNITY_EDITOR
        private void OnDestroy()
        {
            // OnDisable already runs before destroy, but be safe in edit mode.
            if (_installed) Uninstall();
        }

        private void OnValidate()
        {
            SyncFlags();
            Refresh();
        }
#endif

        /// <summary>
        /// Re-installs the preprocessor and forces TMP to re-render the current
        /// text. Call after changing text externally at runtime, or after changing
        /// processing flags.
        /// </summary>
        public void Refresh()
        {
            var tmp = Tmp;
            if (tmp == null) return;

            if (!_installed) Install();

            SyncFlags();
            ProcessedText = _preprocessor != null ? _preprocessor.PreprocessText(tmp.text) : tmp.text;
            tmp.SetAllDirty(); // full re-parse/re-render in play mode and editor

            if (m_ValidateFontAsset) RunFontValidation();
        }

        private static bool IsDefaultOrInvalidFont(TMP_FontAsset font)
        {
            if (font == null) return true;
            if (font.name == "LiberationSans SDF" || font.name == "LiberationSans" || font.name == "LiberationSans SDF - Fallback")
                return true;
            return false;
        }

        /// <summary>
        /// Validates the current logical text against the current font. Only
        /// re-runs (and only logs) when the text or font asset actually changed.
        /// </summary>
        private void RunFontValidation()
        {
            var tmp = Tmp;
            if (tmp == null) return;

            string text = tmp.text;
            TMP_FontAsset font = tmp.font;

            if (font == null || IsDefaultOrInvalidFont(font))
            {
                _fontValid = false;
                _missingCharacters = "<no RTL Font Asset assigned>";

                if (text == _validatedText && font == _validatedFont) return;
                _validatedText = text;
                _validatedFont = font;

                if (!_loggedInvalidFontWarning)
                {
                    _loggedInvalidFontWarning = true;
                    Debug.LogWarning(
                        $"[RTL Forge] RTLForgeText on '{gameObject.name}' has no valid RTL Font Asset assigned " +
                        $"(current font: {(font != null ? "\"" + font.name + "\"" : "null")}). Skipping validation.",
                        this);
                }
                return;
            }

            if (text == _validatedText && font == _validatedFont) return;

            _validatedText = text;
            _validatedFont = font;
            _loggedInvalidFontWarning = false;

            _fontValid = RTLForgeFontValidator.ValidateText(font, text, includeFallbacks: true, out string missing);
            _missingCharacters = missing;

            if (!_fontValid)
            {
                Debug.LogWarning(
                    "[RTL Forge] " + missing.Length + " required glyph(s) missing from \"" +
                    font.name + "\":\n" + RTLForgeFontValidator.FormatCharacterCodepoints(missing) +
                    "\nOpen Tools → RTL Forge → Font Validator to copy the required character set.",
                    this);
            }
        }

        private void Install()
        {
            var tmp = Tmp;
            if (tmp == null || _installed) return;

            if (_preprocessor == null) _preprocessor = new RTLForgeTextPreprocessor();
            _preprocessor.Preprocessed -= OnPreprocessed;
            _preprocessor.Preprocessed += OnPreprocessed;

            _previousPreprocessor = tmp.textPreprocessor;
            tmp.textPreprocessor = _preprocessor;
            _installed = true;
        }

        private void OnPreprocessed(string logicalText)
        {
            // Runs on every TMP layout pass; RunFontValidation's cache makes this cheap.
            if (m_ValidateFontAsset) RunFontValidation();
        }

        private void Uninstall()
        {
            var tmp = Tmp;
            if (tmp != null && _installed && ReferenceEquals(tmp.textPreprocessor, _preprocessor))
                tmp.textPreprocessor = _previousPreprocessor; // restore what was there before

            if (_preprocessor != null) _preprocessor.Preprocessed -= OnPreprocessed;
            _installed = false;
            _previousPreprocessor = null;
        }

        private void SyncFlags()
        {
            if (_preprocessor == null) return;
            _preprocessor.ProcessArabic = m_ProcessArabic;
            _preprocessor.ProcessRtlOrdering = m_ProcessRtlOrdering;
        }
    }
}
