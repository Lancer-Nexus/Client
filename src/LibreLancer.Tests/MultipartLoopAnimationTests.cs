using System;
using System.Collections.Generic;
using LibreLancer.Utf;
using System.Numerics;
using LibreLancer;
using LibreLancer.Utf.Anm;
using LibreLancer.World.Components;
using Xunit;

namespace LibreLancer.Tests;

public sealed class MultipartLoopAnimationTests
{
    [Fact]
    public void MultipartTracksWithMatchingFinalValuesWaitForTheLongestTrack()
    {
        var animation = new AnmFile();
        var script = new Script("Sc_loop");
        AddTimedTrack(script, "Valve_A", [0f, 0.1f, 3f], [0f, 1f, 0f]);
        AddTimedTrack(script, "Valve_B", [0f, 1f, 1.1f, 4f], [0f, 0f, 1f, 0f]);
        AddTimedTrack(script, "Valve_C", [0f, 2f, 2.1f, 5f], [0f, 0f, 1f, 0f]);
        animation.Scripts.Add(script.Name, script);

        var (component, model) = CreateComponent(script, animation);
        component.StartAnimation(script, loop: true);
        component.Update(3.1, null!);

        var a = model.Parts["Valve_A"].Construct!.LocalTransform.Position.X;
        var b = model.Parts["Valve_B"].Construct!.LocalTransform.Position.X;
        var c = model.Parts["Valve_C"].Construct!.LocalTransform.Position.X;

        Assert.InRange(a, -0.001f, 0.001f);
        Assert.InRange(b, 0.309f, 0.312f);
        Assert.InRange(c, 0.654f, 0.656f);

        component.Update(2, null!);
        Assert.InRange(model.Parts["Valve_A"].Construct!.LocalTransform.Position.X, 0.999f, 1.001f);
        Assert.InRange(model.Parts["Valve_B"].Construct!.LocalTransform.Position.X, -0.001f, 0.001f);
        Assert.InRange(model.Parts["Valve_C"].Construct!.LocalTransform.Position.X, -0.001f, 0.001f);
    }

    [Fact]
    public void TracksWithDifferentFinalValuesKeepIndependentLoops()
    {
        var animation = new AnmFile();
        var script = new Script("Sc_independent");
        AddTimedTrack(script, "Valve_A", [0f, 0.1f, 3f], [0f, 1f, 2f]);
        AddTimedTrack(script, "Valve_B", [0f, 1f, 1.1f, 4f], [0f, 0f, 1f, 3f]);
        animation.Scripts.Add(script.Name, script);

        var (component, model) = CreateComponent(script, animation);
        component.StartAnimation(script, loop: true);
        component.Update(3.1, null!);

        Assert.InRange(model.Parts["Valve_A"].Construct!.LocalTransform.Position.X, 0.999f, 1.001f);
        Assert.InRange(model.Parts["Valve_B"].Construct!.LocalTransform.Position.X, 2.37f, 2.39f);
    }

    private static (AnimationComponent Component, RigidModel Model) CreateComponent(Script script,
        AnmFile animation)
    {
        var root = new RigidModelPart { Name = "Root", Children = [] };
        var model = new RigidModel
        {
            Root = root,
            Parts = new ModelPartCollection(),
            Source = RigidModelSource.Compound
        };
        var parts = new List<RigidModelPart> { root };
        foreach (var jointMap in script.JointMaps)
        {
            var part = new RigidModelPart
            {
                Name = jointMap.ChildName,
                Construct = new PrisConstruct
                {
                    ParentName = "Root",
                    ChildName = jointMap.ChildName,
                    AxisTranslation = Vector3.UnitX,
                    Min = -10,
                    Max = 10
                }
            };
            root.Children.Add(part);
            model.Parts.Add(part);
            parts.Add(part);
        }
        model.AllParts = parts.ToArray();
        return (new AnimationComponent(model, animation), model);
    }

    private static void AddTimedTrack(Script script, string name, float[] times, float[] values)
    {
        var buffer = new AnmBuffer { Buffer = new byte[times.Length * 8] };
        for (var i = 0; i < times.Length; i++)
        {
            BitConverter.GetBytes(times[i]).CopyTo(buffer.Buffer, i * 8);
            BitConverter.GetBytes(values[i]).CopyTo(buffer.Buffer, i * 8 + 4);
        }
        script.JointMaps.Add(new JointMap
        {
            ParentName = "Root",
            ChildName = name,
            Channel = new Channel(1, times.Length, -1, buffer)
        });
    }

    [Fact]
    public void NonLoopedAnimationStartsAtRequestedClipTime()
    {
        var animation = new AnmFile();
        var script = new Script("Sc_offset");
        AddTrack(script, "Valve", 4f, 4f);
        animation.Scripts.Add(script.Name, script);

        var root = new RigidModelPart { Name = "Root", Children = [] };
        var model = new RigidModel
        {
            Root = root,
            Parts = new ModelPartCollection(),
            Source = RigidModelSource.Compound
        };
        var part = new RigidModelPart
        {
            Name = "Valve",
            Construct = new PrisConstruct
            {
                ParentName = "Root",
                ChildName = "Valve",
                AxisTranslation = Vector3.UnitX,
                Min = -10,
                Max = 10
            }
        };
        root.Children.Add(part);
        model.Parts.Add(root);
        model.Parts.Add(part);
        model.AllParts = [root, part];

        var component = new AnimationComponent(model, animation);
        component.StartAnimation(script, loop: false, start_time: 1.5f);
        component.Update(0, null!);

        Assert.InRange(model.Parts["Valve"].Construct!.LocalTransform.Position.X, 1.499f, 1.501f);
    }

    [Fact]
    public void FinishingNonLoopedAnimationAppliesFinalPose()
    {
        var animation = new AnmFile();
        var script = new Script("Sc_finish");
        AddTrack(script, "Valve", 4f, 4f);
        animation.Scripts.Add(script.Name, script);

        var root = new RigidModelPart { Name = "Root", Children = [] };
        var part = new RigidModelPart
        {
            Name = "Valve",
            Construct = new PrisConstruct
            {
                ParentName = "Root",
                ChildName = "Valve",
                AxisTranslation = Vector3.UnitX,
                Min = -10,
                Max = 10
            }
        };
        root.Children.Add(part);
        var model = new RigidModel
        {
            Root = root,
            Parts = new ModelPartCollection(),
            AllParts = [root, part],
            Source = RigidModelSource.Compound
        };
        model.Parts.Add(root);
        model.Parts.Add(part);

        var component = new AnimationComponent(model, animation);
        component.StartAnimation(script, loop: false);
        component.Update(0.25, null!);

        component.FinishAnimation(script.Name);

        Assert.InRange(part.Construct!.LocalTransform.Position.X, 3.999f, 4.001f);
        Assert.Single(component.Serialize(), x => x.Finished);
    }

    [Fact]
    public void NewClipBlendsFromTheCurrentlyDisplayedPose()
    {
        var animation = new AnmFile();
        var previous = new Script("Sc_previous");
        var next = new Script("Sc_next");
        AddTrack(previous, "Valve", 4f, 4f);
        AddTrack(next, "Valve", 4f, 0f);
        animation.Scripts.Add(previous.Name, previous);
        animation.Scripts.Add(next.Name, next);

        var root = new RigidModelPart { Name = "Root", Children = [] };
        var part = new RigidModelPart
        {
            Name = "Valve",
            Construct = new PrisConstruct
            {
                ParentName = "Root",
                ChildName = "Valve",
                AxisTranslation = Vector3.UnitX,
                Min = -10,
                Max = 10
            }
        };
        root.Children.Add(part);
        var model = new RigidModel
        {
            Root = root,
            Parts = new ModelPartCollection(),
            AllParts = [root, part],
            Source = RigidModelSource.Compound
        };
        model.Parts.Add(root);
        model.Parts.Add(part);

        var component = new AnimationComponent(model, animation);
        component.StartAnimation(previous);
        component.Update(2, null!);
        Assert.InRange(part.Construct!.LocalTransform.Position.X, 1.999f, 2.001f);

        component.StartAnimation(next);
        component.Update(0.125, null!);
        Assert.InRange(part.Construct.LocalTransform.Position.X, 0.999f, 1.001f);

        component.Update(0.125, null!);
        Assert.InRange(part.Construct.LocalTransform.Position.X, -0.001f, 0.001f);
    }

    [Fact]
    public void ReversingAnActiveClipBlendsWithoutSuppressingItself()
    {
        var animation = new AnmFile();
        var script = new Script("Sc_reverse");
        AddTrack(script, "Valve", 4f, 4f);
        animation.Scripts.Add(script.Name, script);

        var root = new RigidModelPart { Name = "Root", Children = [] };
        var part = new RigidModelPart
        {
            Name = "Valve",
            Construct = new PrisConstruct
            {
                ParentName = "Root",
                ChildName = "Valve",
                AxisTranslation = Vector3.UnitX,
                Min = -10,
                Max = 10
            }
        };
        root.Children.Add(part);
        var model = new RigidModel
        {
            Root = root,
            Parts = new ModelPartCollection(),
            AllParts = [root, part],
            Source = RigidModelSource.Compound
        };
        model.Parts.Add(root);
        model.Parts.Add(part);

        var component = new AnimationComponent(model, animation);
        component.StartAnimation(script);
        component.Update(1.5, null!);
        component.StartAnimation(script, reverse: true);
        component.Update(0, null!);
        Assert.InRange(part.Construct!.LocalTransform.Position.X, 1.499f, 1.501f);

        component.Update(0.125, null!);
        Assert.InRange(part.Construct.LocalTransform.Position.X, 1.436f, 1.439f);
    }

    [Fact]
    public void ReversingAnActiveClipAppliesTheRequestedTimeScale()
    {
        var animation = new AnmFile();
        var script = new Script("Sc_reverse_scale");
        AddTrack(script, "Valve", 4f, 4f);
        animation.Scripts.Add(script.Name, script);

        var root = new RigidModelPart { Name = "Root", Children = [] };
        var part = new RigidModelPart
        {
            Name = "Valve",
            Construct = new PrisConstruct
            {
                ParentName = "Root",
                ChildName = "Valve",
                AxisTranslation = Vector3.UnitX,
                Min = -10,
                Max = 10
            }
        };
        root.Children.Add(part);
        var model = new RigidModel
        {
            Root = root,
            Parts = new ModelPartCollection(),
            AllParts = [root, part],
            Source = RigidModelSource.Compound
        };
        model.Parts.Add(root);
        model.Parts.Add(part);

        var component = new AnimationComponent(model, animation);
        component.StartAnimation(script);
        component.Update(1.5, null!);
        component.StartAnimation(script, reverse: true, time_scale: 2);
        component.Update(0.125, null!);

        Assert.InRange(part.Construct!.LocalTransform.Position.X, 1.374f, 1.376f);
    }

    [Fact]
    public void ResetAllowsTheSameClipToStartAgain()
    {
        var animation = new AnmFile();
        var script = new Script("Sc_restart");
        AddTrack(script, "Valve", 4f, 4f);
        animation.Scripts.Add(script.Name, script);

        var root = new RigidModelPart { Name = "Root", Children = [] };
        var part = new RigidModelPart
        {
            Name = "Valve",
            Construct = new PrisConstruct
            {
                ParentName = "Root",
                ChildName = "Valve",
                AxisTranslation = Vector3.UnitX,
                Min = -10,
                Max = 10
            }
        };
        root.Children.Add(part);
        var model = new RigidModel
        {
            Root = root,
            Parts = new ModelPartCollection(),
            AllParts = [root, part],
            Source = RigidModelSource.Compound
        };
        model.Parts.Add(root);
        model.Parts.Add(part);

        var component = new AnimationComponent(model, animation);
        component.StartAnimation(script);
        component.Update(1.5, null!);
        Assert.InRange(part.Construct!.LocalTransform.Position.X, 1.499f, 1.501f);

        component.ResetAnimations();
        component.StartAnimation(script);
        component.Update(1.5, null!);
        Assert.InRange(part.Construct.LocalTransform.Position.X, 1.499f, 1.501f);
    }

    private static void AddTrack(Script script, string name, float duration, float lastValue)
    {
        var buffer = new AnmBuffer { Buffer = new byte[8] };
        BitConverter.GetBytes(0f).CopyTo(buffer.Buffer, 0);
        BitConverter.GetBytes(lastValue).CopyTo(buffer.Buffer, 4);
        script.JointMaps.Add(new JointMap
        {
            ParentName = "Root",
            ChildName = name,
            Channel = new Channel(1, 2, duration, buffer)
        });
    }
}
