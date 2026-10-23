using System;
using System.IO;

namespace WinDesk.Signing;

// A signature may only change the checksum, certificate-table entry, alignment
// padding and the appended certificate. All payload bytes must stay identical.
public static class PeSigningContent
{
    public static void AssertUnchanged(string originalPath, string signedPath)
    {
        byte[] original = File.ReadAllBytes(originalPath), signed = File.ReadAllBytes(signedPath);
        if (original.Length < 256 || BitConverter.ToUInt16(original, 0) != 0x5a4d)
            throw new InvalidDataException("Expected an unsigned PE input.");
        int pe = BitConverter.ToInt32(original, 0x3c);
        if (pe < 64 || pe > original.Length - 256 || BitConverter.ToUInt32(original, pe) != 0x4550)
            throw new InvalidDataException("Invalid PE header.");
        int optional = pe + 24;
        ushort magic = BitConverter.ToUInt16(original, optional);
        if (magic != 0x10b && magic != 0x20b) throw new InvalidDataException("Unsupported PE format.");
        int checksum = optional + 64, security = optional + (magic == 0x20b ? 112 : 96) + 32;
        if (BitConverter.ToUInt64(original, security) != 0 || signed.Length <= original.Length)
            throw new InvalidDataException("Expected unsigned input and an appended signature.");
        uint offset = BitConverter.ToUInt32(signed, security), length = BitConverter.ToUInt32(signed, security + 4);
        if (offset != ((long)original.Length + 7) / 8 * 8 || length < 8 ||
            (long)offset + length != signed.Length)
            throw new InvalidDataException("Unexpected PE signature extent.");
        for (int i = 0; i < original.Length; i++)
            if (!(i >= checksum && i < checksum + 4) && !(i >= security && i < security + 8) && original[i] != signed[i])
                throw new InvalidDataException("Signed payload differs from the build input.");
        for (int i = original.Length; i < offset; i++)
            if (signed[i] != 0) throw new InvalidDataException("Non-zero signing padding.");
    }
}
