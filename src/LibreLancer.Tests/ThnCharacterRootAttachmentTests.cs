using System;
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
