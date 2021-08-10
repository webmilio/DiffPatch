using System.Collections.Generic;

namespace DiffPatch
{
    public class PatienceDiffer : Differ
    {
        public PatienceDiffer() : base() { }

        public PatienceDiffer(CharRepresenter charRep) : base(charRep) { }

        public override int[] Match(IReadOnlyList<string> lines1, IReadOnlyList<string> lines2)
        {
            LineModeString1 = CharRep.LinesToChars(lines1);
            LineModeString2 = CharRep.LinesToChars(lines2);
            return new PatienceMatch().Match(LineModeString1, LineModeString2, CharRep.MaxLineChar);
        }

        public string LineModeString1 { get; private set; }
        public string LineModeString2 { get; private set; }
    }
}