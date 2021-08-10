using System.Collections.Generic;
using System.Linq;

namespace DiffPatch
{
    public class LineMatchedDiffer : PatienceDiffer
    {
        public LineMatchedDiffer() : base()  { }

        public LineMatchedDiffer(CharRepresenter charRep) : base(charRep) { }

        public override int[] Match(IReadOnlyList<string> lines1, IReadOnlyList<string> lines2)
        {
            var matches = base.Match(lines1, lines2);

            WordModeLines1 = lines1.Select(CharRep.WordsToChars).ToArray();
            WordModeLines2 = lines2.Select(CharRep.WordsToChars).ToArray();

            new FuzzyLineMatcher
            {
                MinMatchScore = MinMatchScore,
                MaxMatchOffset = MaxMatchOffset
            }
                .MatchLinesByWords(matches, WordModeLines1, WordModeLines2);

            return matches;
        }

        public string[] WordModeLines1 { get; private set; }
        public string[] WordModeLines2 { get; private set; }

        public int MaxMatchOffset { get; set; } = MatchMatrix.DefaultMaxOffset;
        public float MinMatchScore { get; set; } = FuzzyLineMatcher.DefaultMinMatchScore;
    }
}
