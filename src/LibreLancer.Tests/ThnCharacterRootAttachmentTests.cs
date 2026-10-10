using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using LibreLancer.Render;
using LibreLancer.Thn;
using LibreLancer.World;
using Xunit;

namespace LibreLancer.Tests;

public sealed class ThnCharacterRootAttachmentTests
{
    [Fact]
    public void CharacterRootAttachmentUsesWorldTransformAfterRootMotion()
    {
        var character = new GameObject
        {
            RenderComponent = (CharacterRenderer)RuntimeHelpers.GetUninitializedObject(typeof(CharacterRenderer))
        };
        var rootMotionTransform = new Transform3D(
            new Vector3(-7, 2, 11),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.75f));
        character.SetLocalTransform(rootMotionTransform);

        var sceneObject = new ThnSceneObject
        {
            Translate = Vector3.Zero,
            Rotate = Quaternion.Identity,
            Object = character
        };
        Assert.True(ThnObjectParent.IsRootTarget(sceneObject, "Root"));
        Assert.False(ThnObjectParent.IsRootTarget(sceneObject, "UpperTorso"));
        var parent = new ThnObjectParent(sceneObject, null, null, characterRoot: true);

        AssertTransformClose(rootMotionTransform, parent.GetTransform(pathLookAt: false));
    }

    [Fact]
    public void RootAttachmentSupportsActorAliasWithoutRenderableObject()
    {
        var sceneObject = new ThnSceneObject
        {
            Translate = new Vector3(3, -2, 7),
            Rotate = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.3f)
        };

        Assert.True(ThnObjectParent.IsRootTarget(sceneObject, "Root"));
        var parent = new ThnObjectParent(sceneObject, null, null, characterRoot: true);
        AssertTransformClose(new Transform3D(sceneObject.Translate, sceneObject.Rotate),
            parent.GetTransform(pathLookAt: false));
    }

    [Fact]
    public void CharacterHardpointAttachmentUsesWorldTransformAfterRootMotion()
    {
        var character = new GameObject
        {
            RenderComponent = (CharacterRenderer)RuntimeHelpers.GetUninitializedObject(typeof(CharacterRenderer))
        };
        var rootMotionTransform = new Transform3D(
            new Vector3(5, 3, -8),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.4f));
        character.SetLocalTransform(rootMotionTransform);

        var sceneObject = new ThnSceneObject
        {
            Translate = Vector3.Zero,
            Rotate = Quaternion.Identity,
            Object = character
        };
        var hardpointTransform = new Transform3D(new Vector3(0.1f, 1.6f, 0.05f), Quaternion.Identity);
        var parent = new ThnObjectParent(sceneObject, new TestHardpoint(hardpointTransform), null);

        AssertTransformClose(hardpointTransform * rootMotionTransform, parent.GetTransform(pathLookAt: false));
    }

    [Fact]
    public void CharacterBoneAttachmentUsesAnimatedBoneTransformAndWorldTransform()
    {
        var boneTransform = new Transform3D(
            new Vector3(0.2f, 1.1f, -0.35f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.2f));
        var bone = new BoneInstance("Body_Head", boneTransform, Vector3.Zero, Vector3.One);
        var skinning = (DfmSkinning)RuntimeHelpers.GetUninitializedObject(typeof(DfmSkinning));
        skinning.Bones = new Dictionary<string, BoneInstance>(StringComparer.OrdinalIgnoreCase)
        {
            [bone.Name] = bone
        };
        var skeleton = (DfmSkeletonManager)RuntimeHelpers.GetUninitializedObject(typeof(DfmSkeletonManager));
        skeleton.BodySkinning = skinning;
        var characterRenderer = (CharacterRenderer)RuntimeHelpers.GetUninitializedObject(typeof(CharacterRenderer));
        characterRenderer.Skeleton = skeleton;
        var worldTransform = new Transform3D(
            new Vector3(4, 2, -6),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.6f));
        var character = new GameObject { RenderComponent = characterRenderer };
        character.SetLocalTransform(worldTransform);
        var sceneObject = new ThnSceneObject { Object = character };
        var parent = new ThnObjectParent(sceneObject, null, null, characterBoneName: "body_head");

        AssertTransformClose(boneTransform * worldTransform, parent.GetTransform(pathLookAt: false));

        var animatedBoneTransform = new Transform3D(
            new Vector3(-0.1f, 1.3f, 0.4f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -0.15f));
        bone.LocalTransform = animatedBoneTransform;

        AssertTransformClose(animatedBoneTransform * worldTransform, parent.GetTransform(pathLookAt: false));
    }

    private sealed class TestHardpoint(Transform3D transform) : IRenderHardpoint
    {
        public Transform3D Transform => transform;
    }

    private static void AssertTransformClose(Transform3D expected, Transform3D actual)
    {
        Assert.True(Vector3.Distance(expected.Position, actual.Position) < 0.0001f);
        Assert.True(MathF.Abs(Quaternion.Dot(expected.Orientation, actual.Orientation)) > 0.9999f);
    }
}
