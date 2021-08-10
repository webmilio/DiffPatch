using System;
using System.Collections.Generic;

namespace DiffPatch
{
    public static class LineMatching
    {
        public static IEnumerable<(LineRange, LineRange)> UnmatchedRanges(int[] matches, int len2)
        {
            int len1 = matches.Length;
            int start1 = 0, start2 = 0;

            do
            {
                // Search for a matchpoint
                int end1 = start1;
                while (end1 < len1 && matches[end1] < 0)
                    end1++;

                int end2 = end1 == len1 ? len2 : matches[end1];

                if (end1 != start1 || end2 != start2)
                {
                    yield return (new LineRange { Start = start1, End = end1 }, new LineRange { Start = start2, End = end2 });
                    start1 = end1;
                    start2 = end2;
                }
                else
                {
                    // Matchpoint follows on from start, no unmatched lines
                    start1++;
                    start2++;
                }
            } while (start1 < len1 || start2 < len2);
        }

        public static int[] FromUnmatchedRanges(IEnumerable<(LineRange, LineRange)> unmatchedRanges, int len1)
        {
            int[] matches = new int[len1];
            int start1 = 0, start2 = 0;

            foreach (var (range1, range2) in unmatchedRanges)
            {
                while (start1 < range1.Start)
                    matches[start1++] = start2++;

                if (start2 != range2.Start)
                    throw new ArgumentException("Unequal number of lines between umatched ranges on each side");

                while (start1 < range1.End)
                    matches[start1++] = -1;

                start2 = range2.End;
            }

            while (start1 < len1)
                matches[start1++] = start2++;

            return matches;
        }

        public static IEnumerable<(LineRange, LineRange)> UnmatchedRanges(IEnumerable<Patch> patches)
        {
            foreach (var patch in patches)
            {
                var diffs = patch.Diffs;
                int start1 = patch.Start1, start2 = patch.Start2;

                for (int i = 0; i < diffs.Count;)
                {
                    // Skip matched
                    while (i < diffs.Count && diffs[i].Operation == Operation.Equal)
                    {
                        start1++;
                        start2++;
                        i++;
                    }

                    int end1 = start1, end2 = start2;
                    while (i < diffs.Count && diffs[i].Operation != Operation.Equal)
                    {
                        if (diffs[i++].Operation == Operation.Delete)
                            end1++;
                        else
                            end2++;
                    }

                    if (end1 != start1 || end2 != start2)
                        yield return (new LineRange { Start = start1, End = end1 }, new LineRange { Start = start2, End = end2 });

                    start1 = end1;
                    start2 = end2;
                }
            }
        }

        public static int[] FromPatches(IEnumerable<Patch> patches, int len1) =>
            FromUnmatchedRanges(UnmatchedRanges(patches), len1);

        public static List<Diff> MakeDiffList(int[] matches, IReadOnlyList<string> lines1, IReadOnlyList<string> lines2)
        {
            var list = new List<Diff>();
            int l = 0, r = 0;

            for (int i = 0; i < matches.Length; i++)
            {
                if (matches[i] < 0)
                    continue;

                while (l < i)
                    list.Add(new Diff(Operation.Delete, lines1[l++]));

                while (r < matches[i])
                    list.Add(new Diff(Operation.Insert, lines2[r++]));

                if (lines1[l] != lines2[r])
                {
                    list.Add(new Diff(Operation.Delete, lines1[l]));
                    list.Add(new Diff(Operation.Insert, lines2[r]));
                }
                else
                {
                    list.Add(new Diff(Operation.Equal, lines1[l]));
                }
                l++; r++;
            }

            while (l < lines1.Count)
                list.Add(new Diff(Operation.Delete, lines1[l++]));

            while (r < lines2.Count)
                list.Add(new Diff(Operation.Insert, lines2[r++]));

            return list;
        }
    }
}
