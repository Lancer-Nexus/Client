// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.IO;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using LibreLancer.Client.Components;
using LibreLancer.Data.GameData.World;
using LibreLancer.Net.Protocol;
using LibreLancer.Server.Components;

namespace LibreLancer.World.Components
{
    public enum AutopilotBehaviors
    {
        None,
        Goto,
        Dock,
        Formation,
        Undock
    }

    internal abstract class AutopilotBehavior
    {
        private bool hasTriggeredCruise = false;
        protected readonly AutopilotObstacleAvoidance avoidance = new();

        public abstract AutopilotBehaviors Behavior { get; }

        public virtual bool DockCameraActive => false;

        protected AutopilotBehavior(AutopilotComponent component)
        {
            Component = component;
        }

        protected readonly AutopilotComponent Component;
        protected GameObject Parent => Component.Parent;

        public abstract void ImGuiDebug();

        public abstract bool Update(
            ShipSteeringComponent control,
            ShipInputComponent? input,
            double time,
            GameWorld world);

        private void Start(GotoKind kind, float maxThrottle, float gotoRadius)
        {
            CanCruise = kind != GotoKind.GotoNoCruise;
            MaxThrottle = maxThrottle;
            GotoRadius = gotoRadius;
        }

        public void Start(GotoKind kind,
            GameObject targetObject,
            float maxThrottle,
            float gotoRadius,
            bool shouldStopAtTarget = true)
        {
            TargetObject = targetObject;
            ShouldStopAtTarget = shouldStopAtTarget;
            Start(kind, maxThrottle, gotoRadius);
        }

        public void Start(GotoKind kind,
            Vector3 targetPosition,
            float maxThrottle,
            float gotoRadius,
            bool shouldStopAtTarget = true)
        {
            _targetPosition = targetPosition;
            _targetRadius = 5;
            ShouldStopAtTarget = shouldStopAtTarget;
            Start(kind, maxThrottle, gotoRadius);
        }

        protected bool CanCruise;
        protected bool HasTriggeredCruise => hasTriggeredCruise;

        protected GameObject? TargetObject;
        private Vector3 _targetPosition;
        private float _targetRadius;

        protected float MaxThrottle;
        protected float GotoRadius;
        protected bool ShouldStopAtTarget = true;

        protected AutopilotTransferState CaptureCommonState(
            Func<GameObject, LibreLancer.Server.NpcTransferObjectReferenceV1?>? captureReference = null,
            Vector3 formationHeldSeparation = default,
            int formationHeldSeparationNeighbor = 0,
            float formationSeparationHoldTimer = 0)
        {
            var targetReference = CaptureTargetReference(captureReference);
            return new AutopilotTransferState
            {
                Behavior = Behavior,
                CanCruise = CanCruise,
                HasTriggeredCruise = hasTriggeredCruise,
                MaxThrottle = MaxThrottle,
                GotoRadius = GotoRadius,
                ShouldStopAtTarget = ShouldStopAtTarget,
                TargetPosition = _targetPosition,
                TargetRadius = _targetRadius,
                TargetNpcId = targetReference?.NpcId,
                TargetCharacterId = targetReference?.CharacterId,
                TargetNickname = targetReference?.Nickname,
                FormationHeldSeparation = formationHeldSeparation,
                FormationHeldSeparationNeighbor = formationHeldSeparationNeighbor,
                FormationSeparationHoldTimer = formationSeparationHoldTimer
            };
        }

        protected LibreLancer.Server.NpcTransferObjectReferenceV1? CaptureTargetReference(
            Func<GameObject, LibreLancer.Server.NpcTransferObjectReferenceV1?>? captureReference) =>
            TargetObject is null ? null : captureReference?.Invoke(TargetObject)
                ?? throw new NotSupportedException("Autopilot targets need stable transfer IDs before capture.");

        protected void RestoreCommonState(AutopilotTransferState state,
            Func<LibreLancer.Server.NpcTransferObjectReferenceV1, GameObject?>? resolveReference = null)
        {
            CanCruise = state.CanCruise;
            hasTriggeredCruise = state.HasTriggeredCruise;
            MaxThrottle = state.MaxThrottle;
            GotoRadius = state.GotoRadius;
            ShouldStopAtTarget = state.ShouldStopAtTarget;
            _targetPosition = state.TargetPosition;
            _targetRadius = state.TargetRadius;
            var targetReference = state.GetTargetReference();
            TargetObject = targetReference is null ? null : resolveReference?.Invoke(targetReference)
                ?? throw new InvalidDataException("Autopilot target reference could not be resolved after restore.");
            avoidance.RestoreTransferState(state.Avoidance ??
                throw new InvalidDataException("Autopilot avoidance transfer state is missing."));
        }

        internal AutopilotAvoidanceTransferState CaptureAvoidanceTransferState() => avoidance.CaptureTransferState();

        public virtual AutopilotTransferState CaptureTransferState() =>
            throw new NotSupportedException($"Autopilot behavior {Behavior} cannot be transferred yet.");

        internal virtual AutopilotTransferState CaptureTransferState(
            Func<GameObject, LibreLancer.Server.NpcTransferObjectReferenceV1?> captureReference) =>
            CaptureTransferState();

        protected void SetThrottle(float throttle, ShipSteeringComponent control, ShipInputComponent? input)
        {
            input?.AutopilotThrottle = throttle;
            control.InThrottle = throttle;
        }

        protected void TriggerCruise(ShipSteeringComponent control, bool shouldCruise)
        {
            if (hasTriggeredCruise)
            {
                return;
            }

            hasTriggeredCruise = true;
            control.Cruise = CanCruise && shouldCruise;
        }

        protected bool TargetValid() => TargetObject == null ||
                                        TargetObject.Flags.HasFlag(GameObjectFlags.Exists);

        protected bool Dockable(out DockInfoComponent? dockInfoComponent)
        {
            if (TargetObject != null)
            {
                return TargetObject.TryGetComponent(out dockInfoComponent);
            }

            dockInfoComponent = null;
            return false;
        }

        protected Vector3 GetTargetPoint()
        {
            return TargetObject == null ? _targetPosition : TargetObject.WorldTransform.Position;
        }

        protected float GetTargetRadius()
        {
            if (TargetObject == null)
            {
                return _targetRadius;
            }

            return TargetObject.PhysicsComponent!.Body!.Collider.Radius;
        }

        protected Hardpoint? GetTargetHardpoint(DockInfoComponent docking, bool reverse, int index, int dockIndex = 0)
        {
            var hps = docking.GetDockHardpoints(Parent.PhysicsComponent!.Body!.Position, dockIndex);

            if (reverse)
            {
                hps = hps.Reverse();
            }

            return hps.Skip(index).FirstOrDefault();
        }

        protected bool TurnTowards(double time, Vector3 targetPoint)
        {
            var vec = Parent.InverseTransformPoint(targetPoint);
            return TurnTowardsLocalDirection(time, vec);
        }

        protected bool TurnTowardsDirection(double time, Vector3 worldDirection)
        {
            if (worldDirection.LengthSquared() <= float.Epsilon)
            {
                Component.OutYaw = 0;
                Component.OutPitch = 0;
                return true;
            }

            var localDirection = Vector3.Transform(
                Vector3.Normalize(worldDirection),
                Quaternion.Conjugate(Parent.WorldTransform.Orientation));
            return TurnTowardsLocalDirection(time, localDirection);
        }

        private bool TurnTowardsLocalDirection(double time, Vector3 vec)
        {
            if (vec.LengthSquared() <= float.Epsilon)
            {
                Component.OutYaw = 0;
                Component.OutPitch = 0;
                return true;
            }

            vec.Normalize();

            var directionSatisfied = vec.Z < 0 &&
                                     Math.Abs(vec.X) < 0.0015f &&
                                     Math.Abs(vec.Y) < 0.0015f;

            if (!directionSatisfied)
            {
                var yawError = Math.Abs(vec.X) < 0.0015f && vec.Z > 0 ? 1 : vec.X;
                Component.OutYaw = MathHelper.Clamp((float)Component.YawControl.Update(0, yawError, time), -1, 1);
                Component.OutPitch = MathHelper.Clamp((float)Component.PitchControl.Update(0, -vec.Y, time), -1, 1);
                return false;
            }

            Component.OutYaw = 0;
            Component.OutPitch = 0;
            return true;
        }

        protected bool MoveToPoint(
            double time,
            Vector3 point,
            float radius,
            float range,
            float maxSpeed,
            bool shouldStop,
            ShipSteeringComponent control,
            ShipInputComponent? input,
            GameWorld world,
            bool includeTargetRadius = true,
            Vector3? turnDirection = null)
        {
            float targetPower = 0;
            // Bring ship to within GotoRange metres of target
            var targetRadius = includeTargetRadius ? GetTargetRadius() : 0;
            var myRadius = Parent.PhysicsComponent!.Body!.Collider.Radius;
            var distance = (point - Parent.PhysicsComponent.Body.Position).Length();

            TriggerCruise(control, (distance - range) > 2000);

            if (shouldStop && (distance - range) < 500)
            {
                control.Cruise = false; // Disable cruise at small distance
            }

            var distrad = targetRadius + myRadius + radius + range;
            var distanceSatisfied = distrad >= distance;

            if (distanceSatisfied && shouldStop)
            {
                targetPower = 0;
            }
            else
            {
                targetPower = maxSpeed;
            }

            if (targetPower > maxSpeed)
            {
                targetPower = maxSpeed;
            }

            input?.AutopilotThrottle = targetPower;
            control.InThrottle = targetPower;

            if (distanceSatisfied)
                return true;

            if (Behavior != AutopilotBehaviors.Undock && Component.AvoidanceEnabled)
            {
                var avoidancePlan = avoidance.GetPlan(world, Parent, TargetObject, point, targetRadius + radius + range,
                    time);
                Component.SetAutopilotStrafe(avoidancePlan.Strafe, avoidancePlan.StrafeVector);
            }

            var directionSatisfied = turnDirection is { } direction
                ? TurnTowardsDirection(time, direction)
                : TurnTowards(time, point);


            return distanceSatisfied && directionSatisfied;
        }

    }

    internal sealed class DockBehavior(
        AutopilotComponent c,
        int dockIndex,
        string? selectedTradelaneHardpoint) : AutopilotBehavior(c)
    {
        public override AutopilotBehaviors Behavior => AutopilotBehaviors.Dock;
        public override bool DockCameraActive => ringDocking;

        private int lastTargetHp = 0;
        private bool ringDocking = false;
        private double ringDockTime = 0;
        private string? tradelaneHardpoint = selectedTradelaneHardpoint;
        private bool tradelaneEntryPathActive;
        private float tradelaneEntryPathProgress;
        private Vector3 tradelaneEntryStart;
        private Vector3 tradelaneEntryStartControl;
        private Vector3 tradelaneEntryEndControl;
        private Vector3 tradelaneEntryEnd;
        private Vector3 tradelaneEntryAxis;

        internal void RestoreTransferState(AutopilotTransferState state,
            Func<LibreLancer.Server.NpcTransferObjectReferenceV1, GameObject?>? resolveReference)
        {
            if (state.Behavior != Behavior || state.DockIndex != dockIndex)
                throw new ArgumentException("Snapshot state does not match Dock behavior.", nameof(state));
            RestoreCommonState(state, resolveReference);
            lastTargetHp = state.DockLastTargetHardpoint;
            ringDocking = state.DockRingDocking;
            ringDockTime = state.DockRingTime;
            tradelaneHardpoint = state.DockTradelaneHardpoint;
            tradelaneEntryPathActive = state.DockTradelaneEntryPathActive;
            tradelaneEntryPathProgress = state.DockTradelaneEntryPathProgress;
            tradelaneEntryStart = state.DockTradelaneEntryStart;
            tradelaneEntryStartControl = state.DockTradelaneEntryStartControl;
            tradelaneEntryEndControl = state.DockTradelaneEntryEndControl;
            tradelaneEntryEnd = state.DockTradelaneEntryEnd;
            tradelaneEntryAxis = state.DockTradelaneEntryAxis;
        }

        public override AutopilotTransferState CaptureTransferState() =>
            CaptureDockState((Func<GameObject, LibreLancer.Server.NpcTransferObjectReferenceV1?>?)null);

        internal override AutopilotTransferState CaptureTransferState(
            Func<GameObject, LibreLancer.Server.NpcTransferObjectReferenceV1?> captureReference) =>
            CaptureDockState(captureReference);

        private AutopilotTransferState CaptureDockState(
            Func<GameObject, LibreLancer.Server.NpcTransferObjectReferenceV1?>? captureReference) =>
            CaptureDockState(CaptureTargetReference(captureReference));

        private AutopilotTransferState CaptureDockState(
            LibreLancer.Server.NpcTransferObjectReferenceV1? targetReference) => new()
            {
                Behavior = Behavior,
                CanCruise = CanCruise,
                HasTriggeredCruise = HasTriggeredCruise,
                MaxThrottle = MaxThrottle,
                GotoRadius = GotoRadius,
                ShouldStopAtTarget = ShouldStopAtTarget,
                DockIndex = dockIndex,
                DockLastTargetHardpoint = lastTargetHp,
                DockRingDocking = ringDocking,
                DockRingTime = ringDockTime,
                DockTradelaneHardpoint = tradelaneHardpoint,
                DockTradelaneEntryPathActive = tradelaneEntryPathActive,
                DockTradelaneEntryPathProgress = tradelaneEntryPathProgress,
                DockTradelaneEntryStart = tradelaneEntryStart,
                DockTradelaneEntryStartControl = tradelaneEntryStartControl,
                DockTradelaneEntryEndControl = tradelaneEntryEndControl,
                DockTradelaneEntryEnd = tradelaneEntryEnd,
                DockTradelaneEntryAxis = tradelaneEntryAxis,
                TargetNpcId = targetReference?.NpcId,
                TargetCharacterId = targetReference?.CharacterId,
                TargetNickname = targetReference?.Nickname
            };

        private static bool IsDockingRingIndex(DockInfoComponent docking, int index) =>
            docking.Action.Kind == DockKinds.Base &&
            index == 0 &&
            index < docking.Spheres.Length &&
            docking.Spheres[index].Type == Data.Schema.Solar.DockSphereType.ring;

        private bool TryGetTradelaneDirection(
            DockInfoComponent docking,
            Hardpoint entryHardpoint,
            GameWorld world,
            out Vector3 direction)
        {
            direction = default;
            var nextNickname = entryHardpoint.Name.Equals(
                "HpRightLane",
                StringComparison.OrdinalIgnoreCase)
                ? docking.Action.Target
                : docking.Action.TargetLeft;
            if (nextNickname == null || TargetObject == null)
            {
                return false;
            }

            var nextRing = world.GetObject(nextNickname);
            var nextHardpoint = nextRing?.GetHardpoint(entryHardpoint.Name);
            if (nextRing == null || nextHardpoint == null)
            {
                return false;
            }

            var entryPosition = (entryHardpoint.TransformNoRotate * TargetObject.WorldTransform).Position;
            var nextPosition = (nextHardpoint.TransformNoRotate * nextRing.WorldTransform).Position;
            direction = nextPosition - entryPosition;
            return direction.LengthSquared() > float.Epsilon;
        }

        private void BeginTradelaneEntryPath(Vector3 hardpoint, Vector3 laneDirection, float distance)
        {
            var body = Parent.PhysicsComponent!.Body;
            tradelaneEntryAxis = Vector3.Normalize(laneDirection);
            tradelaneEntryEnd = TradelaneMotion.EntryCapturePoint(
                hardpoint,
                tradelaneEntryAxis,
                body.Collider.Radius);
            tradelaneEntryStart = body.Position;

            var startForward = TradelaneMotion.Forward(body.Orientation);
            var handleLength = MathHelper.Clamp(distance * 0.45f, 50, 225);
            tradelaneEntryStartControl = tradelaneEntryStart + startForward * handleLength;
            tradelaneEntryEndControl = tradelaneEntryEnd - tradelaneEntryAxis * handleLength;
            tradelaneEntryPathProgress = 0;
            tradelaneEntryPathActive = true;
        }

        private Vector3 FollowTradelaneEntryPath(Vector3 shipPosition)
        {
            const int samples = 24;
            var closestProgress = tradelaneEntryPathProgress;
            var closestDistance = float.MaxValue;

            for (var i = 0; i <= samples; i++)
            {
                var sampleProgress = MathHelper.Lerp(
                    tradelaneEntryPathProgress,
                    1,
                    i / (float)samples);
                var sample = TradelaneMotion.EntryPathPoint(
                    tradelaneEntryStart,
                    tradelaneEntryStartControl,
                    tradelaneEntryEndControl,
                    tradelaneEntryEnd,
                    sampleProgress);
                var distance = Vector3.DistanceSquared(shipPosition, sample);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestProgress = sampleProgress;
                }
            }

            tradelaneEntryPathProgress = MathF.Max(tradelaneEntryPathProgress, closestProgress);
            var lookAheadProgress = MathF.Min(1, tradelaneEntryPathProgress + 0.08f);
            var target = TradelaneMotion.EntryPathPoint(
                tradelaneEntryStart,
                tradelaneEntryStartControl,
                tradelaneEntryEndControl,
                tradelaneEntryEnd,
                lookAheadProgress);
            var direction = target - shipPosition;
            if (direction.LengthSquared() <= float.Epsilon)
            {
                direction = TradelaneMotion.EntryPathTangent(
                    tradelaneEntryStart,
                    tradelaneEntryStartControl,
                    tradelaneEntryEndControl,
                    tradelaneEntryEnd,
                    lookAheadProgress);
            }

            return direction.LengthSquared() > float.Epsilon
                ? Vector3.Normalize(direction)
                : tradelaneEntryAxis;
        }

        private void StartRingFlyThrough(ShipSteeringComponent control, ShipInputComponent? input)
        {
            if (Parent.TryGetComponent<ShipPhysicsComponent>(out var physics))
            {
                physics.Active = false;
            }

            Parent.PhysicsComponent!.Collidable = false;
            Parent.PhysicsComponent.Body.Collidable = false;
            Parent.PhysicsComponent.Body.LinearVelocity =
                Vector3.Transform(-Vector3.UnitZ, Parent.PhysicsComponent.Body.Orientation) * 80;
            ringDocking = true;
            ringDockTime = 3.0;
            SetThrottle(1, control, input);
        }

        public override void ImGuiDebug()
        {
            if (Dockable(out var docking))
            {
                ImGui.Text($"Docking with: {docking?.Parent}");
                var hp = GetTargetHardpoint(docking!, false, lastTargetHp, dockIndex);
                ImGui.Text($"Target hardpoint: {hp?.Name}");
            }
        }

        public override bool Update(ShipSteeringComponent control, ShipInputComponent? input, double time, GameWorld world)
        {
            if (!TargetValid() || !Dockable(out var docking))
            {
                return true; // finished
            }

            if (ringDocking)
            {
                SetThrottle(1, control, input);
                ringDockTime -= time;
                return ringDockTime <= 0;
            }

            var dock = docking!;
            var isTradelane = dock.Action.Kind == DockKinds.Tradelane;
            var hp = isTradelane && tradelaneHardpoint != null
                ? TargetObject!.GetHardpoint(tradelaneHardpoint)
                : GetTargetHardpoint(dock, false, lastTargetHp, dockIndex);

            if (hp == null)
            {
                // No dock hardpoints available, cancel docking
                FLLog.Error("Autopilot", "No dock hardpoints available for docking");
                return true; // finished
            }

            if (isTradelane)
            {
                tradelaneHardpoint ??= hp.Name;
            }

            var radius = isTradelane || lastTargetHp == 2 ? dock.GetTriggerRadius(dockIndex) : 5;
            var targetPoint = ((isTradelane
                    ? hp.TransformNoRotate
                    : hp.Transform) * TargetObject!.WorldTransform).Position;
            var isDockingRing = IsDockingRingIndex(dock, dockIndex);

            var d2 = (targetPoint - Parent.PhysicsComponent!.Body!.Position).Length();

            if (isDockingRing &&
                hp.Name.Equals(dock.Spheres[dockIndex].Hardpoint, StringComparison.OrdinalIgnoreCase) &&
                d2 <= Math.Max(250, dock.GetTriggerRadius(dockIndex) + Parent.PhysicsComponent.Body.Collider.Radius))
            {
                StartRingFlyThrough(control, input);
                return false;
            }

            var maxSpeed = isTradelane
                ? 1f
                : lastTargetHp > 0 || d2 < 80
                    ? 0.3f
                    : 1f;
            if (isTradelane && d2 < TradelaneMotion.EntryManeuverDistance)
            {
                control.Cruise = false;
            }
            Vector3? entryDirection = null;
            var movementPoint = targetPoint;
            var movementRadius = radius;
            if (isTradelane &&
                (tradelaneEntryPathActive || d2 <= TradelaneMotion.EntryManeuverDistance) &&
                TryGetTradelaneDirection(dock, hp, world, out var direction))
            {
                var body = Parent.PhysicsComponent.Body;
                if (TradelaneMotion.HasCrossedEntryPlane(
                        body.Position,
                        targetPoint,
                        direction,
                        radius + body.Collider.Radius,
                        body.Collider.Radius))
                {
                    control.Cruise = false;
                    SetThrottle(0, control, input);
                    TurnTowardsDirection(time, direction);
                    return false;
                }

                if (!tradelaneEntryPathActive)
                {
                    BeginTradelaneEntryPath(targetPoint, direction, d2);
                }

                entryDirection = FollowTradelaneEntryPath(
                    Parent.PhysicsComponent.Body.Position);
                movementPoint = tradelaneEntryEnd;
                movementRadius = 0;
            }

            if (!MoveToPoint(
                    time,
                    movementPoint,
                    movementRadius,
                    0,
                    maxSpeed,
                    true,
                    control,
                    input,
                    world,
                    false,
                    entryDirection))
            {
                return false; // not finished
            }

            if (isTradelane)
            {
                if (entryDirection is { } alignmentDirection)
                {
                    TurnTowardsDirection(time, alignmentDirection);
                }

                return false; // wait for the server to start the tradelane
            }

            if (lastTargetHp < 2)
            {
                lastTargetHp++;
            }
            else if (isDockingRing)
            {
                StartRingFlyThrough(control, input);
            }
            else
            {
                SetThrottle(1, control, input);
            }

            return false; // not finished
        }
    }

    internal sealed class UndockBehavior(AutopilotComponent c, int index, double initialDelay) : AutopilotBehavior(c)
    {
        public override AutopilotBehaviors Behavior => AutopilotBehaviors.Undock;

        private const double MAX_TIME_UNDOCK = 30.0;

        private double totalTime = 0.0;
        private double delay = initialDelay;
        private int targetHp = 1;

        internal void RestoreTransferState(AutopilotTransferState state,
            Func<LibreLancer.Server.NpcTransferObjectReferenceV1, GameObject?>? resolveReference)
        {
            if (state.Behavior != Behavior || state.UndockIndex != index)
                throw new ArgumentException("Snapshot state does not match Undock behavior.", nameof(state));
            RestoreCommonState(state, resolveReference);
            totalTime = state.UndockTotalTime;
            delay = state.UndockDelay;
            targetHp = state.UndockTargetHardpoint;
        }

        public override AutopilotTransferState CaptureTransferState() => CaptureUndockState(null);

        internal override AutopilotTransferState CaptureTransferState(
            Func<GameObject, LibreLancer.Server.NpcTransferObjectReferenceV1?> captureReference) =>
            CaptureUndockState(captureReference);

        private AutopilotTransferState CaptureUndockState(
            Func<GameObject, LibreLancer.Server.NpcTransferObjectReferenceV1?>? captureReference)
        {
            var targetReference = CaptureTargetReference(captureReference);
            return new AutopilotTransferState
            {
                Behavior = Behavior,
                CanCruise = CanCruise,
                HasTriggeredCruise = HasTriggeredCruise,
                MaxThrottle = MaxThrottle,
                GotoRadius = GotoRadius,
                ShouldStopAtTarget = ShouldStopAtTarget,
                UndockIndex = index,
                UndockTotalTime = totalTime,
                UndockDelay = delay,
                UndockTargetHardpoint = targetHp,
                TargetNpcId = targetReference?.NpcId,
                TargetCharacterId = targetReference?.CharacterId,
                TargetNickname = targetReference?.Nickname
            };
        }

        public override void ImGuiDebug()
        {
            ImGui.Text("Undocking");
        }

        public override bool Update(ShipSteeringComponent control,
            ShipInputComponent? input,
            double time,
            GameWorld world)
        {
            if (delay > 0)
            {
                SetThrottle(0, control, input);
                Component.OutPitch = 0;
                Component.OutYaw = 0;
                delay -= time;
                return false; // not finished
            }

            totalTime += time;

            if (totalTime > MAX_TIME_UNDOCK)
            {
                FLLog.Warning("Autopilot", $"Undock force quit at {totalTime}");
                return true; // finished
            }

            if (!TargetValid() ||
                !Dockable(out var docking))
            {
                return true; // finished
            }

            var hps = docking!.GetDockHardpoints(Parent.PhysicsComponent!.Body!.Position, index).Reverse().ToArray();
            if (hps.Length < 2)
            {
                var info = docking.GetUndockInfo(index);
                var fallbackPoint = (info.End!.Transform * TargetObject!.WorldTransform).Position;
                return MoveToPoint(time, fallbackPoint, 25, 0, 1f, false, control, input, world, false);
            }

            if (targetHp >= hps.Length)
            {
                return true;
            }

            var targetPoint = (hps[targetHp].Transform * TargetObject!.WorldTransform).Position;
            if (!MoveToPoint(time, targetPoint, 25, 0, 1f, false, control, input, world, false))
            {
                return false;
            }

            targetHp++;
            if (targetHp < hps.Length)
            {
                return false;
            }

            return true;
        }
    }

    internal sealed class GotoBehavior(AutopilotComponent c) : AutopilotBehavior(c)
    {
        public override AutopilotBehaviors Behavior => AutopilotBehaviors.Goto;

        internal void RestoreTransferState(AutopilotTransferState state,
            Func<LibreLancer.Server.NpcTransferObjectReferenceV1, GameObject?>? resolveReference = null)
        {
            if (state.Behavior != Behavior)
                throw new ArgumentException("Snapshot behavior does not match Goto.", nameof(state));
            RestoreCommonState(state, resolveReference);
        }

        private GameObject? cruiseSpeedReference;
        private string? cruiseSpeedReferenceNickname;
        private float cruiseSpeedFullDistance;
        private float cruiseSpeedZeroDistance;
        private float cruiseSpeedUnknown;
        private float referenceDistance = -1;
        private float referenceSpeedFactor = 1;
        internal float ReferenceSpeedFactor => referenceSpeedFactor;

        internal void SetCruiseSpeedReference(string? referenceNickname, float fullDistance, float zeroDistance,
            float unknown)
        {
            cruiseSpeedReferenceNickname = referenceNickname;
            cruiseSpeedFullDistance = fullDistance;
            cruiseSpeedZeroDistance = zeroDistance;
            cruiseSpeedUnknown = unknown;
        }

        public override AutopilotTransferState CaptureTransferState()
        {
            if (cruiseSpeedReference != null || !string.IsNullOrWhiteSpace(cruiseSpeedReferenceNickname))
                throw new NotSupportedException("Cruise reference needs a stable transfer ID before capture.");
            return CaptureCommonState();
        }

        internal override AutopilotTransferState CaptureTransferState(
            Func<GameObject, LibreLancer.Server.NpcTransferObjectReferenceV1?> captureReference)
        {
            if (cruiseSpeedReference != null || !string.IsNullOrWhiteSpace(cruiseSpeedReferenceNickname))
                throw new NotSupportedException("Cruise reference needs a stable transfer ID before capture.");
            return CaptureCommonState(captureReference);
        }

        internal static float ReferenceCruiseSpeedOffset(float distance, float fullDistance, float zeroDistance,
            float cruiseSpeed)
        {
            return -cruiseSpeed * (1 - ReferenceCruiseFactor(distance, fullDistance, zeroDistance));
        }

        internal static float ReferenceCruiseFactor(float distance, float fullDistance, float zeroDistance)
        {
            if (zeroDistance <= fullDistance)
                return 1;
            return 1 - MathHelper.Clamp((distance - fullDistance) / (zeroDistance - fullDistance), 0, 1);
        }

        private GameObject? ResolveCruiseSpeedReference(GameWorld world)
        {
            if (string.IsNullOrWhiteSpace(cruiseSpeedReferenceNickname))
                return null;
            if (!cruiseSpeedReferenceNickname.Equals("Player", StringComparison.OrdinalIgnoreCase))
                return world.GetObject(cruiseSpeedReferenceNickname);

            // "Player" in mission objlists is a special token for a player object, not an object nickname.
            var parentPosition = Parent.PhysicsComponent?.Body?.Position ?? Parent.WorldTransform.Position;
            GameObject? nearestPlayer = null;
            var nearestDistance = float.MaxValue;
            foreach (var candidate in world.Objects)
            {
                if (!candidate.Flags.HasFlag(GameObjectFlags.Player) ||
                    !candidate.Flags.HasFlag(GameObjectFlags.Exists))
                    continue;
                var candidatePosition = candidate.PhysicsComponent?.Body?.Position ??
                                        candidate.WorldTransform.Position;
                var distance = Vector3.DistanceSquared(parentPosition, candidatePosition);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPlayer = candidate;
                }
            }
            return nearestPlayer;
        }

        public override bool Update(ShipSteeringComponent control, ShipInputComponent? input, double time, GameWorld world)
        {
            if (!TargetValid())
            {
                return true;
            }

            var finished = MoveToPoint(time, GetTargetPoint(), GetTargetRadius(), GotoRadius, MaxThrottle,
                ShouldStopAtTarget,
                control, input, world);
            referenceDistance = -1;
            referenceSpeedFactor = 1;
            cruiseSpeedReference = ResolveCruiseSpeedReference(world);
            if (cruiseSpeedReference is { } reference && reference.Flags.HasFlag(GameObjectFlags.Exists))
            {
                var parentPosition = Parent.PhysicsComponent?.Body?.Position ?? Parent.WorldTransform.Position;
                var referencePosition = reference.PhysicsComponent?.Body?.Position ?? reference.WorldTransform.Position;
                var distance = Vector3.Distance(parentPosition, referencePosition);
                var cruiseFactor = ReferenceCruiseFactor(distance, cruiseSpeedFullDistance,
                    cruiseSpeedZeroDistance);
                referenceDistance = distance;
                referenceSpeedFactor = cruiseFactor;
                SetThrottle(control.InThrottle * cruiseFactor, control, input);
                if (control.Cruise)
                {
                    var cruiseSpeed = Parent.GetComponent<SEngineComponent>()?.Engine.CruiseSpeed ?? 300;
                    control.CruiseSpeedOffset = ReferenceCruiseSpeedOffset(distance, cruiseSpeedFullDistance,
                        cruiseSpeedZeroDistance, cruiseSpeed);
                }
            }
            return finished;
        }

        public override void ImGuiDebug()
        {
            ImGui.Text($"Goto: {GetTargetPoint()}");
            if (!string.IsNullOrWhiteSpace(cruiseSpeedReferenceNickname))
            {
                ImGui.Text($"Cruise reference: {cruiseSpeedReference?.Nickname ?? "unresolved"} " +
                           $"({cruiseSpeedFullDistance}-{cruiseSpeedZeroDistance}, {cruiseSpeedUnknown})");
                ImGui.Text(referenceDistance < 0
                    ? "Cruise reference unresolved"
                    : $"Reference distance: {referenceDistance:0.0}, factor: {referenceSpeedFactor:0.000}");
            }
        }
    }

    internal sealed class FormationBehavior(AutopilotComponent c) : AutopilotBehavior(c)
    {
        public override AutopilotBehaviors Behavior => AutopilotBehaviors.Formation;

        internal void RestoreTransferState(AutopilotTransferState state,
            Func<LibreLancer.Server.NpcTransferObjectReferenceV1, GameObject?>? resolveReference)
        {
            if (state.Behavior != Behavior || Parent.Formation is null || Parent.Formation.LeadShip == Parent ||
                !Parent.Formation.Contains(Parent))
                throw new ArgumentException("Snapshot state does not match a formation follower.", nameof(state));
            RestoreCommonState(state, resolveReference);
            heldSeparation = state.FormationHeldSeparation;
            heldSeparationNeighbor = state.FormationHeldSeparationNeighbor;
            separationHoldTimer = state.FormationSeparationHoldTimer;
        }

        public override AutopilotTransferState CaptureTransferState() => CaptureFormationState(null);

        internal override AutopilotTransferState CaptureTransferState(
            Func<GameObject, LibreLancer.Server.NpcTransferObjectReferenceV1?> captureReference) =>
            CaptureFormationState(captureReference);

        private AutopilotTransferState CaptureFormationState(
            Func<GameObject, LibreLancer.Server.NpcTransferObjectReferenceV1?>? captureReference) =>
            CaptureCommonState(captureReference, heldSeparation, heldSeparationNeighbor, separationHoldTimer);

        private const float LeaderCruiseCatchupDistance = 450;
        private const float SteeringLookahead = 1000;

        private Vector3 heldSeparation;
        private int heldSeparationNeighbor;
        private float separationHoldTimer;
        private float debugSlotDistance;
        private float debugDesiredSpeed;
        private bool debugSeparating;
        private float debugSeparationBrake;
        private FormationControl.Neighbor[] separationNeighbors = [];

        private static bool LeadIsCruising(GameObject lead)
        {
            if (lead.TryGetComponent<ShipControlAccessComponent>(out var leadControl) &&
                (leadControl.CruiseEnabled ||
                 leadControl.EngineState is EngineStates.Cruise or EngineStates.CruiseCharging))
            {
                return true;
            }
            return false;
        }

        private static float LeadThrottle(GameObject lead)
        {
            if (lead.TryGetComponent<ShipControlAccessComponent>(out var leadControl))
            {
                return leadControl.EnginePower;
            }
            return 0;
        }

        private static Vector3 ShipPosition(GameObject ship) =>
            ship.PhysicsComponent?.Body?.Position ?? ship.WorldTransform.Position;

        private bool ShouldCruise(GameObject lead)
        {
            var leaderDistance = Vector3.Distance(ShipPosition(lead), Parent.PhysicsComponent!.Body!.Position);
            return LeadIsCruising(lead) || leaderDistance > LeaderCruiseCatchupDistance;
        }

        private static int StableFormationId(GameObject ship, ShipFormation formation)
        {
            if (ship == formation.LeadShip)
            {
                return 0;
            }
            for (int i = 0; i < formation.Followers.Count; i++)
            {
                if (formation.Followers[i] == ship)
                    return i + 1;
            }
            return ship.NetID != 0 ? ship.NetID : -1;
        }

        private FormationControl.Separation GetSeparation(double time, Vector3 selfForward)
        {
            var formation = Parent.Formation!;
            var body = Parent.PhysicsComponent!.Body!;
            if (body.IsDisposed)
                return FormationControl.Separation.None;
            var requiredNeighbors = formation.Followers.Count + 1;
            if (separationNeighbors.Length < requiredNeighbors)
                separationNeighbors = new FormationControl.Neighbor[requiredNeighbors];
            int neighborCount = 0;

            void AddNeighbor(GameObject ship)
            {
                var otherBody = ship.PhysicsComponent?.Body;
                if (ship == Parent || otherBody is not { IsDisposed: false })
                    return;
                separationNeighbors[neighborCount++] = new FormationControl.Neighbor(otherBody.Position,
                    otherBody.LinearVelocity, otherBody.Collider.Radius, StableFormationId(ship, formation));
            }

            AddNeighbor(formation.LeadShip);
            foreach (var follower in formation.Followers)
                AddNeighbor(follower);

            var separation = FormationControl.CalculateSeparation(body.Position, body.LinearVelocity, selfForward,
                body.Collider.Radius, StableFormationId(Parent, formation),
                separationNeighbors.AsSpan(0, neighborCount));
            return FormationControl.ApplySeparationHysteresis(separation, ref heldSeparation,
                ref heldSeparationNeighbor, ref separationHoldTimer, (float)time);
        }

        private StrafeControls SeparationStrafe(FormationControl.Separation separation)
        {
            if (!separation.Active)
                return StrafeControls.None;

            var body = Parent.PhysicsComponent!.Body!;
            var rightAmount = Vector3.Dot(separation.Direction, body.RotateVector(Vector3.UnitX));
            var upAmount = Vector3.Dot(separation.Direction, body.RotateVector(Vector3.UnitY));
            if (MathF.Abs(rightAmount) < 0.1f && MathF.Abs(upAmount) < 0.1f)
            {
                var selfId = StableFormationId(Parent, Parent.Formation!);
                rightAmount = selfId <= separation.NeighborId ? -1 : 1;
            }

            var strafe = StrafeControls.None;
            if (rightAmount < -0.1f)
                strafe |= StrafeControls.Left;
            else if (rightAmount > 0.1f)
                strafe |= StrafeControls.Right;
            if (upAmount < -0.1f)
                strafe |= StrafeControls.Down;
            else if (upAmount > 0.1f)
                strafe |= StrafeControls.Up;
            return strafe;
        }

        private static void DrawFormationPoint(GameWorld world, Vector3 point, Color4 color, float size = 15)
        {
            world.DrawFormationDebug(point);
            world.DrawFormationDebugLine(point - Vector3.UnitX * size, point + Vector3.UnitX * size, color);
            world.DrawFormationDebugLine(point - Vector3.UnitY * size, point + Vector3.UnitY * size, color);
            world.DrawFormationDebugLine(point - Vector3.UnitZ * size, point + Vector3.UnitZ * size, color);
        }

        private static void DrawFormationPoints(GameWorld world, ShipFormation formation, Vector3 currentTarget)
        {
            DrawFormationPoint(world, formation.LeadShip.WorldTransform.Position, Color4.Yellow, 20);
            foreach (var follower in formation.Followers)
            {
                var point = formation.LeadShip.WorldTransform.Transform(formation.GetShipOffset(follower));
                DrawFormationPoint(world, point, Color4.Yellow);
            }
            DrawFormationPoint(world, currentTarget, Color4.Cyan, 22);
        }

        public override void ImGuiDebug()
        {
            ImGui.Text("In Formation");
            ImGui.Text($"Slot distance: {debugSlotDistance:0.0}");
            ImGui.Text($"Desired speed: {debugDesiredSpeed:0.0}");
            ImGui.Text($"Separating: {debugSeparating}");
            ImGui.Text($"Collision brake: {debugSeparationBrake:0.00}");
        }

        public override bool Update(ShipSteeringComponent control, ShipInputComponent? input, double time, GameWorld world)
        {
            var formation = Parent.Formation;
            if (formation == null ||
                formation.LeadShip == Parent)
            {
                return true;
            }

            var body = Parent.PhysicsComponent?.Body;
            var lead = formation.LeadShip;
            var leadBody = lead.PhysicsComponent?.Body;
            if ((lead.Flags & GameObjectFlags.Exists) != GameObjectFlags.Exists ||
                body is not { IsDisposed: false } ||
                leadBody is not { IsDisposed: false })
            {
                if (formation.Contains(Parent))
                    formation.Remove(Parent);
                return true;
            }

            var targetPoint = formation.GetShipPosition(Parent, Component.LocalPlayer);
            var slotError = targetPoint - body.Position;
            var distance = slotError.Length();
            var leadPosition = leadBody.Position;
            var leadVelocity = leadBody.LinearVelocity;
            var leadAngularVelocity = leadBody.AngularVelocity;
            var slotVelocity = FormationControl.SlotVelocity(leadVelocity, leadAngularVelocity,
                targetPoint - leadPosition);
            var desiredVelocity = FormationControl.DesiredVelocity(slotVelocity, slotError);
            var leadThrottle = LeadThrottle(lead);
            var cruiseSpeed = Parent.GetComponent<SEngineComponent>()?.Engine.CruiseSpeed ?? 300;
            var selfForward = body.RotateVector(-Vector3.UnitZ);
            var travelDirection = slotVelocity.LengthSquared() > 400
                ? slotVelocity
                : leadBody.RotateVector(-Vector3.UnitZ);

            control.Cruise = ShouldCruise(lead);
            control.CruiseSpeedOffset =
                !control.Cruise
                    ? 0
                    : FormationControl.CruiseSpeedOffset(slotError, body.LinearVelocity, slotVelocity,
                        travelDirection, cruiseSpeed);
            var throttle = FormationControl.StandardThrottle(leadThrottle, desiredVelocity, body.LinearVelocity,
                selfForward);
            throttle = FormationControl.ArrivalThrottle(throttle, distance);
            var separation = Component.AvoidanceEnabled
                ? GetSeparation(time, selfForward)
                : FormationControl.Separation.None;
            throttle *= 1 - separation.Brake;
            if (separation.Brake > 0.35f)
            {
                control.Cruise = false;
                control.CruiseSpeedOffset = 0;
            }
            SetThrottle(throttle, control, input);
            var avoidancePlan = Component.AvoidanceEnabled
                ? avoidance.GetPlan(world, Parent, TargetObject, targetPoint,
                    MathF.Max(125, body.Collider.Radius), time)
                : AutopilotObstacleAvoidance.AvoidancePlan.None;
            Component.SetAutopilotStrafe(avoidancePlan.Active
                ? avoidancePlan.Strafe
                : SeparationStrafe(separation));

            Vector3? steeringPoint = null;
            if (desiredVelocity.LengthSquared() > 25)
                steeringPoint = body.Position + Vector3.Normalize(desiredVelocity) * SteeringLookahead;
            else if (distance > MathF.Max(10, body.Collider.Radius * 0.1f))
                steeringPoint = targetPoint;

            if (steeringPoint != null)
            {
                TurnTowards(time, steeringPoint.Value);
            }
            else
            {
                Component.OutYaw = 0;
                Component.OutPitch = 0;
            }

            DrawFormationPoints(world, formation, targetPoint);
            world.DrawFormationDebugLine(body.Position, targetPoint, Color4.Cyan);
            if (steeringPoint is { } point)
                world.DrawFormationDebugLine(body.Position, point, Color4.Lime);
            if (separation.Active)
                world.DrawFormationDebugLine(body.Position, body.Position + separation.Direction * 200, Color4.Orange);

            debugSlotDistance = distance;
            debugDesiredSpeed = desiredVelocity.Length();
            debugSeparating = separation.Active;
            debugSeparationBrake = separation.Brake;

            return false;
        }
    }


    public class AutopilotComponent : GameComponent
    {
        private AutopilotBehavior? instance;
        public bool LocalPlayer = false;
        internal bool AvoidanceEnabled { get; set; } = true;
        internal StrafeControls AutopilotStrafe { get; private set; } = StrafeControls.None;

        public AutopilotBehaviors CurrentBehavior
            => instance?.Behavior ?? AutopilotBehaviors.None;

        public bool DockCameraActive => instance?.DockCameraActive ?? false;
        internal float ReferenceSpeedFactor => instance is GotoBehavior gotoBehavior
            ? gotoBehavior.ReferenceSpeedFactor
            : 1;

        public readonly PIDController PitchControl = new();
        public readonly PIDController YawControl = new();

        public float OutPitch;
        public float OutYaw;

        public AutopilotComponent(GameObject parent) : base(parent)
        {
            PitchControl.P = 4;
            YawControl.P = 4;
        }

        /// <summary>Capture only behavior states that do not contain process-local object references.</summary>
        public AutopilotTransferState CaptureTransferState()
        {
            var state = instance?.CaptureTransferState() ?? new AutopilotTransferState { Behavior = AutopilotBehaviors.None };
            return CaptureControllerState(state);
        }

        internal AutopilotTransferState CaptureTransferState(
            Func<GameObject, LibreLancer.Server.NpcTransferObjectReferenceV1?> captureReference)
        {
            var state = instance?.CaptureTransferState(captureReference) ??
                        new AutopilotTransferState { Behavior = AutopilotBehaviors.None };
            return CaptureControllerState(state);
        }

        private AutopilotTransferState CaptureControllerState(AutopilotTransferState state)
        {
            state.PitchController = PitchControl.CaptureTransferState();
            state.YawController = YawControl.CaptureTransferState();
            state.Avoidance = instance?.CaptureAvoidanceTransferState();
            return state;
        }

        public void RestoreTransferState(AutopilotTransferState state) => RestoreTransferState(state, null);

        internal void RestoreTransferState(AutopilotTransferState state,
            Func<LibreLancer.Server.NpcTransferObjectReferenceV1, GameObject?>? resolveReference)
        {
            ArgumentNullException.ThrowIfNull(state);
            state.Validate();
            PitchControl.RestoreTransferState(state.PitchController!);
            YawControl.RestoreTransferState(state.YawController!);
            switch (state.Behavior)
            {
                case AutopilotBehaviors.None:
                    Cancel();
                    break;
                case AutopilotBehaviors.Goto:
                {
                    var gotoBehavior = new GotoBehavior(this);
                    gotoBehavior.RestoreTransferState(state, resolveReference);
                    SetInstance(gotoBehavior);
                    break;
                }
                case AutopilotBehaviors.Dock:
                {
                    var dockBehavior = new DockBehavior(this, state.DockIndex, state.DockTradelaneHardpoint);
                    dockBehavior.RestoreTransferState(state, resolveReference);
                    SetInstance(dockBehavior);
                    break;
                }
                case AutopilotBehaviors.Undock:
                {
                    var undockBehavior = new UndockBehavior(this, state.UndockIndex, state.UndockDelay);
                    undockBehavior.RestoreTransferState(state, resolveReference);
                    SetInstance(undockBehavior);
                    break;
                }
                case AutopilotBehaviors.Formation:
                {
                    var formationBehavior = new FormationBehavior(this);
                    formationBehavior.RestoreTransferState(state, resolveReference);
                    SetInstance(formationBehavior);
                    break;
                }
                default:
                    throw new NotSupportedException($"Autopilot behavior {state.Behavior} cannot be restored yet.");
            }
        }

        public delegate void BehaviorChangedCallback(AutopilotBehaviors newBehavior, AutopilotBehaviors oldBehavior);

        public BehaviorChangedCallback? BehaviorChanged;

        private void SetInstance(AutopilotBehavior? i)
        {
            var old = CurrentBehavior;
            instance = i;

            if (CurrentBehavior != old)
            {
                BehaviorChanged?.Invoke(CurrentBehavior, old);
            }
        }

        public void ImGuiDebug()
        {
            ImGui.Text($"Autopilot: {CurrentBehavior}");
            instance?.ImGuiDebug();
        }

        public void GotoVec(Vector3 vec, GotoKind kind, float maxThrottle = 1, float gotoRange = 40, bool shouldStopAtTarget = true)
        {
            SetInstance(new GotoBehavior(this));
            instance?.Start(kind, vec, maxThrottle, gotoRange, shouldStopAtTarget);
        }

        internal void GotoVec(Vector3 vec, GotoKind kind, float maxThrottle, float gotoRange,
            string? cruiseSpeedReferenceNickname, float cruiseSpeedFullDistance, float cruiseSpeedZeroDistance,
            float cruiseSpeedUnknown,
            bool shouldStopAtTarget = true)
        {
            var behavior = new GotoBehavior(this);
            behavior.Start(kind, vec, maxThrottle, gotoRange, shouldStopAtTarget);
            behavior.SetCruiseSpeedReference(cruiseSpeedReferenceNickname, cruiseSpeedFullDistance,
                cruiseSpeedZeroDistance, cruiseSpeedUnknown);
            SetInstance(behavior);
        }

        public void GotoObject(GameObject obj, GotoKind kind, float maxThrottle = 1, float gotoRange = 40, bool shouldStopAtTarget = true)
        {
            SetInstance(new GotoBehavior(this));
            instance?.Start(kind, obj, maxThrottle, gotoRange, shouldStopAtTarget);
        }

        public void Cancel()
        {
            SetInstance(null);
            SetAutopilotStrafe(StrafeControls.None, Vector2.Zero);
        }

        public void StartDock(
            GameObject target,
            GotoKind kind,
            int dockIndex = 0,
            string? tradelaneHardpoint = null)
        {
            SetInstance(new DockBehavior(this, dockIndex, tradelaneHardpoint));
            instance?.Start(kind, target, 1, 40);
        }

        public void Undock(GameObject target, int index)
        {
            SetInstance(new UndockBehavior(this, index, GetUndockDelay(target, index)));
            instance?.Start(GotoKind.GotoNoCruise,
                target, 1, 10);
        }

        private static double GetUndockDelay(GameObject target, int index)
        {
            if (!target.TryGetComponent<DockInfoComponent>(out var docking) ||
                index < 0 ||
                index >= docking.Spheres.Length ||
                docking.Spheres[index].Type != Data.Schema.Solar.DockSphereType.berth)
            {
                return 1.2;
            }

            var duration = target.AnimationComponent?.GetAnimationDuration(docking.Spheres[index].Script) ?? 0;
            return Math.Max(1.2, duration);
        }

        public void StartFormation()
        {
            if (Parent.TryGetComponent<ShipPhysicsComponent>(out var physics))
            {
                physics.StopCruise();
            }

            if (Parent.TryGetComponent<ShipSteeringComponent>(out var steering))
            {
                steering.Cruise = false;
                steering.CruiseSpeedOffset = 0;
                steering.InThrottle = 1;
            }

            if (Parent.TryGetComponent<ShipInputComponent>(out var input))
            {
                input.AutopilotThrottle = 1;
                input.InFormation = true;
            }

            SetInstance(new FormationBehavior(this));
            instance?.Start(GotoKind.Goto, Vector3.Zero, 1, 10);
        }

        public override void Update(double time, GameWorld world)
        {
            var control = Parent?.GetComponent<ShipSteeringComponent>();
            var input = Parent?.GetComponent<ShipInputComponent>();
            SetAutopilotStrafe(StrafeControls.None, Vector2.Zero);

            if (input != null)
            {
                input.AutopilotThrottle = 0;
                input.InFormation = CurrentBehavior == AutopilotBehaviors.Formation;
            }

            if (control == null)
            {
                return;
            }

            control.CruiseSpeedOffset = 0;

            if (instance == null)
            {
                return;
            }

            if (instance.Update(control, input, time, world))
            {
                SetInstance(null);
                SetAutopilotStrafe(StrafeControls.None, Vector2.Zero);
            }
        }

        internal void SetAutopilotStrafe(StrafeControls strafe)
        {
            SetAutopilotStrafe(strafe, Vector2.Zero);
        }

        internal void SetAutopilotStrafe(StrafeControls strafe, Vector2 _)
        {
            AutopilotStrafe = strafe;
        }
    }
}
