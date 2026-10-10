using System;
using System.IO;
using LibreLancer.Data.IO;
using Xunit;

namespace LibreLancer.Tests;

public sealed class FreelancerDataOverlayTests
{
    [Fact]
    public void OverridesDataFilesAndFallsBackToFreelancerInstallation()
    {
        var root = Path.Combine(Path.GetTempPath(), "freelancer-overlay-" + Guid.NewGuid().ToString("N"));
        var installData = Path.Combine(root, "DATA", "MISSIONS", "M01A");
        var overlayData = Path.Combine(root, "overlay", "MISSIONS", "M01A");
        Directory.CreateDirectory(Path.Combine(root, "EXE"));
        Directory.CreateDirectory(installData);
        Directory.CreateDirectory(overlayData);
        File.WriteAllText(Path.Combine(root, "EXE", "freelancer.ini"), "[Freelancer]\n");
        File.WriteAllText(Path.Combine(installData, "m01a.ini"), "vanilla");
        File.WriteAllText(Path.Combine(installData, "fallback.ini"), "installation");
        File.WriteAllText(Path.Combine(overlayData, "m01a.ini"), "overlay");

        try
        {
            var fs = FileSystem.FromPath(root);
            fs.FileProviders.Add(new FreelancerDataOverlayFileProvider(Path.Combine(root, "overlay")));

            Assert.Equal("overlay", fs.ReadAllText("EXE/../data/MISSIONS/M01A/m01a.ini"));
            Assert.Equal("installation", fs.ReadAllText("EXE/../data/MISSIONS/M01A/fallback.ini"));
            Assert.Contains("m01a.ini", fs.GetFiles("EXE/../data/MISSIONS/M01A"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
