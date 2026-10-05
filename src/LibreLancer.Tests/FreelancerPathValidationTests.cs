using System;
using System.IO;
using LibreLancer.Exceptions;
using Xunit;

namespace LibreLancer.Tests;

public sealed class FreelancerPathValidationTests
{
    [Fact]
    public void MissingFreelancerPathUsesConfigurationException()
    {
        var config = new GameConfig
        {
            FreelancerPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        };

        var exception = Assert.Throws<InvalidFreelancerDirectory>(config.CreateFreelancerFileSystem);

        Assert.Contains(config.FreelancerPath, exception.Message);
    }

    [Fact]
    public void CreatesFilesystemForValidFreelancerDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "EXE"));
        File.WriteAllText(Path.Combine(root, "EXE", "freelancer.ini"), "[Freelancer]\n");
        try
        {
            var config = new GameConfig { FreelancerPath = root };

            var fileSystem = config.CreateFreelancerFileSystem();

            Assert.True(fileSystem.FileExists("EXE\\freelancer.ini"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
