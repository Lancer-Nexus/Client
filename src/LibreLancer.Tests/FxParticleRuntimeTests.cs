using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using LibreLancer.Fx;
using Xunit;

namespace LibreLancer.Tests;

public class FxParticleRuntimeTests
{
    private static ParticleEffect CreateEffect(FxEmitter emitter, FxAppearance appearance)
    {
        var emitterNode = new EmitterReference(emitter);
        var appearanceNode = new AppearanceReference(appearance);
        emitterNode.Linked = appearanceNode;
        return new ParticleEffect(1, "test", [emitterNode], [appearanceNode], [emitterNode, appearanceNode]);
    }

    private static ParticleEffectInstance CreateInstance(ParticleEffect effect)
    {
        var instance = new ParticleEffectInstance(effect)
        {
            Pool = (ParticleEffectPool)RuntimeHelpers.GetUninitializedObject(typeof(ParticleEffectPool))
        };
        return instance;
    }

    [Fact]
    public void InitialParticlesSpawnOnceAndReceiveStableIds()
    {
        var emitter = new FxCubeEmitter("cube") { InitialParticles = 2 };
        emitter.InitLifeSpan = new(1);
        var effect = CreateEffect(emitter, new FxParticleAppearance("particle"));
        var instance = CreateInstance(effect);

        instance.Update(0, Matrix4x4.Identity, 0);
        Assert.Equal(2, instance.Buffer.GetCount(0));
        var firstId = instance.Buffer[0, 0].Id;
        var secondId = instance.Buffer[0, 1].Id;
        Assert.NotEqual(firstId, secondId);

        instance.Update(0, Matrix4x4.Identity, 0);
        Assert.Equal(2, instance.Buffer.GetCount(0));
        Assert.Equal(firstId, instance.Buffer[0, 0].Id);
        Assert.Equal(secondId, instance.Buffer[0, 1].Id);
    }

    [Fact]
    public void ParticleLifeEffectIsUpdatedAndReleasedWithItsParticle()
    {
        var emitter = new FxCubeEmitter("cube") { InitialParticles = 1 };
        emitter.InitLifeSpan = new(0.1f);
        var appearance = new FxParticleAppearance("particle");
        var effect = CreateEffect(emitter, appearance);
        var childEffect = new ParticleEffect(2, "life", [], [], []);
        typeof(FxParticleAppearance).GetField("LifeEffect", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(appearance, childEffect);
        var instance = CreateInstance(effect);

        instance.Update(0.05, Matrix4x4.Identity, 0);
        var particleId = instance.Buffer[0, 0].Id;
        Assert.True(instance.TryGetChildEffect(0, particleId, out var child));
        Assert.Equal(0.05, child!.GlobalTime);

        instance.Update(0.2, Matrix4x4.Identity, 0);
        Assert.Equal(0, instance.Buffer.GetCount(0));
        Assert.False(instance.TryGetChildEffect(0, particleId, out _));
    }
}
