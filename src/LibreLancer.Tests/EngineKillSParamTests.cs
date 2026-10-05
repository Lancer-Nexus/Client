using LibreLancer.Client.Components;
using Xunit;

namespace LibreLancer.Tests;

public class EngineKillSParamTests
{
    [Fact]
    public void EngineKillSParamTracksRemainingShipVelocity()
    {
        Assert.Equal(0.5f, CUpdateSParamComponent.CalculateSParam(true, 0, 100, 200));
    }

    [Fact]
    public void EngineKillSParamClampsToValidRange()
    {
        Assert.Equal(1f, CUpdateSParamComponent.CalculateSParam(true, 0, 300, 200));
        Assert.Equal(0f, CUpdateSParamComponent.CalculateSParam(true, 0, 0, 200));
        Assert.Equal(0f, CUpdateSParamComponent.CalculateSParam(true, 0, 100, 0));
    }

    [Fact]
    public void NormalEngineSParamRemainsUnchanged()
    {
        Assert.Equal(0.72f, CUpdateSParamComponent.CalculateSParam(false, 0.72f, 100, 200));
    }
}
