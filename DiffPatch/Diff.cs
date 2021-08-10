using System;

namespace DiffPatch
{
    public class Diff
    {
        public Diff(Operation operation, string text)
        {
            Operation = operation;
            Text = text;
        }

        public override string ToString() =>
            Operation switch
            {
                Operation.Equal => ' ',
                Operation.Insert => '+',
                Operation.Delete => '-',
                _ => throw new ArgumentOutOfRangeException()
            } + Text;

        public Operation Operation { get; }
        public string Text { get; set; }
    }
}
