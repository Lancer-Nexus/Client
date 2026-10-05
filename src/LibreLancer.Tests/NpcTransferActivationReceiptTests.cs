using System;
using System.IO;
using LibreLancer.Server;
using Xunit;

namespace LibreLancer.Tests;

public sealed class NpcTransferActivationReceiptTests
{
    [Fact]
    public void ActivationReceiptSurvivesNewReaderAndIsIdempotent()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"npc-transfer-receipt-{Guid.NewGuid():N}");
        var transferId = Guid.NewGuid();
        try
        {
            Assert.False(NpcTransferActivationReceipt.IsRecorded(directory, transferId));
            NpcTransferActivationReceipt.Record(directory, transferId);
            NpcTransferActivationReceipt.Record(directory, transferId);

            Assert.True(NpcTransferActivationReceipt.IsRecorded(directory, transferId));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CorruptActivationReceiptFailsClosed()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"npc-transfer-receipt-{Guid.NewGuid():N}");
        var transferId = Guid.NewGuid();
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, $"{transferId:N}.activated"), "wrong-transfer");

            Assert.Throws<InvalidDataException>(() => NpcTransferActivationReceipt.IsRecorded(directory, transferId));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
