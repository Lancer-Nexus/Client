using LibreLancer.Data;
using LibreLancer.Data.Schema.Save;
using LibreLancer.Net.Protocol;
using LiteNetLib.Utils;
using Xunit;

namespace LibreLancer.Tests;

public class VisitBundleEmptyTests
{
    private static VisitBundle RoundTrip(VisitEntry[] visits)
    {
        var writer = new PacketWriter();
        VisitBundle.Compress(visits).Put(writer);
        return VisitBundle.Read(new PacketReader(new NetDataReader(writer.GetCopy())));
    }

    [Fact]
    public void NewPilotWithNoVisitsRoundTripsWithoutIndexingAnEmptyArray()
    {
        Assert.Empty(RoundTrip([]).Visits);
    }

    [Fact]
    public void ExistingVisitsKeepSortedHashesAndFlags()
    {
        var restored = RoundTrip([new() { Obj = (HashValue)200u, Visit = 3 },
                                  new() { Obj = (HashValue)100u, Visit = 1 }]);
        Assert.Equal(2, restored.Visits.Length);
        Assert.Equal(100u, restored.Visits[0].Obj.Hash);
        Assert.Equal(1, restored.Visits[0].Visit);
        Assert.Equal(200u, restored.Visits[1].Obj.Hash);
        Assert.Equal(3, restored.Visits[1].Visit);
    }
}
