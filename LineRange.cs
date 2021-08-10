using System;
using System.Collections.Generic;
using System.Linq;

namespace DiffPatch
{
    public struct LineRange : IEquatable<LineRange>
    {
        public LineRange Map(Func<int, int> f) => new()
        {
            Start = f(Start), 
            End = f(End)
        };

        public bool Contains(int i) => Start <= i && i < End;
        public bool Contains(LineRange r) => r.Start >= Start && r.End <= End;
        public bool Intersects(LineRange r) => r.Start < End || r.End > Start;

        public override string ToString() => "[" + Start + "," + End + ")";

        public static bool operator ==(LineRange r1, LineRange r2) => r1.Start == r2.Start && r1.End == r2.End;
        public static bool operator !=(LineRange r1, LineRange r2) => r1.Start != r2.Start || r1.End != r2.End;

        public static LineRange operator +(LineRange r, int i) => new()
        {
            Start = r.Start + i, 
            End = r.End + i
        };
        public static LineRange operator -(LineRange r, int i) => new()
        {
            Start = r.Start - i, 
            End = r.End - i
        };

        public static LineRange Union(LineRange r1, LineRange r2) => new LineRange
        {
            Start = Math.Min(r1.Start, r2.Start),
            End = Math.Max(r1.End, r2.End)
        };

        public static LineRange Intersection(LineRange r1, LineRange r2) => new LineRange
        {
            Start = Math.Max(r1.Start, r2.Start),
            End = Math.Min(r1.End, r2.End)
        };

        public IEnumerable<LineRange> Except(IEnumerable<LineRange> except, bool presorted = false)
        {
            if (!presorted)
                except = except.OrderBy(r => r.Start);

            int start = Start;

            foreach (var r in except)
            {
                if (r.Start - start > 0)
                    yield return new LineRange 
                    { 
                        Start = start,
                        End = r.Start
                    };

                start = r.End;
            }

            if (End - start > 0)
                yield return new LineRange
                {
                    Start = start, 
                    End = End
                };
        }

        public override bool Equals(object obj) => obj is LineRange range && Equals(range);

        public bool Equals(LineRange other) => this == other;

        public override int GetHashCode() => End * End + Start; // Elegant pairing function, seems appropriate, probably reduces hash collisions when truncating hash

        public int Start { get; set; }
        public int End { get; set; }

        public int Length
        {
            get => End - Start;
            set => End = Start + value;
        }

        public int Last
        {
            get => End - 1;
            set => End = value + 1;
        }

        public int First
        {
            get => Start;
            set => Start = value;
        }
    }
}
