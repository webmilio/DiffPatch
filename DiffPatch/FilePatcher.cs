using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiffPatch
{
    public class FilePatcher
    {
        public void LoadBaseFile()
        {
            BaseLines = File.ReadAllLines(BasePath);
        }

        public void Patch(Patcher.Mode mode)
        {
            if (BaseLines == null)
                LoadBaseFile();

            var patcher = new Patcher(PatchFile.Patches, BaseLines);
            patcher.Patch(mode);
            Results = patcher.Results.ToList();
            PatchedLines = patcher.ResultLines;
        }

        public void Save()
        {
            File.WriteAllLines(PatchedPath, PatchedLines);
        }

        public static FilePatcher FromPatchFile(string patchFilePath, string rootDir = "")
        {
            return new()
            {
                PatchFilePath = patchFilePath,
                PatchFile = PatchFile.FromText(File.ReadAllText(patchFilePath)),
                RootDir = rootDir
            };
        }


        public string[] BaseLines { get; private set; }
        public string[] PatchedLines { get; private set; }
        public List<Patcher.Result> Results { get; private set; }

        public string PatchFilePath { get; set; }

        public PatchFile PatchFile { get; set; }
        public string RootDir { get; set; } = "";

        public string BasePath => Path.Combine(RootDir, PatchFile.BasePath);
        public string PatchedPath => Path.Combine(RootDir, PatchFile.PatchedPath);
    }
}
