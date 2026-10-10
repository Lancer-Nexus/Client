// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Collections.Generic;
using System.Numerics;
using LibreLancer.Data.Ini;

namespace LibreLancer.Data.Schema.Fuses;

public abstract class FuseAction
{
    [Entry("at_t", MinMax = true)]
    public Vector2 AtT;
}

[ParsedSection]
public partial class FuseDestroyRoot : FuseAction
{
}

[ParsedSection]
public partial class FuseStartEffect : FuseAction //[start_effect]
{
    public string Effect = null!;
    [Entry("hardpoint", Multiline = true)]
    public List<string> Hardpoints = [];
    [Entry("attached")]
    public bool Attached;
    [Entry("pos_offset")]
    public Vector3 PosOffset;
    [Entry("ori_offset")]
    public Vector3 OriOffset;

    [EntryHandler("effect", MinComponents = 1)]
    [EntryHandler("particles", MinComponents = 1)]
    private void HandleEffect(Entry e) => Effect = e[0].ToString();
}
[ParsedSection]
public partial class FuseDestroyHpAttachment : FuseAction //[destroy_hp_attachment]
{
    [Entry("hardpoint")]
    public string? Hardpoint;
    [Entry("fate")]
    public FusePartFate Fate;
}
public enum FusePartFate
{
    NONE,
    disappear,
    debris,
    loot
}
[ParsedSection]
public partial class FuseDestroyGroup : FuseAction //[destroy_group]
{
    [Entry("group_name")]
    public string? GroupName;
    [Entry("fate")]
    public FusePartFate Fate;
}
[ParsedSection]
public partial class FuseStartCamParticles : FuseAction //[start_cam_particles]
{
    [Entry("effect")]
    public string? Effect;
    [Entry("pos_offset")]
    public Vector3 PosOffset;
    [Entry("ori_offset")]
    public Vector3 OriOffset;
}
[ParsedSection]
public partial class FuseIgniteFuse : FuseAction //[ignite_fuse]
{
    [Entry("fuse")]
    public string? Fuse;
    [Entry("fuse_t")]
    public float FuseT;
}
[ParsedSection]
public partial class FuseImpulse : FuseAction //[impulse]
{
    [Entry("hardpoint")]
    public string? Hardpoint;
    [Entry("pos_offset")]
    public Vector3 PosOffset;
    [Entry("radius")]
    public float Radius;
    [Entry("damage")]
    public float Damage;
    [Entry("force")]
    public float Force;
}

[ParsedSection]
public partial class FuseDamageRoot : FuseAction
{
    [Entry("damage_type")]
    public string? DamageType;
    [Entry("hitpoints")]
    public float Hitpoints;
}

[ParsedSection]
public partial class FuseDamageGroup : FuseAction
{
    [Entry("group_name")]
    public string? GroupName;
    [Entry("damage_type")]
    public string? DamageType;
    [Entry("hitpoints")]
    public float Hitpoints;
}

[ParsedSection]
public partial class FuseMakeInvincible : FuseAction
{
    [Entry("turn_on")]
    public bool TurnOn;
}

[ParsedSection]
public partial class FuseDumpCargo : FuseAction
{
    [Entry("origin_hardpoint")]
    public string? OriginHardpoint;
}

[ParsedSection]
public partial class FuseTumble : FuseAction
{
    [Entry("ang_drag_scale")]
    public float AngularDragScale = 1;
    [Entry("turn_throttle_x", MinMax = true)]
    public Vector2 TurnThrottleX;
    [Entry("turn_throttle_y", MinMax = true)]
    public Vector2 TurnThrottleY;
    [Entry("turn_throttle_z", MinMax = true)]
    public Vector2 TurnThrottleZ;
    [Entry("throttle", MinMax = true)]
    public Vector2 Throttle;
}
