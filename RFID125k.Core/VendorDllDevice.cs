namespace RFID125k.Core;

/// <summary>
/// A gyártói IDReader.dll-lel vezérelt RFID olvasó/író (CH341-alapú USB HID eszköz).
/// </summary>
public sealed class VendorDllDevice : IRfidDevice
{
    private int _handle = -1;
    private static readonly object TraceLock = new();
    private static void Trace(string message)
    {
        try
        {
            lock (TraceLock)
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory ?? ".", "device_trace.log"),
                    $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { /* naplóhiba nem kritikus */ }
    }

    public string DeviceName => VendorNativeApi.IsAvailable
        ? Localization.T("dev.dll.loaded", VendorNativeApi.LoadedDll)
        : Localization.T("dev.dll.notloaded");

    public bool IsOpen => _handle != -1;

    public bool CanWrite => true;

    /// <summary>Az olvasó nyitásához használt típuskód (a készülék tesztelés szerint a 0 működik).</summary>
    public int OpenType { get; set; } = 0;

    /// <summary>Írási mód: T4100 (ajánlott), E4100 vagy EL4100.</summary>
    public WriteMethod WriteMethod { get; set; } = WriteMethod.T4100;

    /// <summary>Írás után kártyalezárás (csak ha az eszköz támogatja).</summary>
    public bool LockAfterWrite { get; set; }

    public event Action<CardData>? CardPresented;

    /// <summary>Minden sikeres (rc==0) nyers olvasásnál meghívódik a pufferrel (diagnosztika).</summary>
    public event Action<byte[]>? RawBufferReceived;

    private readonly byte[] _readBuffer = new byte[4096];

    /// <summary>
    /// Diagnosztika: minden nyitási típuskódot (0..8) kipróbál, és megadja a DLL
    /// visszatérési kódját. Az eszköz állapotát visszaállítja (zárva volt-e → ismét kinyitja).
    /// </summary>
    public List<(int Type, int Rc)> ProbeOpenTypes()
    {
        var results = new List<(int, int)>();
        if (!VendorNativeApi.IsAvailable) return results;

        bool wasOpen = IsOpen;
        if (wasOpen) Close();

        for (int t = 0; t <= 8; t++)
        {
            int h = -1;
            int rc = VendorNativeApi.OpenReader(ref h, t);
            Trace($"ProbeOpenTypes type={t}: rc={rc}");
            results.Add((t, rc));
            if (h != -1)
                VendorNativeApi.CloseReader(ref h);
        }

        if (wasOpen)
        {
            try { Open(); }
            catch { /* a felhasználónak a nyitási kódokból kell tájékozódnia */ }
        }
        return results;
    }

    public void Open()
    {
        if (!VendorNativeApi.IsAvailable)
            throw new InvalidOperationException(Localization.T("err.dll.missing"));

        int rc = VendorNativeApi.OpenReader(ref _handle, OpenType);
        Trace($"Open(type={OpenType}): rc={rc}, handle={_handle}");
        // 0 = megnyitva, 1 = már megnyitva, 2 = eszköz nem található
        if (rc == 2 || (_handle == -1 && rc != 0))
        {
            _handle = -1;
            throw new InvalidOperationException(
                Localization.T("err.reader.notfound", rc, VendorNativeApi.ReaderVid, VendorNativeApi.ReaderPid));
        }

        // NEM kapcsolunk be folyamatos beep-et: a kártyadetektáláskor a készülék
        // firmware-e magától sípol. (A korábbi SetBeep(1) itt folyamatos
        // sípolást okozhatott a program bezárása után.)
    }

    public void Close()
    {
        if (_handle == -1) return;
        Trace($"Close() kezdet, handle={_handle}");
        try
        {
            Trace($"SetAutoRead(0): rc={VendorNativeApi.SetAutoRead(_handle, 0)}");
            // Várunk, hogy az utolsó HID-csomag biztosan kiürüljön az endpointról,
            // mielőtt a DLL lezárja az eszközt.
            Thread.Sleep(300);
        }
        finally
        {
            Trace($"CloseReader: rc={VendorNativeApi.CloseReader(ref _handle)}");
            _handle = -1;
            Trace("Close() vége");
        }
    }

    public async Task<CardReadResult> ReadCardAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        Trace($"ReadCardAsync: SetAutoRead(1) rc={VendorNativeApi.SetAutoRead(_handle, 1)}");
        var lastState = CardReadStatus.None;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_handle == -1)
                    throw new InvalidOperationException(Localization.T("err.device.closed"));
                int rc = VendorNativeApi.ReadIdCard(_handle, _readBuffer);
                if (rc == 0)
                {
                    bool blank = IsBlankBuffer(_readBuffer);
                    CardData? card = TryParseCard(_readBuffer);
                    if (card is not null)
                    {
                        if (lastState != CardReadStatus.Card)
                        {
                            Trace("Kártya az olvasón.");
                            lastState = CardReadStatus.Card;
                        }
                        RawBufferReceived?.Invoke(_readBuffer.ToArray());
                        CardPresented?.Invoke(card);
                        return new CardReadResult(CardReadStatus.Card, card);
                    }
                    if (blank)
                    {
                        // Üres kártya: észleljük és befejezzük az olvasást,
                        // ne kérdezzük le vég nélkül újra és újra.
                        if (lastState != CardReadStatus.BlankCard)
                        {
                            Trace("Üres kártya az olvasón (nincs programozott tartalom) - olvasás befejezve.");
                            lastState = CardReadStatus.BlankCard;
                        }
                        RawBufferReceived?.Invoke(_readBuffer.ToArray());
                        return new CardReadResult(CardReadStatus.BlankCard, null);
                    }
                }
                else
                {
                    if (lastState != CardReadStatus.None)
                    {
                        Trace($"ReadIdCard: rc=0x{rc:X2} (nincs kártya)");
                        lastState = CardReadStatus.None;
                    }
                    await Task.Delay(200, ct).ConfigureAwait(false);
                }
            }
            throw new OperationCanceledException(ct);
        }
        finally
        {
            if (_handle != -1)
                Trace($"ReadCardAsync: SetAutoRead(0) rc={VendorNativeApi.SetAutoRead(_handle, 0)}");
        }
    }

    /// <summary>
    /// A kártya állapotának vizsgálata: chip típus (T5577 vagy EM4100), üzemmód,
    /// és hogy a tartalom törölhető/módosítható-e.
    /// Mivel a DLL csak az ID-t adja vissza, a chip típusa biztonságos próbaírással
    /// deríthető ki: egy eltérő próbaérték írása után visszaolvasunk, majd az eredeti
    /// tartalmat visszaállítjuk. Csak olvasásra való kártyán a próbaírás hatástalan.
    /// </summary>
    public async Task<CardInfo> GetCardInfoAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        var res = await TryReadAnyCardAsync(ct, TimeSpan.FromSeconds(4)).ConfigureAwait(false);

        if (res.Status == CardReadStatus.None)
            return new CardInfo(false, null, false, "", false, Localization.T("cardinfo.nocard"));

        if (res.Status == CardReadStatus.BlankCard)
        {
            // Üres kártya: a próbaírás nem ront el semmit, mert nincs tartalom.
            var probe = CardData.FromBytes(ProbeIdBytes);
            bool writable = await TryProbeWriteAsync(probe, ct).ConfigureAwait(false);

            // Próba után visszaállítás üresre.
            int rc = VendorNativeApi.WriteT4100(_handle, new byte[5], 0);
            bool restored = rc == 0;
            string? msg = restored
                ? null
                : Localization.T("cardinfo.probe.restore.warn", probe.HexId, $"0x{rc:X2}");
            return new CardInfo(true, null, true,
                writable
                    ? Localization.T("cardinfo.t5577.blank")
                    : Localization.T("cardinfo.unknown.blank"),
                writable, msg);
        }

        CardData current = res.Card!;
        byte[] probeBytes = ProbeIdBytes;
        if (probeBytes.SequenceEqual(current.Raw))
            probeBytes = AltProbeIdBytes;

        bool isWritable = await TryProbeWriteAsync(CardData.FromBytes(probeBytes), ct).ConfigureAwait(false);

        string? message = null;
        if (isWritable)
        {
            // Visszaállítás az eredeti tartalomra (1 újrapróbálkozás).
            bool restored = await TryRestoreAsync(current, ct).ConfigureAwait(false);
            if (!restored)
                message = Localization.T("cardinfo.original.restore.warn", current.HexId,
                    BitConverter.ToString(probeBytes).Replace("-", ""));
        }

        return new CardInfo(true, current, false,
            isWritable
                ? Localization.T("cardinfo.t5577")
                : Localization.T("cardinfo.em4100.readonly"),
            isWritable, message);
    }

    /// <summary>Próbaírás: a megadott érték írása, majd visszaolvasás ellenőrzéssel.</summary>
    private async Task<bool> TryProbeWriteAsync(CardData probe, CancellationToken ct)
    {
        int rc = VendorNativeApi.WriteT4100(_handle, probe.Raw, 0);
        Trace($"GetCardInfoAsync próbaírás ({probe.HexId}): rc=0x{rc:X2}");
        if (rc != 0) return false;
        await Task.Delay(300, ct).ConfigureAwait(false);
        var res = await TryReadAnyCardAsync(ct, TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        return res.Status == CardReadStatus.Card && res.Card!.HexId == probe.HexId;
    }

    /// <summary>Az eredeti tartalom visszaállítása (1 újrapróbálkozás).</summary>
    private async Task<bool> TryRestoreAsync(CardData original, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            int rc = VendorNativeApi.WriteT4100(_handle, original.Raw, 0);
            Trace($"GetCardInfoAsync visszaállítás ({original.HexId}) {attempt}. kísérlet: rc=0x{rc:X2}");
            if (rc != 0) continue;
            await Task.Delay(300, ct).ConfigureAwait(false);
            var res = await TryReadAnyCardAsync(ct, TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            if (res.Status == CardReadStatus.Card && res.Card!.HexId == original.HexId)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Rövid ideig próbál kártyát olvasni; Card / BlankCard / None eredményt ad.
    /// </summary>
    private async Task<CardReadResult> TryReadAnyCardAsync(CancellationToken ct, TimeSpan timeout)
    {
        if (_handle == -1)
            throw new InvalidOperationException(Localization.T("err.device.closed"));
        VendorNativeApi.SetAutoRead(_handle, 1);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            while (!cts.IsCancellationRequested)
            {
                if (_handle == -1) return new CardReadResult(CardReadStatus.None, null);
                int rc = VendorNativeApi.ReadIdCard(_handle, _readBuffer);
                if (rc == 0)
                {
                    if (IsBlankBuffer(_readBuffer))
                        return new CardReadResult(CardReadStatus.BlankCard, null);
                    CardData? card = TryParseCard(_readBuffer);
                    if (card is not null)
                        return new CardReadResult(CardReadStatus.Card, card);
                }
                await Task.Delay(50, cts.Token).ConfigureAwait(false);
            }
            return new CardReadResult(CardReadStatus.None, null);
        }
        catch (OperationCanceledException)
        {
            return new CardReadResult(CardReadStatus.None, null);
        }
        finally
        {
            if (_handle != -1)
                Trace($"TryReadAnyCardAsync: SetAutoRead(0) rc={VendorNativeApi.SetAutoRead(_handle, 0)}");
        }
    }

    /// <summary>
    /// Rövid ideig (3 s) próbál érvényes kártyát olvasni. Ha nem olvasható kártyaadat
    /// (üres/törölt kártya), null-t ad vissza.
    /// </summary>
    private async Task<CardData?> TryReadCardBrieflyAsync(CancellationToken ct)
    {
        var res = await TryReadAnyCardAsync(ct, TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        return res.Status == CardReadStatus.Card ? res.Card : null;
    }

    private static bool IsBlankBuffer(byte[] buf) =>
        buf[0] == 0 && buf[1] == 0 && buf[2] == 0 && buf[3] == 0 && buf[4] == 0;

    /// <summary>Próbaérték az írhatóság megállapításához (1234567890).</summary>
    private static readonly byte[] ProbeIdBytes = [0x12, 0x34, 0x56, 0x78, 0x90];

    /// <summary>Tartalék próbaérték, ha a kártyán épp az első próbaérték lenne (987654321).</summary>
    private static readonly byte[] AltProbeIdBytes = [0x00, 0x3A, 0xDE, 0x68, 0xB1];

    public Task WriteCardAsync(CardData card, CancellationToken ct = default)
    {
        EnsureOpen();
        byte mode = LockAfterWrite ? (byte)1 : (byte)0;
        int rc = WriteMethod switch
        {
            WriteMethod.E4100 => VendorNativeApi.WriteE4100(_handle, card.Raw, mode),
            WriteMethod.EL4100 => VendorNativeApi.WriteEL4100(_handle, card.Raw),
            _ => VendorNativeApi.WriteT4100(_handle, card.Raw, mode)
        };
        Trace($"WriteCardAsync ({WriteMethod}, mode={mode}): rc=0x{rc:X2}");
        if (rc != 0)
            throw new InvalidOperationException(Localization.T("err.write.code", rc));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Kártya törlése: üres (00 00 00 00 00) ID írása T5577 módban.
    /// Ezután a kártya üres, újraírható; a tartalma nem olvasható vissza kártyaadatként.
    /// A visszaolvasás-ellenőrzés kiszűri a csak olvasásra való (EM4100) kártyákat:
    /// azokon a törlés "sikeres" (rc=0), de a tartalom nem változik.
    /// </summary>
    public async Task EraseCardAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        int rc = VendorNativeApi.WriteT4100(_handle, new byte[5], 0);
        Trace($"EraseCardAsync: rc=0x{rc:X2}");
        if (rc != 0)
            throw new InvalidOperationException(Localization.T("err.erase.code", rc));

        CardData? leftover = await TryReadCardBrieflyAsync(ct).ConfigureAwait(false);
        if (leftover is not null)
            throw new InvalidOperationException(Localization.T("err.erase.ineffective", leftover.HexId));
    }

    /// <summary>
    /// Kártya feloldása (unlock).
    /// A DLL nem rendelkezik külön jelszókezelő hívással; a lezárást (T4100 mode=1) a gyártói
    /// szoftver alapértelmezett jelszóval végzi, ezért a feloldás a normál írási paranccsal
    /// történik (mode=0). Az így zárolt kártyák újraírhatók. Ismeretlen jelszóval zárolt
    /// kártyát az eszköz nem tud feloldani — ilyenkor az írás hiba kóddal tér vissza.
    /// </summary>
    public async Task UnlockCardAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        int rc = VendorNativeApi.WriteT4100(_handle, new byte[5], 0);
        Trace($"UnlockCardAsync: rc=0x{rc:X2}");
        if (rc != 0)
            throw new InvalidOperationException(Localization.T("err.unlock.code", rc));

        CardData? leftover = await TryReadCardBrieflyAsync(ct).ConfigureAwait(false);
        if (leftover is not null)
            throw new InvalidOperationException(Localization.T("err.unlock.ineffective", leftover.HexId));
    }

    /// <summary>
    /// Biztonságos eszköz-információ natív hívás nélkül.
    /// (A DEV_GetModel/GetNumber/GetFrequency hívások stack-overflow/AV-t okoztak,
    /// mert a rekonstruált aláírások nem voltak pontosak — ezért azok nem hívhatók.)
    /// </summary>
    public string GetReaderInfo()
    {
        string dll = VendorNativeApi.IsAvailable
            ? (VendorNativeApi.LoadedDll ?? VendorNativeApi.DefaultDllName)
            : Localization.T("dev.dll.notloaded");
        var devices = UsbDeviceScanner.FindByVidPid(VendorNativeApi.ReaderVid, VendorNativeApi.ReaderPid);
        string dev = devices.Count == 0
            ? Localization.T("usb.notidentified")
            : string.Join("; ", devices);
        return Localization.T("reader.info.dll", dll, VendorNativeApi.ReaderVid, VendorNativeApi.ReaderPid, dev);
    }

    private static CardData? TryParseCard(byte[] buf)
    {
        if (buf[0] == 0 && buf[1] == 0 && buf[2] == 0 && buf[3] == 0 && buf[4] == 0)
            return null;
        // Üres/érvénytelen chipre jellemző minta (CC vagy FF sorozat): ne értelmezzük kártyaadatként.
        if ((buf[1] == 0xCC || buf[1] == 0xFF) && buf.Skip(1).Take(4).All(b => b == buf[1]))
            return null;
        try
        {
            return CardData.FromBytes(buf.AsSpan(0, 5).ToArray());
        }
        catch
        {
            return null;
        }
    }

    private void EnsureOpen()
    {
        if (_handle == -1)
            throw new InvalidOperationException(Localization.T("err.notopen"));
    }

    public void Dispose() => Close();
}

public enum WriteMethod
{
    /// <summary>T5577 chip írása (újraírható kártyák, EM4100 kompatibilis).</summary>
    T4100,

    /// <summary>EM4100 üzemmódú írás.</summary>
    E4100,

    /// <summary>EL4100 üzemmódú írás.</summary>
    EL4100
}