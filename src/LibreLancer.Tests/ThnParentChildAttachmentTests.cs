using System;
using System.Numerics;
using LibreLancer.Thn;
using Xunit;

namespace LibreLancer.Tests;

public sealed class ThnParentChildAttachmentTests
{
    [Fact]
    public void ParentChildAttachmentPreservesInitialRelativeTransform()
    {
        var initialParent = new Transform3D(
            new Vector3(-3.665222f, 0, 3.132761f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.151f));
        var initialChild = new Transform3D(
            new Vector3(-3.924391f, 1.667192f, 5.18079f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.15f));
        var parent = new MutableTransformParent(initialParent);
        var child = new ThnSceneObject
        {
            Translate = initialChild.Position,
            Rotate = initialChild.Orientation
        };
        var relative = ThnAttachment.CaptureParentChildTransform(initialChild, initialParent);
        child.Attachments.Add(new ThnAttachment(parent)
        {
            Position = true,
            Orientation = true,
            ParentChildTransform = relative
        });

        AssertTransformClose(initialChild, child.GetTransform());

        var movedParent = new Transform3D(
            new Vector3(12, 4, -7),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.2f));
        parent.Transform = movedParent;

        var expectedChild = relative * movedParent;
        AssertTransformClose(expectedChild, child.GetTransform());
        Assert.True(Vector3.Distance(expectedChild.Position, movedParent.Position) > 1.5f);
    }

    private static void AssertTransformClose(Transform3D expected, Transform3D actual)
    {
        Assert.True(Vector3.Distance(expected.Position, actual.Position) < 0.0001f);
        Assert.True(MathF.Abs(Quaternion.Dot(expected.Orientation, actual.Orientation)) > 0.9999f);
    }

    private sealed class MutableTransformParent(Transform3D transform) : ThnAttachParent
    {
        public Transform3D Transform = transform;

        public override Transform3D GetTransform(bool pathLookAt) => Transform;
    }
}
