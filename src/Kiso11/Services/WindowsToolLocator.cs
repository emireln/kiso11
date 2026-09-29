using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Kiso11.Services;

public static class WindowsToolLocator
{
    public static string FindDism()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dism.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Windows Kits", "10", "Assessment and Deployment Kit", "Deployment Tools", "amd64", "DISM", "dism.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Windows Kits", "11", "Assessment and Deployment Kit", "Deployment Tools", "amd64", "DISM", "dism.exe")
        }.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (candidates.Count == 0) return "dism.exe";

        return candidates
            .OrderByDescending(GetToolVersion)
            .First();
    }

    private static Version GetToolVersion(string path)
    {
        var info = FileVersionInfo.GetVersionInfo(path);
        return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
    }
}
