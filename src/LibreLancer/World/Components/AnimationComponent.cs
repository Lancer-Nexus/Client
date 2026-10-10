// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LibreLancer.Net.Protocol;
using LibreLancer.Render;
using LibreLancer.Utf;
using LibreLancer.Utf.Anm;

namespace LibreLancer.World.Components
{
    public class AnimationComponent : GameComponent
    {
        private Dictionary<Script, ActiveAnimation> active = new();

        private class ActiveAnimation
        {
            public string Name;
            public Script Script;
            public float StartT;
            public float ScriptDuration;
            public double WorldStartTime;
            public double Duration;
            public float TimeScale;
            public bool Reverse;
            public bool Loop;
            public HashSet<string> SuppressedJoints = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<AbstractConstruct, Transform3D> BlendFrom = [];
            public double BlendStartTime;
            public float BlendInDuration;
            public int[] ObjectCursors;
            public int[] JointCursors;
            public float[] JointLoopDurations;

            public ActiveAnimation(Script script, string name)
            {
                Name = name;
                ObjectCursors = new int[script.ObjectMaps.Count];
                JointCursors = new int[script.JointMaps.Count];
                JointLoopDurations = new float[script.JointMaps.Count];
                for (var i = 0; i < script.JointMaps.Count; i++)
                {
                    var channel = script.JointMaps[i].Channel;
                    JointLoopDurations[i] = channel.Duration;
                    for (var j = 0; j < script.JointMaps.Count; j++)
                    {
                        if (i == j || !SameLastKeyframe(channel, script.JointMaps[j].Channel))
                            continue;
                        JointLoopDurations[i] = Math.Max(JointLoopDurations[i], script.JointMaps[j].Channel.Duration);
                    }
                }
                Script = script;
                ScriptDuration = GetScriptDuration(script);
            }

            private static bool SameLastKeyframe(Channel first, Channel second)
            {
                if (first.FrameCount == 0 || second.FrameCount == 0 ||
                    first.HasAngle != second.HasAngle ||
                    first.HasPosition != second.HasPosition ||
                    first.HasOrientation != second.HasOrientation ||
                    (!first.HasAngle && !first.HasPosition && !first.HasOrientation))
                    return false;

                var firstIndex = first.FrameCount - 1;
                var secondIndex = second.FrameCount - 1;
                if (first.HasAngle && first.GetAngle(firstIndex) != second.GetAngle(secondIndex))
                    return false;
                if (first.HasPosition && first.GetPosition(firstIndex) != second.GetPosition(secondIndex))
                    return false;
                if (first.HasOrientation &&
                    MathF.Abs(Quaternion.Dot(first.GetQuaternion(firstIndex), second.GetQuaternion(secondIndex))) <
                    0.99999f)
                    return false;
                return true;
            }

            private float GetScriptDuration(Script sc)
            {
                float duration = 0;

                foreach (var jm in sc.JointMaps)
                {
                    duration = Math.Max(duration, jm.Channel.Duration);
                }

                return duration;
            }
        }

        private AnmFile anm;
        private RigidModel rm = null!;
        private List<ActiveAnimation> animations = [];
        private List<ActiveAnimation> completeAnimations = [];
        public event Action<string>? AnimationCompleted;

        private Func<double> getTotalTime;

        private double accumTime;
        private const float DefaultClipBlendIn = 0.25f;

        public IEnumerable<NetCmpAnimation> Serialize() =>
            animations.Select(x => new NetCmpAnimation()
            {
                Name = x.Name,
                WorldStartTime = (float) x.WorldStartTime,
                Reverse = x.Reverse,
                Loop = x.Loop,
                Finished = false,
            }).Concat(completeAnimations.Select(x => new NetCmpAnimation()
            {
                Name = x.Name,
                Reverse = x.Reverse,
                Finished = true,
            }));

        public void UpdateAnimations(NetCmpAnimation[] data)
        {
            animations = [];
            completeAnimations = [];

            foreach (var a in data.Where(x => anm.Scripts.ContainsKey(x.Name)))
            {
                var sc = anm.Scripts[a.Name];

                if (a.Finished)
                {
                    foreach (var jm in sc.JointMaps)
                    {
                        var mdl = Parent == null ? rm : Parent.Model!.RigidModel;
                        var joint = mdl.Parts[jm.ChildName].Construct;
                        ChannelFloat angles = 0;
                        float t = a.Reverse ? 0 : jm.Channel.Duration;
                        int jointCursor = 0;

                        if (jm.Channel.HasAngle)
                        {
                            angles = jm.Channel.FloatAtTime((float) t, ref jointCursor);
                        }

                        var quat = Quaternion.Identity;

                        if (jm.Channel.HasOrientation)
                        {
                            quat = jm.Channel.QuaternionAtTime((float) t, ref jointCursor);
                        }

                        joint?.Update(angles, quat);
                    }

                    completeAnimations.Add(new ActiveAnimation(sc, a.Name)
                    {
                        Reverse = a.Reverse,
                    });
                }
                else
                {
                    FLLog.Debug("Client", $"Add animation starting {a.WorldStartTime}: current {getTotalTime()}");
                    animations.Add(new ActiveAnimation(sc, a.Name)
                    {
                        WorldStartTime = a.WorldStartTime,
                        Loop = a.Loop,
                        Reverse = a.Reverse,
                    });
                }
            }
        }

        public AnimationComponent(GameObject parent, AnmFile animation) : base(parent)
        {
            anm = animation;
            getTotalTime = () => accumTime;
        }

        public AnimationComponent(RigidModel rm, AnmFile animation) : base(null!)
        {
            this.rm = rm;
            anm = animation;
            getTotalTime = () => accumTime;
        }


        public void StartAnimation(Script script, bool loop = true, float start_time = 0, float time_scale = 1,
            float duration = 0, bool reverse = false)
        {
            if (Parent?.RenderComponent is CharacterRenderer characterRenderer)
            {
                characterRenderer.Skeleton.StartScript(script, start_time, time_scale, duration, loop);
                return;
            }

            if (active.TryGetValue(script, out var animation))
            {
                if (reverse != animation.Reverse)
                {
                    var currTime = getTotalTime();
                    var t = animation.StartT + (currTime - animation.WorldStartTime) * animation.TimeScale;
                    t = MathHelper.Clamp(t, 0, animation.ScriptDuration);
                    BeginClipTransition(animation, script, start_time, duration);
                    animation.StartT = (float) (animation.ScriptDuration - t);
                    animation.Reverse = reverse;
                    animation.TimeScale = time_scale;
                    animation.WorldStartTime = currTime;
                    animation.Duration = duration;
                    animation.Loop = loop;
                }
            }
            else
            {
                var next = new ActiveAnimation(script, script.Name)
                {
                    WorldStartTime = getTotalTime(),
                    Loop = loop,
                    Duration = duration,
                    StartT = start_time,
                    TimeScale = time_scale,
                    Reverse = reverse,
                };
                BeginClipTransition(next, script, start_time, duration);
                animations.Add(next);
                active[script] = next;
            }
        }

        public void StartAnimation(string animationName, bool loop = true, float start_time = 0, float time_scale = 1,
            float duration = 0, bool reverse = false)
        {
            if (anm.Scripts.TryGetValue(animationName, out var script))
            {
                StartAnimation(script, loop, start_time, time_scale, duration, reverse);
            }
            else
            {
                FLLog.Error("Animation", animationName + " not present");
            }
        }

        private RigidModel? GetAnimationModel() => Parent == null ? rm : Parent.Model?.RigidModel;

        private void BeginClipTransition(ActiveAnimation animation, Script script, float startTime, float duration)
        {
            animation.BlendFrom.Clear();
            animation.SuppressedJoints.Clear();
            var model = GetAnimationModel();
            if (model == null)
                return;

            var jointNames = script.JointMaps.Select(jm => jm.ChildName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var previousPoseAvailable = completeAnimations.Any(previous =>
                previous.Script.JointMaps.Any(jm => jointNames.Contains(jm.ChildName))) ||
                animations.Any(previous => previous.Script.JointMaps.Any(jm =>
                    jointNames.Contains(jm.ChildName) && !previous.SuppressedJoints.Contains(jm.ChildName)));
            if (!previousPoseAvailable)
                return;

            foreach (var jointName in jointNames)
            {
                var part = model.Parts[jointName];
                var construct = part.Construct;
                if (construct == null)
                    continue;

                animation.BlendFrom[construct] = construct.LocalTransform;
                foreach (var previous in animations)
                {
                    if (!ReferenceEquals(previous, animation) && previous.Script.JointMaps.Any(jm =>
                            jm.ChildName.Equals(jointName, StringComparison.OrdinalIgnoreCase)))
                        previous.SuppressedJoints.Add(jointName);
                }
            }

            if (animation.BlendFrom.Count == 0)
                return;

            var remainingDuration = duration > 0 ? duration : script.JointMaps
                .Select(jm => jm.Channel.Duration - startTime)
                .Where(d => d > 0)
                .DefaultIfEmpty(DefaultClipBlendIn)
                .Min();
            animation.BlendInDuration = MathF.Min(DefaultClipBlendIn, remainingDuration);
            animation.BlendStartTime = getTotalTime();
        }

        public void ResetAnimations()
        {
            animations.Clear();
            completeAnimations.Clear();
            active.Clear();
            foreach (var part in GetAnimationModel()?.AllParts ?? [])
                if (part.Construct != null)
                    part.Construct.OverrideTransform = null;

            if ((GameObject?)Parent != null)
            {
                foreach (var p in Parent.Model!.RigidModel.AllParts)
                {
                    p.Construct?.Reset();
                }

                Parent.Model.RigidModel.UpdateTransform();
            }
            else if ((RigidModel?)rm != null)
            {
                foreach (var p in rm.AllParts)
                {
                    p.Construct?.Reset();
                }

                rm.UpdateTransform();
            }
        }

        public void FinishAnimation(string animationName)
        {
            if (Parent?.RenderComponent is CharacterRenderer characterRenderer)
            {
                characterRenderer.Skeleton.FinishScript(animationName);
                return;
            }

            for (int i = animations.Count - 1; i >= 0; i--)
            {
                var animation = animations[i];
                if (animation.Loop ||
                    !animation.Name.Equals(animationName, StringComparison.OrdinalIgnoreCase))
                    continue;

                ApplyAnimationFinalPose(animation.Script, animation.Reverse);
                active.Remove(animation.Script);
                completeAnimations.Add(animation);
                animations.RemoveAt(i);
            }

            Parent?.Model?.RigidModel.UpdateTransform();
            Parent?.UpdateCollision();
            rm?.UpdateTransform();
        }

        private void ApplyAnimationFinalPose(Script script, bool reverse)
        {
            var model = Parent == null ? rm : Parent.Model!.RigidModel;
            foreach (var jointMap in script.JointMaps)
            {
                var joint = model.Parts[jointMap.ChildName].Construct;
                var time = reverse ? 0 : jointMap.Channel.Duration;
                var cursor = 0;
                var angle = jointMap.Channel.HasAngle
                    ? jointMap.Channel.FloatAtTime(time, ref cursor)
                    : (ChannelFloat)0;
                var orientation = jointMap.Channel.HasOrientation
                    ? jointMap.Channel.QuaternionAtTime(time, ref cursor)
                    : Quaternion.Identity;
                joint?.Update(angle, orientation);
            }
        }

        public void ResetTimeSource()
        {
            getTotalTime = () => accumTime;
        }

        public void SetTimeSource(Func<double> time)
        {
            getTotalTime = time;
        }


        public bool HasAnimation(string? animationName)
        {
            if (animationName == null)
            {
                return false;
            }

            return anm.Scripts.ContainsKey(animationName);
        }

        public float GetAnimationDuration(string? animationName)
        {
            if (animationName == null ||
                !anm.Scripts.TryGetValue(animationName, out var script))
            {
                return 0;
            }

            var duration = 0f;
            foreach (var jm in script.JointMaps)
            {
                duration = Math.Max(duration, jm.Channel.Duration);
            }

            return duration;
        }

        public override void Update(double time, GameWorld world)
        {
            if ((GameObject?)Parent != null && Parent.RenderComponent is CharacterRenderer characterRenderer)
            {
                characterRenderer.Skeleton.UpdateScripts(time);
                return;
            }

            accumTime += time;
            int c = animations.Count;

            for (int i = 0; i < animations.Count;)
            {
                var animation = animations[i];
                if (ProcessAnimation(animation))
                {
                    foreach (var joint in animation.BlendFrom.Keys)
                        joint.OverrideTransform = null;
                    animation.BlendFrom.Clear();
                    AnimationCompleted?.Invoke(animation.Name);
                    active.Remove(animation.Script);
                    completeAnimations.Add(animation);
                    animations.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }

            if (c > 0 && Parent != null)
            {
                Parent.Model?.RigidModel.UpdateTransform();
                Parent.UpdateCollision();
            }

            if (c > 0)
            {
                rm?.UpdateTransform();
            }
        }

        private bool ProcessAnimation(ActiveAnimation a)
        {
            bool finished = true;
            float ts = a.TimeScale <= 0 ? 1 : a.TimeScale;

            for (int i = 0; i < a.Script.ObjectMaps.Count; i++)
            {
                if (!ProcessObjectMap(ref a.Script.ObjectMaps[i], ref a.ObjectCursors[i], a.WorldStartTime, ts, a.Loop))
                {
                    finished = false;
                }
            }

            for (int i = 0; i < a.Script.JointMaps.Count; i++)
            {
                ref var jointMap = ref a.Script.JointMaps[i];
                if (!ProcessJointMap(ref jointMap, ref a.JointCursors[i], a.StartT, a.WorldStartTime, ts, a.Loop,
                        a.JointLoopDurations[i],
                        a.Reverse, a.SuppressedJoints.Contains(jointMap.ChildName), a))
                {
                    finished = false;
                }
            }

            return finished || (a.Duration > 0 && (getTotalTime() - a.WorldStartTime) >= a.Duration);
        }

        private bool ProcessObjectMap(ref ObjectMap om, ref int cur, double startTime, float timeScale, bool loop)
        {
            return false;
        }

        private bool ProcessJointMap(ref JointMap jm, ref int cur, float initialTime, double startTime,
            float timeScale, bool loop, float loopDuration, bool reverse, bool suppressed,
            ActiveAnimation animation)
        {
            var mdl = Parent == null ? rm : Parent.Model!.RigidModel;
            var joint = mdl.Parts[jm.ChildName].Construct;
            double t = initialTime + (getTotalTime() - startTime) * timeScale;
            bool done = false;

            // looping?
            if (!loop && t >= jm.Channel.Duration)
            {
                done = true;
            }
            else
            {
                t = t % (loopDuration > 0 ? loopDuration : jm.Channel.Duration);
                if (loopDuration > jm.Channel.Duration)
                    t = Math.Min(t, jm.Channel.Duration);
            }

            if (reverse)
            {
                t = jm.Channel.Duration - t;
            }

            ChannelFloat angle = 0;

            if (jm.Channel.HasAngle)
            {
                angle = jm.Channel.FloatAtTime((float) t, ref cur);
            }

            var quat = Quaternion.Identity;

            if (jm.Channel.HasOrientation)
            {
                quat = jm.Channel.QuaternionAtTime((float) t, ref cur);
            }

            if (joint == null || suppressed)
                return done;

            joint.OverrideTransform = null;
            joint.Update(angle, quat);
            if (animation.BlendFrom.TryGetValue(joint, out var startPose))
            {
                var blend = animation.BlendInDuration <= 0
                    ? 1f
                    : MathHelper.Clamp((float)((getTotalTime() - animation.BlendStartTime) /
                                               animation.BlendInDuration), 0f, 1f);
                if (blend >= 1f)
                {
                    animation.BlendFrom.Remove(joint);
                    joint.OverrideTransform = null;
                }
                else
                {
                    var target = joint.LocalTransform;
                    joint.OverrideTransform = new Transform3D(
                        Vector3.Lerp(startPose.Position, target.Position, blend),
                        Quaternion.Slerp(startPose.Orientation, target.Orientation, blend));
                }
            }
            return done;
        }

        public float GetPlayPosition(Script sc)
        {
            if (!active.TryGetValue(sc, out var a))
            {
                return 0;
            }

            float ts = a.TimeScale <= 0 ? 1 : a.TimeScale;
            double t = a.StartT + (getTotalTime() - a.WorldStartTime) * ts;
            float duration = 0;

            for (int i = 0; i < sc.JointMaps.Count; i++)
            {
                if (sc.JointMaps[i].Channel.Duration > duration)
                {
                    duration = sc.JointMaps[i].Channel.Duration;
                }
            }

            var v = duration > 0 ? (float) MathHelper.Clamp(t / duration, 0, 1) : 0;
            return a.Reverse ? (1 - v) : v;
        }
    }
}
