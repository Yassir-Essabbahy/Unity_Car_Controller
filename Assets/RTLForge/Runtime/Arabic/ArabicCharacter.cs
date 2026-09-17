namespace RTLForge.Arabic
{
    public enum JoiningType
    {
        NonJoining,   // never connects (e.g. ء)
        RightJoining, // connects only to the previous character (e.g. ا د ر ز و)
        DualJoining,  // connects on both sides (e.g. ب س ع)
    }

    /// <summary>
    /// One Arabic letter with its Unicode Arabic Presentation Forms-B (U+FE70..U+FEFF)
    /// codepoints for each contextual form. Everything is expressed as codepoints —
    /// no visual/hardcoded glyph assumptions.
    /// </summary>
    public struct ArabicCharacter
    {
        public char Base;
        public JoiningType Joining;
        public char Isolated;
        public char Initial;
        public char Medial;
        public char Final;

        public bool ConnectsToNext => Joining == JoiningType.DualJoining;
        public bool ConnectsToPrevious => Joining != JoiningType.NonJoining;

        public char GetForm(bool joinPrevious, bool joinNext)
        {
            if (Joining == JoiningType.RightJoining)
                return joinPrevious ? Final : Isolated;
            if (joinPrevious && joinNext) return Medial;
            if (joinPrevious) return Final;
            if (joinNext) return Initial;
            return Isolated;
        }
    }
}
