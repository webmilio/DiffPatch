using System.Collections;
using System.Collections.Generic;

namespace DiffPatch
{
    public class ReadOnlyListSlice<T> : IReadOnlyList<T>
    {
        private readonly IReadOnlyList<T> wrapped;
        private readonly LineRange range;

        public ReadOnlyListSlice(IReadOnlyList<T> wrapped, LineRange range)
        {
            this.wrapped = wrapped;
            this.range = range;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = range.Start; i < range.End; i++)
                yield return wrapped[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public T this[int index] => wrapped[index + range.Start];

        public int Count => range.Length;
    }

    public static class SliceExtension
    {
        public static IReadOnlyList<T> Slice<T>(this IReadOnlyList<T> list, LineRange range) =>
            range.Start == 0 && range.Length == list.Count ? list :
            new ReadOnlyListSlice<T>(list, range);

        public static IReadOnlyList<T> Slice<T>(this IReadOnlyList<T> list, int start, int len) =>
            new ReadOnlyListSlice<T>(list, new LineRange { Start = start, Length = len });
    }
}
