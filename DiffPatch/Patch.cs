using System;
using System.Collections.Generic;
using System.Linq;

namespace DiffPatch
{
    public class Patch
    {
        public Patch()
        {
            Diffs = new List<Diff>();
        }

        public Patch(Patch patch)
        {
            Diffs = new List<Diff>(patch.Diffs.Select(d => new Diff(d.Operation, d.Text)));
            Start1 = patch.Start1;
            Start2 = patch.Start2;
            Length1 = patch.Length1;
            Length2 = patch.Length2;
        }

        public string Header => $"@@ -{Start1 + 1},{Length1} +{Start2 + 1},{Length2} @@";
        public string AutoHeader => $"@@ -{Start1 + 1},{Length1} +_,{Length2} @@";

        public IEnumerable<string> ContextLines => Diffs.Where(d => d.Operation != Operation.Insert).Select(d => d.Text);
        public IEnumerable<string> PatchedLines => Diffs.Where(d => d.Operation != Operation.Delete).Select(d => d.Text);
        public LineRange Range1 => new LineRange { Start = Start1, Length = Length1 };
        public LineRange Range2 => new LineRange { Start = Start2, Length = Length2 };

        public LineRange TrimmedRange1 => TrimRange(Range1);
        public LineRange TrimmedRange2 => TrimRange(Range2);

        private LineRange TrimRange(LineRange range)
        {
            int start = 0;
            while (start < Diffs.Count && Diffs[start].Operation == Operation.Equal)
                start++;

            if (start == Diffs.Count)
                return new LineRange { Start = range.Start, Length = 0 };

            int end = Diffs.Count;
            while (end > start && Diffs[end - 1].Operation == Operation.Equal)
                end--;

            return new LineRange { Start = range.Start + start, End = range.End - (Diffs.Count - end) };
        }

        public void RecalculateLength()
        {
            Length1 = Diffs.Count;
            Length2 = Diffs.Count;

            foreach (var d in Diffs)
                if (d.Operation == Operation.Delete)
                    Length2--;
                else if (d.Operation == Operation.Insert)
                    Length1--;
        }

        public override string ToString() =>
            string.Join(Environment.NewLine, Diffs.Select(d => d.ToString()).Prepend(Header));

        public void Trim(int numContextLines)
        {
            var r = TrimRange(new LineRange { Start = 0, Length = Diffs.Count });

            if (r.Length == 0)
            {
                Length1 = Length2 = 0;
                Diffs.Clear();
                return;
            }

            int trimStart = r.Start - numContextLines;
            int trimEnd = Diffs.Count - r.End - numContextLines;
            if (trimStart > 0)
            {
                Diffs.RemoveRange(0, trimStart);
                Start1 += trimStart;
                Start2 += trimStart;
                Length1 -= trimStart;
                Length2 -= trimStart;
            }

            if (trimEnd > 0)
            {
                Diffs.RemoveRange(Diffs.Count - trimEnd, trimEnd);
                Length1 -= trimEnd;
                Length2 -= trimEnd;
            }
        }

        public void Uncollate()
        {
            var uncollatedDiffs = new List<Diff>(Diffs.Count);
            var addDiffs = new List<Diff>();
            foreach (var d in Diffs)
            {
                if (d.Operation == Operation.Delete)
                {
                    uncollatedDiffs.Add(d);
                }
                else if (d.Operation == Operation.Insert)
                {
                    addDiffs.Add(d);
                }
                else
                {
                    uncollatedDiffs.AddRange(addDiffs);
                    addDiffs.Clear();
                    uncollatedDiffs.Add(d);
                }
            }
            uncollatedDiffs.AddRange(addDiffs); //patches may not end with context diffs
            Diffs = uncollatedDiffs;
        }

        public List<Patch> Split(int numContextLines)
        {
            if (Diffs.Count == 0)
                return new List<Patch>();

            var ranges = new List<LineRange>();
            int start = 0;
            int n = 0;

            for (int i = 0; i < Diffs.Count; i++)
            {
                if (Diffs[i].Operation == Operation.Equal)
                {
                    n++;
                    continue;
                }

                if (n > numContextLines * 2)
                {
                    ranges.Add(new LineRange { Start = start, End = i - n + numContextLines });
                    start = i - numContextLines;
                }

                n = 0;
            }

            ranges.Add(new LineRange { Start = start, End = Diffs.Count });

            var patches = new List<Patch>(ranges.Count);
            int end1 = Start1, end2 = Start2;
            int endDiffIndex = 0;

            foreach (var r in ranges)
            {
                int skip = r.Start - endDiffIndex;
                var p = new Patch
                {
                    Start1 = end1 + skip,
                    Start2 = end2 + skip,
                    Diffs = Diffs.Slice(r).ToList()
                };

                p.RecalculateLength();
                patches.Add(p);
                end1 = p.Start1 + p.Length1;
                end2 = p.Start2 + p.Length2;
                endDiffIndex = r.End;
            }

            return patches;
        }


        public void Combine(Patch patch2, IReadOnlyList<string> lines1)
        {
            if (Range1.Intersects(patch2.Range1) || Range2.Intersects(patch2.Range2))
                throw new ArgumentException("Patches overlap");

            while (Start1 + Length1 < patch2.Start1)
            {
                Diffs.Add(new Diff(Operation.Equal, lines1[Start1 + Length1]));
                Length1++;
                Length2++;
            }

            if (Start2 + Length2 != patch2.Start2)
                throw new ArgumentException("Unequal distance between end of patch1 and start of patch2 in context and patched");

            Diffs.AddRange(patch2.Diffs);
            Length1 += patch2.Length1;
            Length2 += patch2.Length2;
        }

        public List<Diff> Diffs { get; set; }

        public int Start1 { get; set; }
        public int Start2 { get; set; }
        public int Length1 { get; set; }
        public int Length2 { get; set; }
    }
}
