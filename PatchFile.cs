using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DiffPatch
{
    public class PatchFile
    {
        private static readonly Regex HunkOffsetRegex = new Regex(@"@@ -(\d+),(\d+) \+([_\d]+),(\d+) @@", RegexOptions.Compiled);

        public static PatchFile FromText(string patchText, bool verifyHeaders = true) =>
            FromLines(patchText.Split('\n').Select(l => l.TrimEnd('\r')), verifyHeaders);

        public static PatchFile FromLines(IEnumerable<string> lines, bool verifyHeaders = true)
        {
            var patchFile = new PatchFile();
            Patch patch = null;
            int delta = 0;

            int i = 0;
            foreach (var line in lines)
            {
                i++;

                // Ignore blank lines
                if (line.Length == 0)
                    continue;

                // Context
                if (patch == null && line[0] != '@')
                {
                    if (i == 1 && line.StartsWith("--- "))
                        patchFile.BasePath = line.Substring(4);
                    else if (i == 2 && line.StartsWith("+++ "))
                        patchFile.PatchedPath = line.Substring(4);
                    else
                        throw new ArgumentException($"Invalid context line:{line}");

                    continue;
                }

                switch (line[0])
                {
                    case '@':
                        var m = HunkOffsetRegex.Match(line);

                        if (!m.Success)
                            throw new ArgumentException($"Invalid patch line {i}:{line}");

                        patch = new Patch
                        {
                            Start1 = int.Parse(m.Groups[1].Value) - 1,
                            Length1 = int.Parse(m.Groups[2].Value),
                            Length2 = int.Parse(m.Groups[4].Value)
                        };

                        // Auto calc applied offset
                        if (m.Groups[3].Value == "_")
                        {
                            patch.Start2 = patch.Start1 + delta;
                        }
                        else
                        {
                            patch.Start2 = int.Parse(m.Groups[3].Value) - 1;
                            if (verifyHeaders && patch.Start2 != patch.Start1 + delta)
                                throw new ArgumentException($"Applied Offset Mismatch. Expected: {patch.Start1 + delta + 1}, Actual: {patch.Start2 + 1}");
                        }

                        delta += patch.Length2 - patch.Length1;
                        patchFile.patches.Add(patch);
                        break;
                    case ' ':
                        patch.Diffs.Add(new Diff(Operation.Equal, line.Substring(1)));
                        break;
                    case '+':
                        patch.Diffs.Add(new Diff(Operation.Insert, line.Substring(1)));
                        break;
                    case '-':
                        patch.Diffs.Add(new Diff(Operation.Delete, line.Substring(1)));
                        break;
                    default:
                        throw new ArgumentException($"Invalid patch line {i}:{line}");
                }
            }

            if (verifyHeaders)
            {
                foreach (var p in patchFile.patches)
                {
                    if (p.Length1 != p.ContextLines.Count())
                        throw new ArgumentException($"Context length doesn't match contents: {p.Header}");
                    if (p.Length2 != p.PatchedLines.Count())
                        throw new ArgumentException($"Patched length doesn't match contents: {p.Header}");
                }
            }

            return patchFile;
        }

        public string ToString(bool autoOffset = false)
        {
            var sb = new StringBuilder();
            if (BasePath != null && PatchedPath != null)
            {
                sb.Append("--- ").AppendLine(BasePath);
                sb.Append("+++ ").AppendLine(PatchedPath);
            }

            foreach (var p in patches)
            {
                sb.AppendLine(autoOffset ? p.AutoHeader : p.Header);
                foreach (var diff in p.Diffs)
                    sb.AppendLine(diff.ToString());
            }

            return sb.ToString();
        }

        public string BasePath { get; set; }
        public string PatchedPath { get; set; }

        public List<Patch> patches = new();

        public bool IsEmpty => patches.Count == 0;
    }
}
