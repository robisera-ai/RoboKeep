using System.IO;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class DiskErrorTests
{
    [Theory]
    [InlineData(23)]   // ERROR_CRC — "errore nei dati (controllo di ridondanza ciclico)"
    [InlineData(27)]   // ERROR_SECTOR_NOT_FOUND
    [InlineData(1117)] // ERROR_IO_DEVICE
    public void Win32BadSectorCodes_AreUnreadable(int win32)
    {
        var ex = new IOException("x", unchecked((int)(0x80070000u | (uint)win32)));
        Assert.True(DiskError.IsUnreadable(ex));
    }

    [Fact]
    public void FileNotFound_IsNotUnreadable()
    {
        // ERROR_FILE_NOT_FOUND (2): un file mancante non e' un settore danneggiato.
        var ex = new IOException("x", unchecked((int)0x80070002));
        Assert.False(DiskError.IsUnreadable(ex));
    }

    [Fact]
    public void GenericException_IsNotUnreadable()
        => Assert.False(DiskError.IsUnreadable(new System.InvalidOperationException("boom")));
}
