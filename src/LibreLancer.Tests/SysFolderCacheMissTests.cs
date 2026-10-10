using System;
using System.IO;
using LibreLancer.Data.IO;
using Xunit;

namespace LibreLancer.Tests;

public sealed class SysFolderCacheMissTests
{
    [Fact]
    public void ResolvesCaseInsensitivePathWhenCachedIndexMissesFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "sysfolder-cache-miss-" + Guid.NewGuid().ToString("N"));
        var interfaceDirectory = Path.Combine(root, "DATA", "INTERFACE");
        Directory.CreateDirectory(Path.Combine(root, "EXE"));
        Directory.CreateDirectory(interfaceDirectory);

        try
        {
            var fs = FileSystem.FromPath(root);
            File.WriteAllText(Path.Combine(interfaceDirectory, "interface.generic.vms"), "interface-library");

            const string path = @"EXE\..\data/INTERFACE/interface.generic.vms";
            Assert.True(fs.FileExists(path));
            Assert.Equal("interface-library", fs.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void MergesDirectoriesThatDifferOnlyByCase()
    {
        var root = Path.Combine(Path.GetTempPath(), "sysfolder-case-collision-" + Guid.NewGuid().ToString("N"));
        var upper = Path.Combine(root, "DATA", "FX", "MISC");
        var lower = Path.Combine(root, "DATA", "FX", "misc");
        Directory.CreateDirectory(upper);
        Directory.CreateDirectory(lower);
        File.WriteAllText(Path.Combine(upper, "upper-only.ale"), "upper");
        File.WriteAllText(Path.Combine(lower, "lower-only.ale"), "lower");

        try
        {
            var fs = FileSystem.FromPath(root);
            const string lowerPath = "DATA/FX/misc/lower-only.ale";
            const string upperPath = "DATA/FX/misc/upper-only.ale";

            Assert.True(fs.FileExists(lowerPath));
            Assert.True(fs.FileExists(upperPath));
            Assert.Equal("lower", fs.ReadAllText(lowerPath));
            Assert.Equal("upper", fs.ReadAllText(upperPath));
            Assert.Contains("upper-only.ale", fs.GetFiles("DATA/FX/misc"));
            Assert.Contains("lower-only.ale", fs.GetFiles("DATA/FX/MISC"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
