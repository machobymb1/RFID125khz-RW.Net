using System.Numerics;

namespace RFID125k.Core;

/// <summary>
/// Egy 125 kHz-es EM4100 kompatibilis kártya adatai.
/// </summary>
public sealed class CardData
{
    public string HexId { get; }
    public byte[] Raw { get; }

    private CardData(string hexId, byte[] raw)
    {
        HexId = hexId;
        Raw = raw;
    }

    /// <summary>Az ID maximális értéke (5 bájt = 40 bit).</summary>
    private static readonly BigInteger MaxIdValue = (BigInteger.One << 40) - 1;

    public static CardData FromHexId(string hexId)
    {
        string normalized = Em4100Codec.Normalize(hexId);
        if (!Em4100Codec.IsValidId(normalized))
            throw new ArgumentException("A kártya ID-nak pontosan 10 hexadecimális karakternek kell lennie.", nameof(hexId));
        return new CardData(normalized, Em4100Codec.IdToBytes(normalized));
    }

    /// <summary>
    /// Ellenőrzi, hogy a megadott szöveg érvényes decimális kártya ID-e
    /// (csak számjegyek, és az érték belefér 5 bájtba: 0..1099511627775).
    /// </summary>
    public static bool IsValidDecimalId(string? decimalId)
    {
        if (string.IsNullOrWhiteSpace(decimalId)) return false;
        string s = decimalId.Trim();
        if (s.Length == 0 || s.Length > 13) return false;
        if (!s.All(char.IsAsciiDigit)) return false;
        return BigInteger.Parse(s) <= MaxIdValue;
    }

    /// <summary>Kártya ID létrehozása decimális értékből (0..1099511627775).</summary>
    public static CardData FromDecimalId(string decimalId)
    {
        if (!IsValidDecimalId(decimalId))
            throw new ArgumentException("Érvénytelen kártya ID! Egész szám 0 és 1099511627775 (5 bájt) között.", nameof(decimalId));
        BigInteger value = BigInteger.Parse(decimalId.Trim());
        byte[] be = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        byte[] raw = new byte[5];
        Array.Copy(be, 0, raw, raw.Length - be.Length, be.Length);
        return new CardData(Convert.ToHexString(raw).ToUpperInvariant(), raw);
    }

    /// <summary>Az ID decimális értéke (az 5 bájtos hexadecimális szám tízes számrendszerben).</summary>
    public string DecimalId => new BigInteger(Raw, isUnsigned: true, isBigEndian: true).ToString();

    /// <summary>
    /// A demo program "8H10D" formátuma: az alsó 32 bit (raw[1..4]) decimális értéke,
    /// 10 számjeggyel kiegészítve.
    /// </summary>
    public string EightHexTenDecimal =>
        (((uint)(Raw[1] << 24) | (uint)(Raw[2] << 16) | (uint)(Raw[3] << 8) | Raw[4]) & 0xFFFFFFFF)
        .ToString().PadLeft(10, '0');

    /// <summary>A kártya ID felső bájtja (raw[0]) az írásnál; a gyári kártyákkal egyező fix érték.</summary>
    private const byte DefaultUpperByte = 0x45;

    /// <summary>
    /// Ellenőrzi, hogy a megadott szöveg érvényes 8H10D érték-e
    /// (csak számjegyek, legfeljebb 10 jegy, 0..4294967295; kezdő nullák nélkül adandó meg).
    /// </summary>
    public static bool IsValidEightHexTenDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string s = value.Trim();
        if (s.Length == 0 || s.Length > 10) return false;
        if (!s.All(char.IsAsciiDigit)) return false;
        return uint.TryParse(s, out _);
    }

    /// <summary>
    /// Kártya ID létrehozása 8H10D értékből (0..4294967295, kezdő nullák nélkül).
    /// A felső bájt (raw[0]) a gyári kártyákénak megfelelő fix érték (0x45),
    /// az alsó 4 bájt a megadott 32 bites érték.
    /// </summary>
    public static CardData FromEightHexTenDecimal(string value8H10D)
    {
        if (!IsValidEightHexTenDecimal(value8H10D))
            throw new ArgumentException("Érvénytelen 8H10D érték! Csak számjegyek, legfeljebb 10 jegy, 0 és 4294967295 között.", nameof(value8H10D));
        uint v = uint.Parse(value8H10D.Trim());
        byte[] raw =
        [
            DefaultUpperByte,
            (byte)(v >> 24),
            (byte)(v >> 16),
            (byte)(v >> 8),
            (byte)v
        ];
        return new CardData(Convert.ToHexString(raw).ToUpperInvariant(), raw);
    }

    /// <summary>A klasszikus 26 bites Wiegand kód (facility kód + kártyaszám, paritásbitekkel).</summary>
    public Wiegand26 Wiegand26 => Wiegand26.FromEm4100(Raw);

    public override string ToString() => HexId;

    public static CardData FromBytes(byte[] raw)
    {
        if (raw is null || raw.Length != 5)
            throw new ArgumentException("A nyers kártyaadatnak pontosan 5 bájtnak kell lennie.", nameof(raw));
        return new CardData(Convert.ToHexString(raw).ToUpperInvariant(), raw);
    }
}

/// <summary>
/// Wiegand 26 kód a demo program konvenciója szerint:
/// facility kód + kártyaszám, ahol az érték = facility * 100000 + kártyaszám.
/// </summary>
public readonly record struct Wiegand26(uint Value, int FacilityCode, int CardNumber)
{
    /// <summary>
    /// EM4100 5 bájtos ID → Wiegand 26 (a demo program kimenetét követve).
    /// A live teszt alapján (kártya 45 00 71 84 05 → WG26: 11333797):
    /// facility = raw[2] (0x71 = 113), kártyaszám = (raw[3] &lt;&lt; 8) | raw[4] (0x8405 = 33797).
    /// </summary>
    public static Wiegand26 FromEm4100(byte[] raw)
    {
        int facility = raw[2];
        int cardNumber = (raw[3] << 8) | raw[4];
        uint demoValue = (uint)(facility * 100000 + cardNumber);
        return new Wiegand26(demoValue, facility, cardNumber);
    }

    /// <summary>A valódi 26 bites Wiegand bittérkép (páros/páratlan paritásbitekkel).</summary>
    public string ToBinaryString()
    {
        uint data = (uint)((FacilityCode << 16) | CardNumber);
        int evenParity = CountBits((data >> 12) & 0xFFF) % 2; // páros paritás a felső 12 biten
        int oddParity = CountBits(data & 0xFFF) % 2 == 0 ? 1 : 0; // páratlan paritás az alsó 12 biten
        uint w = (uint)((uint)(evenParity << 25) | (data << 1) | (uint)oddParity);
        return Convert.ToString(w, 2).PadLeft(26, '0');
    }

    private static int CountBits(uint v)
    {
        int c = 0;
        while (v != 0) { c += (int)(v & 1); v >>= 1; }
        return c;
    }

    public override string ToString() => $"{Value} (facility: {FacilityCode}, kártyaszám: {CardNumber})";
}
