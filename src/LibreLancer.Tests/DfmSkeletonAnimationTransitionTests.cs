using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using LibreLancer.Render;
using LibreLancer.Utf.Anm;
using LibreLancer.Utf.Dfm;
using Xunit;

namespace LibreLancer.Tests;

public class DfmSkeletonAnimationTransitionTests
{
    [Fact]
    public void NewClipBlendsFromThePreviousClipPose()
    {
        var manager = new DfmSkeletonManager(CreateSingleBoneDfm());
        var previousPose = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);

        manager.StartScript(CreateScript("previous", previousPose), 0, 1, 0, blendIn: 0, blendOut: 0);
        manager.UpdateScripts(1);

        var bone = manager.BodySkinning.Bones["root"];
        Assert.True(MathF.Abs(Quaternion.Dot(previousPose, bone.Rotation)) > 0.9999f);

        manager.StartScript(CreateScript("next", Quaternion.Identity), 0, 1, 0, blendIn: 5, blendOut: 0);
        manager.UpdateScripts(0.5);

        var expected = Quaternion.Slerp(previousPose, Quaternion.Identity, 0.1f);
        Assert.True(MathF.Abs(Quaternion.Dot(expected, bone.Rotation)) > 0.9999f);
    }

    [Fact]
    public void ShortFiniteClipCanReachItsFullPose()
    {
        var manager = new DfmSkeletonManager(CreateSingleBoneDfm());
        var previousPose = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        manager.StartScript(CreateScript("previous", previousPose), 0, 1, 0, blendIn: 0, blendOut: 0);
        manager.UpdateScripts(1);

        var targetPose = Quaternion.Identity;
        manager.StartScript(CreateScript("short", targetPose), 0, 1, 2.664f);
        manager.UpdateScripts(1.332);

        var bone = manager.BodySkinning.Bones["root"];
        Assert.True(MathF.Abs(Quaternion.Dot(targetPose, bone.Rotation)) > 0.9999f);
    }

    [Fact]
    public void FinishScriptAppliesTheAnimationFinalPose()
    {
        var manager = new DfmSkeletonManager(CreateSingleBoneDfm());
        var finalPose = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        manager.StartScript(CreateScript("escape", finalPose), 0, 1, 0, blendIn: 0, blendOut: 0);
        manager.UpdateScripts(0.25);

        manager.FinishScript("escape");

        var bone = manager.BodySkinning.Bones["root"];
        Assert.True(MathF.Abs(Quaternion.Dot(finalPose, bone.Rotation)) > 0.9999f);
        Assert.Empty(GetRunningScripts(manager));
    }

    private static System.Collections.ICollection GetRunningScripts(DfmSkeletonManager manager)
    {
        var field = typeof(DfmSkeletonManager).GetField("RunningScripts",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return (System.Collections.ICollection)field.GetValue(manager)!;
    }

    private static Script CreateScript(string name, Quaternion pose)
    {
        var frameData = new byte[32];
        WriteQuaternion(frameData, 0, pose);
        WriteQuaternion(frameData, 16, pose);

        var script = new Script(name);
        script.JointMaps.Add(new JointMap
        {
            ParentName = "root",
            ChildName = "root",
            Channel = new Channel(0x4, 2, 1, new AnmBuffer { Buffer = frameData })
        });
        return script;
    }

    private static void WriteQuaternion(byte[] data, int offset, Quaternion value)
    {
        BitConverter.GetBytes(value.W).CopyTo(data, offset);
        BitConverter.GetBytes(value.X).CopyTo(data, offset + 4);
        BitConverter.GetBytes(value.Y).CopyTo(data, offset + 8);
        BitConverter.GetBytes(value.Z).CopyTo(data, offset + 12);
    }

    private static DfmFile CreateSingleBoneDfm()
    {
        var bone = (Bone)RuntimeHelpers.GetUninitializedObject(typeof(Bone));
        bone.Name = "root";
        bone.Min = Vector3.Zero;
        bone.Max = Vector3.Zero;
        typeof(Bone).GetProperty(nameof(Bone.BoneToRoot))!.SetValue(bone, Matrix4x4.Identity);
        var hardpoints = typeof(Bone).GetProperty(nameof(Bone.Hardpoints))!;
        hardpoints.SetValue(bone, Activator.CreateInstance(hardpoints.PropertyType));

        var bones = new Dictionary<string, Bone> { ["root"] = bone };
        var dfm = (DfmFile)RuntimeHelpers.GetUninitializedObject(typeof(DfmFile));
        typeof(DfmFile).GetProperty(nameof(DfmFile.Parts))!.SetValue(dfm,
            new Dictionary<int, DfmPart> { [0] = new DfmPart("root", "root", bones) });
        typeof(DfmFile).GetProperty(nameof(DfmFile.Bones))!.SetValue(dfm, bones);
        typeof(DfmFile).GetProperty(nameof(DfmFile.Constructs))!.SetValue(dfm, new DfmConstructs());
        typeof(DfmFile).GetProperty(nameof(DfmFile.Levels))!.SetValue(dfm, new Dictionary<int, DfmMesh>());
        return dfm;
    }
}
