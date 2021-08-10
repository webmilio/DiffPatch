using System.Collections.Generic;
using System.IO;

namespace DiffPatch
{
    public abstract class Differ
    {
        public const int DefaultContext = 3;

        protected Differ() : this(new CharRepresenter()) { }

        protected Differ(CharRepresenter charRep)
        {
            CharRep = charRep;
        }

        public abstract int[] Match(IReadOnlyList<string> lines1, IReadOnlyList<string> lines2);

        public List<Diff> Diff(IReadOnlyList<string> lines1, IReadOnlyList<string> lines2) => LineMatching.MakeDiffList(Match(lines1, lines2), lines1, lines2);

        public List<Patch> MakePatches(IReadOnlyList<string> lines1, IReadOnlyList<string> lines2, int numContextLines = DefaultContext, bool collate = true) => MakePatches(Diff(lines1, lines2), numContextLines, collate);

        public static List<Patch> MakePatches(List<Diff> diffs, int numContextLines = DefaultContext, bool collate = true)
        {
            var p = new Patch { Diffs = diffs };

            p.RecalculateLength();
            p.Trim(numContextLines);

            if (p.Length1 == 0)
                return new List<Patch>();

            if (!collate)
                p.Uncollate();

            return p.Split(numContextLines);
        }

        public static PatchFile DiffFiles(Differ differ, string path1, string path2, string rootDir = null, int numContextLines = DefaultContext, bool collate = true)
        {
            return new PatchFile
            {
                BasePath = path1,
                PatchedPath = path2,
                patches = differ.MakePatches(File.ReadAllLines(Path.Combine(rootDir ?? "", path1)), File.ReadAllLines(Path.Combine(rootDir ?? "", path2)), numContextLines, collate)
            };
        }

        public CharRepresenter CharRep { get; }
    }
}
