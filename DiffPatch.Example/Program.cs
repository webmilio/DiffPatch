using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace DiffPatch.Example
{
    internal class Program
    {
        private static readonly Assembly _assembly = Assembly.GetExecutingAssembly();


        private static async Task Main(string[] args)
        {
            var differ = new LineMatchedDiffer();

            var lines1 = await GetLines("Example.OriginalFile.cs");
            var lines2 = await GetLines("Example.NewFile.cs");

            var diff = differ.DiffLines(lines1, lines2);

            await File.WriteAllTextAsync("Example.OriginalFile.cs.patch", diff.ToString(true));
        }

        private static async Task<List<string>> GetLines(string file)
        {
            await using var stream = _assembly.GetManifestResourceStream($"DiffPatch.Example.{file}");
            using var reader = new StreamReader(stream);

            List<string> lines = new();

            while (!reader.EndOfStream)
                lines.Add(await reader.ReadLineAsync());

            return lines;
        }
    }
}
