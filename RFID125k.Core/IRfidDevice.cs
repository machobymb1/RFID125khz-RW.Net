namespace RFID125k.Core;

/// <summary>Egy olvasási kísérlet eredménye.</summary>
public enum CardReadStatus
{
    /// <summary>Nincs (érvényes) kártya az olvasón.</summary>
    None,

    /// <summary>Érvényes kártya-ID olvasva.</summary>
    Card,

    /// <summary>Üres (tartalom nélküli) kártya az olvasón.</summary>
    BlankCard
}

/// <summary>Az olvasás eredménye. BlankCard esetén a Card null.</summary>
public readonly record struct CardReadResult(CardReadStatus Status, CardData? Card);

/// <summary>
/// A kártya állapotvizsgálatának eredménye: milyen chip van az olvasón,
/// milyen módban, és módosítható/törölhető-e a tartalma.
/// </summary>
public sealed record CardInfo(
    bool CardPresent,
    CardData? Card,
    bool IsBlank,
    string ChipDescription,
    bool IsWritable,
    string? Message);

/// <summary>
/// Platformfüggetlen eszköz-absztrakció 125 kHz-es RFID olvasó/írókhoz.
/// </summary>
public interface IRfidDevice : IDisposable
{
    string DeviceName { get; }
    bool IsOpen { get; }
    bool CanWrite { get; }

    void Open();
    void Close();

    /// <summary>Kártya megjelenése (folyamatos olvasás módban).</summary>
    event Action<CardData>? CardPresented;

    /// <summary>
    /// Egy kártya beolvasása (blokkol, amíg kártyát nem érzékel, vagy meg nem szakítják).
    /// Üres kártya esetén azonnal BlankCard eredménnyel tér vissza (nem kérdez le újra és újra).
    /// </summary>
    Task<CardReadResult> ReadCardAsync(CancellationToken ct = default);

    /// <summary>Kártya ID írása üres/újraírható (T5577) kártyára.</summary>
    Task WriteCardAsync(CardData card, CancellationToken ct = default);

    /// <summary>
    /// A kártya tartalmának törlése (üres kártyává alakítás). Csak újraírható (T5577) kártyákon értelmezett.
    /// </summary>
    Task EraseCardAsync(CancellationToken ct = default);

    /// <summary>
    /// Kártya feloldása (unlock). Csak akkor lehetséges, ha az eszköz támogatja.
    /// Sikeres feloldás esetén a kártya üres, újraírható állapotba kerül.
    /// </summary>
    Task UnlockCardAsync(CancellationToken ct = default);

    /// <summary>
    /// A kártya állapotának vizsgálata: chip típusa (T5577/EM4100), üzemmódja,
    /// és hogy a tartalma törölhető/módosítható-e vagy csak olvasható.
    /// A vizsgálat biztonságos próbaírást használhat (az eredeti tartalmat visszaállítja).
    /// </summary>
    Task<CardInfo> GetCardInfoAsync(CancellationToken ct = default);
}
