using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DiffPatch
{
    public class Patcher
    {
        public enum Mode
        {
            Exact,
            Offset,
            Fuzzy
        }

        public class Result
        {
            public string Summary()
            {
                if (!Success)
                    return $"FAILURE: {Patch.Header}";

                if (Mode == Mode.Offset)
                    return (OffsetWarning ? "WARNING" : "OFFSET") + $": {Patch.Header} offset {Offset} lines";

                if (Mode == Mode.Fuzzy)
                {
                    int q = (int)(FuzzyQuality * 100);

                    return $"FUZZY: {Patch.Header} quality {q}%" +
                        (Offset > 0 ? $" offset {Offset} lines" : "");
                }

                return $"EXACT: {Patch.Header}";
            }

            public int SearchOffset { get; set; }
            public Patch AppliedPatch { get; set; }

            public Patch Patch { get; set; }
            public bool Success { get; set; }
            public Mode Mode { get; set; }

            public int Offset { get; set; }
            public bool OffsetWarning { get; set; }
            public float FuzzyQuality { get; set; }
        }

        // Patch extended with implementation fields
        private class WorkingPatch : Patch
        {
            public WorkingPatch(Patch patch) : base(patch) { }

            public LineRange? KeepoutRange1 => Result?.AppliedPatch?.TrimmedRange1;
            public LineRange? KeepoutRange2 => Result?.AppliedPatch?.TrimmedRange2;

            public int? AppliedDelta => Result?.AppliedPatch?.Length2 - Result?.AppliedPatch?.Length1;

            public void Fail()
            {
                Result = new Result
                {
                    Patch = this,
                    Success = false
                };
            }

            public void Succeed(Mode mode, Patch appliedPatch)
            {
                Result = new Result
                {
                    Patch = this,
                    Success = true,
                    Mode = mode,
                    AppliedPatch = appliedPatch
                };
            }

            public void AddOffsetResult(int offset, int fileLength)
            {
                Result.Offset = offset; // Note that offset is different to at - start2, because offset is relative to the applied position of the last patch
                Result.OffsetWarning = offset > OffsetWarnDistance(Length1, fileLength);
            }

            public void AddFuzzyResult(float fuzzyQuality)
            {
                Result.FuzzyQuality = fuzzyQuality;
            }

            public void LinesToChars(CharRepresenter rep)
            {
                LmContext = rep.LinesToChars(ContextLines);
                LmPatched = rep.LinesToChars(PatchedLines);
            }

            public void WordsToChars(CharRepresenter rep)
            {
                WmContext = ContextLines.Select(rep.WordsToChars).ToArray();
                WmPatched = PatchedLines.Select(rep.WordsToChars).ToArray();
            }

            public Result Result { get; set; }
            public string LmContext { get; private set; }
            public string LmPatched { get; private set; }
            public string[] WmContext { get; private set; }
            public string[] WmPatched { get; private set; }
        }

        public class FuzzyMatchOptions
        {
            public int MaxMatchOffset { get; set; } = MatchMatrix.DefaultMaxOffset;
            public float MinMatchScore { get; set; } = FuzzyLineMatcher.DefaultMinMatchScore;
            public bool EnableDistancePenalty { get; set; } = true;
        }

        // The offset distance which constitutes a warning for a patch
        // Currently either 10% of file length, or 10x patch length, whichever is longer
        public static int OffsetWarnDistance(int patchLength, int fileLength) => Math.Max(patchLength * 10, fileLength / 10);

        private readonly IReadOnlyList<WorkingPatch> patches;
        private List<string> lines;
        private bool applied;

        // Last here means highest line number, not necessarily most recent.
        // Patches can only apply before lastAppliedPatch in fuzzy mode
        private Patch lastAppliedPatch;

        // We maintain delta as the offset of the last patch (applied location - expected location)
        // This way if a line is inserted, and all patches are offset by 1, only the first patch is reported as offset
        // Normally this is equivalent to `lastAppliedPatch?.AppliedOffset` but if a patch fails, we subtract its length delta from the search offset
        private int searchOffset;

        // Patches applying within this range (due to fuzzy matching) will cause patch reordering
        private LineRange ModifiedRange => new LineRange { Start = 0, End = lastAppliedPatch?.TrimmedRange2.End ?? 0 };

        private readonly CharRepresenter charRep;
        private string lmText;
        private List<string> wmLines;

        public Patcher(IEnumerable<Patch> patches, IEnumerable<string> lines)
        {
            this.patches = patches.Select(p => new WorkingPatch(p)).ToList();
            this.lines = new List<string>(lines);
        }

        public Patcher(IEnumerable<Patch> patches, IEnumerable<string> lines, CharRepresenter charRep) : this(patches, lines)
        {
            this.charRep = charRep;
        }

        public Patcher Patch(Mode mode)
        {
            if (applied)
                throw new Exception("Cannot apply the same patch twice.");

            applied = true;

            foreach (var patch in patches)
            {
                if (ApplyExact(patch))
                    continue;
                if (mode >= Mode.Offset && ApplyOffset(patch))
                    continue;
                if (mode >= Mode.Fuzzy && ApplyFuzzy(patch))
                    continue;

                patch.Fail();
                patch.Result.SearchOffset = searchOffset;
                searchOffset -= patch.Length2 - patch.Length1;
            }

            return this;
        }

        private void LinesToChars()
        {
            foreach (var patch in patches)
                patch.LinesToChars(charRep);

            lmText = charRep.LinesToChars(lines);
        }

        private void WordsToChars()
        {
            foreach (var patch in patches)
                patch.WordsToChars(charRep);

            wmLines = lines.Select(charRep.WordsToChars).ToList();
        }

        private Patch ApplyExactAt(int loc, WorkingPatch patch)
        {
            if (!patch.ContextLines.SequenceEqual(lines.GetRange(loc, patch.Length1)))
                throw new Exception("Patch engine failure");

            if (!CanApplySafelyAt(loc, patch))
                throw new Exception("Patch affects another patch");

            lines.RemoveRange(loc, patch.Length1);
            lines.InsertRange(loc, patch.PatchedLines);

            // Update the lineModeText
            if (lmText != null)
                lmText = lmText.Remove(loc) + patch.LmPatched + lmText.Substring(loc + patch.Length1);

            // Update the wordModeLines
            if (wmLines != null)
            {
                wmLines.RemoveRange(loc, patch.Length1);
                wmLines.InsertRange(loc, patch.WmPatched);
            }

            int patchedDelta = patches.Where(p => p.KeepoutRange2?.End <= loc).Sum(p => p.AppliedDelta.Value);
            Patch appliedPatch = patch;

            if (appliedPatch.Start2 != loc || appliedPatch.Start1 != loc - patchedDelta)
                appliedPatch = new Patch(patch)
                { 
                    // Create a new patch with different applied position if necessary
                    Start1 = loc - patchedDelta,
                    Start2 = loc
                };


            // Update the applied location for patches following this one in the file, but preceding it in the patch list
            // Can only happen if fuzzy matching causes a patch to move before one of the previously applied patches
            if (loc < ModifiedRange.End)
            {
                foreach (var p in patches.Where(p => p.KeepoutRange2?.Start > loc))
                    p.Result.AppliedPatch.Start2 += appliedPatch.Length2 - appliedPatch.Length1;
            }
            else
            {
                lastAppliedPatch = appliedPatch;
            }

            searchOffset = appliedPatch.Start2 - patch.Start2;
            return appliedPatch;
        }

        private bool CanApplySafelyAt(int loc, Patch patch)
        {
            if (loc >= ModifiedRange.End)
                return true;

            var range = new LineRange { Start = loc, Length = patch.Length1 };
            return patches.All(p => !p.KeepoutRange2?.Contains(range) ?? true);
        }

        private bool ApplyExact(WorkingPatch patch)
        {
            int loc = patch.Start2 + searchOffset;
            if (loc + patch.Length1 > lines.Count)
                return false;

            if (!patch.ContextLines.SequenceEqual(lines.GetRange(loc, patch.Length1)))
                return false;

            patch.Succeed(Mode.Exact, ApplyExactAt(loc, patch));
            return true;
        }

        private bool ApplyOffset(WorkingPatch patch)
        {
            if (lmText == null)
                LinesToChars();

            if (patch.Length1 > lines.Count)
                return false;

            int loc = patch.Start2 + searchOffset;
            if (loc < 0) loc = 0;
            else if (loc >= lines.Count) loc = lines.Count - 1;

            int forward = lmText.IndexOf(patch.LmContext, loc, StringComparison.Ordinal);
            int reverse = lmText.LastIndexOf(patch.LmContext, Math.Min(loc + patch.LmContext.Length, lines.Count - 1), StringComparison.Ordinal);

            if (!CanApplySafelyAt(forward, patch))
                forward = -1;
            if (!CanApplySafelyAt(reverse, patch))
                reverse = -1;

            if (forward < 0 && reverse < 0)
                return false;

            int found = reverse < 0 || forward >= 0 && (forward - loc) < (loc - reverse) ? forward : reverse;
            patch.Succeed(Mode.Offset, ApplyExactAt(found, patch));
            patch.AddOffsetResult(found - loc, lines.Count);

            return true;
        }

        private bool ApplyFuzzy(WorkingPatch patch)
        {
            if (wmLines == null)
                WordsToChars();

            int loc = patch.Start2 + searchOffset;
            if (loc + patch.Length1 > wmLines.Count)//initialise search at end of file if loc is past file length
                loc = wmLines.Count - patch.Length1;

            (int[] match, float matchQuality) = FindMatch(loc, patch.WmContext);
            if (match == null)
                return false;

            var fuzzyPatch = new WorkingPatch(AdjustPatchToMatchedLines(patch, match, lines));
            if (wmLines != null) fuzzyPatch.WordsToChars(charRep);
            if (lmText != null) fuzzyPatch.LinesToChars(charRep);

            int at = match.First(i => i >= 0); //if the patch needs lines trimmed off it, the early match entries will be negative
            patch.Succeed(Mode.Fuzzy, ApplyExactAt(at, fuzzyPatch));
            patch.AddOffsetResult(fuzzyPatch.Start2 - loc, lines.Count);
            patch.AddFuzzyResult(matchQuality);
            return true;
        }

        public static Patch AdjustPatchToMatchedLines(Patch patch, int[] match, IReadOnlyList<string> lines)
        {
            //replace the patch with a copy
            var fuzzyPatch = new Patch(patch);
            var diffs = fuzzyPatch.Diffs; //for convenience

            // Keep operations, but replace lines with lines in source text
            // Unmatched patch lines (-1) are deleted
            // Unmatched target lines (increasing offset) are added to the patch
            for (int i = 0, j = 0, ploc = -1; i < patch.Length1; i++)
            {
                int mloc = match[i];

                // Insert extra target lines into patch
                if (mloc >= 0 && ploc >= 0 && mloc - ploc > 1)
                {
                    // Delete an unmatched target line if the surrounding diffs are also DELETE, otherwise use it as context
                    var op = diffs[j - 1].Operation == Operation.Delete && diffs[j].Operation == Operation.Delete ?
                         Operation.Delete : Operation.Equal;

                    for (int l = ploc + 1; l < mloc; l++)
                        diffs.Insert(j++, new Diff(op, lines[l]));
                }
                ploc = mloc;

                // Keep insert lines the same
                while (diffs[j].Operation == Operation.Insert)
                    j++;

                if (mloc < 0) // Unmatched context line
                    diffs.RemoveAt(j);
                else // Update context to match target file (may be the same, doesn't matter)
                    diffs[j++].Text = lines[mloc];
            }

            // Finish our new patch
            fuzzyPatch.RecalculateLength();
            return fuzzyPatch;
        }

        private (int[] match, float score) FindMatch(int loc, IReadOnlyList<string> wmContext)
        {
            // Fuzzy matching is more complex because we need to split up the patched file to only search _between_ previously applied patches
            var keepoutRanges = patches.Select(p => p.KeepoutRange2).Where(r => r != null).Select(r => r.Value);

            // Parts of file to search in
            var ranges = new LineRange { Length = wmLines.Count }.Except(keepoutRanges).ToArray();

            return FuzzyMatch(wmContext, wmLines, loc, FuzzyOptions, ranges);
        }

        public static (int[] match, float score) FuzzyMatch(IReadOnlyList<string> wmPattern, IReadOnlyList<string> wmText, int loc, FuzzyMatchOptions options = default, LineRange[] ranges = default)
        {
            if (ranges == null)
                ranges = new[]
                {
                    new LineRange {Length = wmText.Count}
                };

            options ??= new FuzzyMatchOptions();

            // We're creating twice as many MatchMatrix objects as we need, incurring some wasted allocation and setup time, but it reads easier than trying to precompute all the edge cases
            var fwdMatchers = ranges.Select(r => new MatchMatrix(wmPattern, wmText, options.MaxMatchOffset, r)).SkipWhile(m => loc > m.WorkingRange.Last).ToArray();
            var revMatchers = ranges.Reverse().Select(r => new MatchMatrix(wmPattern, wmText, options.MaxMatchOffset, r)).SkipWhile(m => loc < m.WorkingRange.First).ToArray();

            int warnDist = OffsetWarnDistance(wmPattern.Count, wmText.Count);
            float penaltyPerLine = options.EnableDistancePenalty ? 1f / (10 * warnDist) : 0;

            var fwd = new MatchRunner(loc, 1, fwdMatchers, penaltyPerLine);
            var rev = new MatchRunner(loc, -1, revMatchers, penaltyPerLine);

            float bestScore = options.MinMatchScore;
            int[] bestMatch = null;
            while (fwd.Step(ref bestScore, ref bestMatch) | rev.Step(ref bestScore, ref bestMatch)) ;

            return (bestMatch, bestScore);
        }

        public string[] ResultLines => lines.ToArray();
        public IEnumerable<Result> Results => patches.Select(p => p.Result);
        public FuzzyMatchOptions FuzzyOptions { get; set; } = new();

        private struct MatchRunner
        {
            private int loc;
            private readonly int dir;
            private readonly MatchMatrix[] mms;
            private readonly float penaltyPerLine;

            // Used as a Range/Slice for the MatchMatrix array
            private LineRange active;
            private float penalty;

            public MatchRunner(int loc, int dir, MatchMatrix[] mms, float penaltyPerLine)
            {
                this.loc = loc;
                this.dir = dir;
                this.mms = mms;
                this.penaltyPerLine = penaltyPerLine;
                active = new LineRange();
                penalty = -0.1f; // Start penalty at -10%, to give some room for finding the best match if it's not "too far"
            }

            public bool Step(ref float bestScore, ref int[] bestMatch)
            {
                if (active.First == mms.Length)
                    return false;

                if (bestScore > 1f - penalty)
                    return false; // Aint getting any better than this

                // Activate matchers as we enter their working range
                while (active.End < mms.Length && mms[active.End].WorkingRange.Contains(loc))
                    active.End++;

                // Active MatchMatrix runs
                for (int i = active.First; i <= active.Last; i++)
                {
                    var mm = mms[i];
                    if (!mm.Match(loc, out float score))
                    {
                        Debug.Assert(i == active.First, "Match matricies out of order?");
                        active.First++;
                        continue;
                    }

                    if (penalty > 0) // Ignore penalty for the first 10%
                        score -= penalty;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMatch = mm.Path();
                    }
                }

                loc += dir;
                penalty += penaltyPerLine;

                return true;
            }
        }
    }
}
