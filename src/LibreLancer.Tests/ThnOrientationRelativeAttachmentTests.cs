using System;
using System.Numerics;
using LibreLancer.Thn;
using Xunit;

namespace LibreLancer.Tests;

public sealed class ThnOrientationRelativeAttachmentTests
{
    [Fact]
    public void OrientationRelativeAttachmentAppliesParentRotationInTheCorrectOrder()
    {
        var initialParent = new Transform3D(Vector3.Zero, Quaternion.CreateFromYawPitchRoll(0.35f, -0.2f, 0.1f));
        var initialChild = new Transform3D(Vector3.Zero, Quaternion.CreateFromYawPitchRoll(-0.4f, 0.5f, 0.6f));
        var parent = new MutableTransformParent(initialParent);
        var child = new ThnSceneObject { Rotate = initialChild.Orientation };
        var relative = ThnAttachment.CaptureParentChildTransform(initialChild, initialParent);
        child.Attachments.Add(new ThnAttachment(parent)
        {
            Orientation = true,
            OrientationRelative = true,
            LastRotate = initialParent.Orientation
        });

        AssertOrientationClose(initialChild.Orientation, child.GetTransform().Orientation);

        var nextParent = new Transform3D(Vector3.Zero, Quaternion.CreateFromYawPitchRoll(1.1f, 0.8f, -0.7f));
        parent.Transform = nextParent;

        var expected = relative * nextParent;
        AssertOrientationClose(expected.Orientation, child.GetTransform().Orientation);
    }

    private static void AssertOrientationClose(Quaternion expected, Quaternion actual) =>
        Assert.True(MathF.Abs(Quaternion.Dot(expected, actual)) > 0.9999f,
            $"Expected {expected}, got {actual}");

    private sealed class MutableTransformParent(Transform3D transform) : ThnAttachParent
    {
        public Transform3D Transform = transform;
        public override Transform3D GetTransform(bool pathLookAt) => Transform;
    }
}
