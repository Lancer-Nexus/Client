using System;
using System.IO;
using System.Linq;
using LibreLancer.Data.IO;
using LibreLancer.Data.Schema.Effects;
using LibreLancer.Data.Schema.Fuses;
using Xunit;

namespace LibreLancer.Tests;

public sealed class FuseAndExplosionIniTests
{
    [Theory]
    [InlineData("particles", "vanilla_particle_effect")]
    [InlineData("effect", "legacy_effect_alias")]
    public void FuseStartEffectReadsVanillaParticlesAndEffectKeys(string key, string value)
    {
        var root = Path.Combine(Path.GetTempPath(), "fuse-start-effect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "fuse.ini"), $"""
                [fuse]
                name = test_fuse

                [start_effect]
                {key} = {value}
                at_t = 0.9
                """);

            var parsed = new FuseIni();
            parsed.AddFuseIni("fuse.ini", FileSystem.FromPath(root));

            var effect = Assert.IsType<FuseStartEffect>(Assert.Single(Assert.Single(parsed.Fuses).Actions));
            Assert.Equal(value, effect.Effect);
            Assert.Equal(new System.Numerics.Vector2(-1, 0.9f), effect.AtT);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ExplosionLifetimeAcceptsScalarAndRangeForms()
    {
        var root = Path.Combine(Path.GetTempPath(), "explosion-lifetime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "explosions.ini"), """
                [explosion]
                nickname = scalar_lifetime
                lifetime = 0.0

                [explosion]
                nickname = ranged_lifetime
                lifetime = 1.0, 2.0
                """);

            var parsed = new ExplosionsIni();
            parsed.AddFile("explosions.ini", FileSystem.FromPath(root));

            Assert.Equal(new System.Numerics.Vector2(-1, 0), parsed.Explosions[0].Lifetime);
            Assert.Equal(new System.Numerics.Vector2(1, 2), parsed.Explosions[1].Lifetime);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void FuseDamageActionsReadRootAndGroupDamage()
    {
        var root = Path.Combine(Path.GetTempPath(), "fuse-damage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "fuse.ini"), """
                [fuse]
                name = root_damage
                lifetime = 1

                [damage_root]
                at_t = 0.25
                damage_type = absolute
                hitpoints = 800

                [fuse]
                name = group_damage
                lifetime = 1

                [damage_group]
                group_name = Li_star_wing_lod1
                at_t = 0.5
                damage_type = absolute
                hitpoints = 50
                """);

            var parsed = new FuseIni();
            parsed.AddFuseIni("fuse.ini", FileSystem.FromPath(root));

            var rootAction = Assert.IsType<FuseDamageRoot>(Assert.Single(parsed.Fuses[0].Actions));
            Assert.Equal("absolute", rootAction.DamageType);
            Assert.Equal(800, rootAction.Hitpoints);
            Assert.Equal(new System.Numerics.Vector2(-1, 0.25f), rootAction.AtT);

            var groupAction = Assert.IsType<FuseDamageGroup>(Assert.Single(parsed.Fuses[1].Actions));
            Assert.Equal("Li_star_wing_lod1", groupAction.GroupName);
            Assert.Equal("absolute", groupAction.DamageType);
            Assert.Equal(50, groupAction.Hitpoints);
            Assert.Equal(new System.Numerics.Vector2(-1, 0.5f), groupAction.AtT);

            File.WriteAllText(Path.Combine(root, "range.ini"), """
                [fuse]
                name = ranged_timing

                [destroy_group]
                group_name = random
                fate = debris
                at_t = 0.1, 0.9
                """);
            var ranged = new FuseIni();
            ranged.AddFuseIni("range.ini", FileSystem.FromPath(root));
            var rangedAction = Assert.IsType<FuseDestroyGroup>(Assert.Single(Assert.Single(ranged.Fuses).Actions));
            Assert.Equal(new System.Numerics.Vector2(0.1f, 0.9f), rangedAction.AtT);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DestroyHpAttachmentReadsHardpointAndFate()
    {
        var root = Path.Combine(Path.GetTempPath(), "fuse-attachment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "fuse.ini"), """
                [fuse]
                name = attachment_destruction

                [destroy_hp_attachment]
                hardpoint = HpTurret_u1_01
                fate = debris
                at_t = 0.0, 1.0
                """);

            var parsed = new FuseIni();
            parsed.AddFuseIni("fuse.ini", FileSystem.FromPath(root));

            var action = Assert.IsType<FuseDestroyHpAttachment>(Assert.Single(Assert.Single(parsed.Fuses).Actions));
            Assert.Equal("HpTurret_u1_01", action.Hardpoint);
            Assert.Equal(FusePartFate.debris, action.Fate);
            Assert.Equal(new System.Numerics.Vector2(0, 1), action.AtT);

            File.WriteAllText(Path.Combine(root, "loot.ini"), """
                [fuse]
                name = attachment_loot

                [destroy_hp_attachment]
                hardpoint = HpWeapon01
                fate = loot
                at_t = 0
                """);
            var lootFuse = new FuseIni();
            lootFuse.AddFuseIni("loot.ini", FileSystem.FromPath(root));
            var lootAction = Assert.IsType<FuseDestroyHpAttachment>(Assert.Single(Assert.Single(lootFuse.Fuses).Actions));
            Assert.Equal(FusePartFate.loot, lootAction.Fate);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void MakeInvincibleAndDumpCargoFuseActionsParse()
    {
        var root = Path.Combine(Path.GetTempPath(), "fuse-cargo-actions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "fuse.ini"), """
                [fuse]
                name = cargo_actions

                [make_invincible]
                turn_on = true
                at_t = 0

                [dump_cargo]
                origin_hardpoint = HpMount
                at_t = 0.5
                """);

            var parsed = new FuseIni();
            parsed.AddFuseIni("fuse.ini", FileSystem.FromPath(root));

            var actions = Assert.Single(parsed.Fuses).Actions;
            var invincible = Assert.IsType<FuseMakeInvincible>(actions[0]);
            Assert.True(invincible.TurnOn);
            Assert.Equal(new System.Numerics.Vector2(-1, 0), invincible.AtT);
            var dump = Assert.IsType<FuseDumpCargo>(actions[1]);
            Assert.Equal("HpMount", dump.OriginHardpoint);
            Assert.Equal(new System.Numerics.Vector2(-1, 0.5f), dump.AtT);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void TumbleFuseParsesAngularDragAndThrottleRanges()
    {
        var root = Path.Combine(Path.GetTempPath(), "fuse-tumble-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "fuse.ini"), """
                [fuse]
                name = tumble_test

                [tumble]
                at_t = 0
                ang_drag_scale = 0.3
                turn_throttle_z = 0.7, 0.95
                turn_throttle_x = 0.0, 0.1
                turn_throttle_y = 0.0, 0.1
                throttle = 1.0, 1.0
                """);

            var parsed = new FuseIni();
            parsed.AddFuseIni("fuse.ini", FileSystem.FromPath(root));
            var action = Assert.IsType<FuseTumble>(Assert.Single(Assert.Single(parsed.Fuses).Actions));
            Assert.Equal(0.3f, action.AngularDragScale);
            Assert.Equal(new System.Numerics.Vector2(0.7f, 0.95f), action.TurnThrottleZ);
            Assert.Equal(new System.Numerics.Vector2(0, 0.1f), action.TurnThrottleX);
            Assert.Equal(new System.Numerics.Vector2(0, 0.1f), action.TurnThrottleY);
            Assert.Equal(new System.Numerics.Vector2(1, 1), action.Throttle);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
