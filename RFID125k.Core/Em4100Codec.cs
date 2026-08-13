namespace RFID125k.Core;

/// <summary>
/// EM4100 kódolás: 10 hexadecimális karakteres kártya ID (5 bájt),
/// EM4100 adatkeret és T5577 blokkok előállítása.
/// </summary>
public static class Em4100Codec
{
    public static string Normalize(string hexId) => hexId.Trim().ToUpperInvariant();

    public static bool IsValidId(string hexId) =>
        hexId.Length == 10 && hexId.All(c => Uri.IsHexDigit(c));

    public static byte[] IdToBytes(string hexId)
    {
        byte[] raw = new byte[5];
        for (int i = 0; i < 5; i++)
            raw[i] = Convert.ToByte(hexId.Substring(i * 2, 2), 16);
        return raw;
    }

    /// <summary>
    /// A 64 bites EM4100 keret: 9 darab '1' fejléc, 10 × (4 adatbit + 1 oszlopparitás),
    /// 1 stop bit (0), majd 4 sorparitás bit.
    /// </summary>
    public static ulong BuildFrame(byte[] id)
    {
        byte[] nibbles = new byte[10];
        for (int i = 0; i < 5; i++)
        {
            nibbles[2 * i] = (byte)(id[i] >> 4);
            nibbles[2 * i + 1] = (byte)(id[i] & 0x0F);
        }

        ulong frame = 0;
        int bit = 0;

        for (int i = 0; i < 9; i++) { frame |= 1UL << bit; bit++; }

        for (int g = 0; g < 10; g++)
        {
            for (int b = 3; b >= 0; b--)
            {
                if ((nibbles[g] & (1 << b)) != 0) frame |= 1UL << bit;
                bit++;
            }
            int columnParity = nibbles[g] ^ (nibbles[g] >> 1) ^ (nibbles[g] >> 2) ^ (nibbles[g] >> 3);
            if ((columnParity & 1) != 0) frame |= 1UL << bit;
            bit++;
        }

        bit++; // stop bit, 0

        for (int r = 0; r < 4; r++)
        {
            int rowParity = 0;
            for (int g = 0; g < 10; g++) rowParity ^= (nibbles[g] >> r) & 1;
            if (rowParity != 0) frame |= 1UL << bit;
            bit++;
        }

        return frame;
    }

    /// <summary>
    /// T5577 chipre írandó blokkok EM4100 kompatibilis módban.
    /// Blokk 0: ismert EM4100 kompatibilis konfiguráció (Manchester, RF/64).
    /// Blokk 1-2: a 64 bites EM4100 keret. Blokk 3-4: üres.
    /// Megjegyzés: egyes eszközök firmware-e eltérő blokk-elrendezést vár.
    /// </summary>
    public static uint[] BuildT5577Blocks(string hexId)
    {
        ulong frame = BuildFrame(IdToBytes(hexId));
        return new uint[]
        {
            0x00148040,
            (uint)(frame & 0xFFFFFFFFUL),
            (uint)(frame >> 32),
            0x00000000,
            0x00000000
        };
    }
}
