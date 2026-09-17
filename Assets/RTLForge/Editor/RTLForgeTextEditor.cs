using UnityEditor;
using UnityEngine;
using TMPro;
using RTLForge.Arabic;

namespace RTLForge.EditorTools
{
    /// <summary>
    /// Minimal inspector for RTLForgeText: default fields plus a Font Validation
    /// warning box (no Console spam — warnings come only when text/font changed).
    /// </summary>
    [CustomEditor(typeof(RTLForgeText))]
    public class RTLForgeTextEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var rtl = (RTLForgeText)target;
            if (!rtl.ValidateFontAsset) return;

            if (rtl.IsFontValid)
            {
                EditorGUILayout.HelpBox("Font Validation: all required glyphs available.", MessageType.Info);
            }
            else
            {
                var tmp = rtl.GetComponent<TMP_Text>();
                string fontName = (tmp != null && tmp.font != null) ? tmp.font.name : "null";
                string formattedMissing = RTLForgeFontValidator.FormatCharacterCodepoints(rtl.MissingCharacters);
                EditorGUILayout.HelpBox(
                    "⚠ RTL Forge: required glyphs missing from \"" + fontName + "\":\n" +
                    formattedMissing +
                    "\nUse Tools → RTL Forge → Font Validator to copy the required character set.",
                    MessageType.Warning);
            }
        }
    }
}
