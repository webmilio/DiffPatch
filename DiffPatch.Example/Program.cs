using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace DiffPatch.Example
{
    internal class Program
    {
        private const string 
            OriginalFilePath = "Example.OriginalFile.cs",
            NewFilePath = "Example.NewFile.cs",
            PatchPath = "Example.OriginalFile.cs.patch";

        private static readonly Assembly _assembly = Assembly.GetExecutingAssembly();


        private static async Task Main(string[] args)
        {
            var original = await GetLines(OriginalFilePath);
            var @new = await GetLines(NewFilePath);

            await CreatePatch(original, @new, PatchPath);
            var lines = await ApplyPatch(original, PatchPath);

            Console.WriteLine(string.Join('\n', lines));
        }

        private static async Task CreatePatch(IList<string> original, IList<string> @new, string result)
        {
            var differ = new LineMatchedDiffer();
            var diff = differ.DiffLines(original, @new);

            await File.WriteAllTextAsync(result, diff.ToString(true));
        }

        private static async Task<string[]> ApplyPatch(IList<string> original, string patch)
        {
            var patchFile = PatchFile.FromText(await File.ReadAllTextAsync(patch));
            var patcher = new Patcher(patchFile.Patches, original);

            patcher.Patch(default);
            return patcher.ResultLines;
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
