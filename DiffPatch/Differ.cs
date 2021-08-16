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

        public abstract int[] Match(IList<string> lines1, IList<string> lines2);

        public List<Diff> Diff(IList<string> lines1, IList<string> lines2) => LineMatching.MakeDiffList(Match(lines1, lines2), lines1, lines2);

        public List<Patch> MakePatches(IList<string> lines1, IList<string> lines2, int numContextLines = DefaultContext, bool collate = true) => MakePatches(Diff(lines1, lines2), numContextLines, collate);

        public static List<Patch> MakePatches(List<Diff> diffs, int numContextLines = DefaultContext, bool collate = true)
        {
            var p = new Patch { Diffs = diffs };

            p.RecalculateLength();
            p.Trim(numContextLines);

            if (p.Length1 == 0 && p.Diffs.Count == 0)
                return new List<Patch>();

            if (!collate)
                p.Uncollate();

            return p.Split(numContextLines);
        }

        public PatchFile DiffFile(string path1, string path2, int numContextLines = DefaultContext, bool collate = true, bool includePaths = true)
        {
            var patch = new PatchFile()
            {
                Patches = MakePatches(File.ReadAllLines(path1), File.ReadAllLines(path2), numContextLines, collate)
            };

            if (includePaths)
            {
                patch.BasePath = path1;
                patch.PatchedPath = path2;
            }

            return patch;
        }

        public PatchFile DiffLines(IList<string> lines1, IList<string> lines2, int numContextLines = DefaultContext, bool collate = true)
        {
            return new()
            {
                Patches = MakePatches(lines1, lines2, numContextLines, collate)
            };
        }

        public CharRepresenter CharRep { get; }
    }
}
