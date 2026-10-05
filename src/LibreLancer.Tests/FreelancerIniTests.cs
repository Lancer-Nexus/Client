using System;
using System.IO;
using LibreLancer.Data.IO;
using LibreLancer.Data.Schema;
using Xunit;

namespace LibreLancer.Tests;

public class FreelancerIniTests
{
    [Fact]
    public void LibrelancerIniUsesFlatPackageSupportFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "librelancer-ini-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "librelancer.ini"),
                "[Freelancer]\ndata path = data\n");

            var ini = new FreelancerIni(FileSystem.FromPath(directory));

            Assert.Equal("data" + Path.DirectorySeparatorChar, ini.DataPath);
            Assert.Equal("newplayer.fl", ini.NewPlayerPath);
            Assert.Equal("mpnewcharacter.fl", ini.MpNewCharacterPath);
            Assert.Null(ini.DacomPath);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
