namespace DiffPatch
{
    public class Diff
    {
        public Diff(Operation op, string text)
        {
            Operation = op;
            Text = text;
        }

        public override string ToString() =>
            Operation switch
            {
                Operation.Equal => ' ',
                Operation.Insert => '+',
                _ => '-'
            } + Text;

        public Operation Operation { get; }
        public string Text { get; set; }
    }
}
