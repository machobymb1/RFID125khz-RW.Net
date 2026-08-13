namespace RFID125k.Core;

/// <summary>
/// Hardver nélküli szimuláció: bemutató és tesztelés céljára.
/// </summary>
public sealed class SimulatedDevice : IRfidDevice
{
    private readonly Random _rng = new();
    private CancellationTokenSource? _loopCts;

    public string DeviceName => Localization.T("sim.name");
    public bool IsOpen { get; private set; }
    public bool CanWrite => true;

    public event Action<CardData>? CardPresented;

    public void Open()
    {
        IsOpen = true;
        _loopCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!_loopCts.IsCancellationRequested)
            {
                await Task.Delay(3000, _loopCts.Token).ConfigureAwait(false);
                CardPresented?.Invoke(RandomCard());
            }
        }, _loopCts.Token);
    }

    public void Close()
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _loopCts = null;
        IsOpen = false;
    }

    public Task<CardReadResult> ReadCardAsync(CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            await Task.Delay(1000, ct).ConfigureAwait(false);
            CardData card = RandomCard();
            CardPresented?.Invoke(card);
            return new CardReadResult(CardReadStatus.Card, card);
        }, ct);
    }

    public Task WriteCardAsync(CardData card, CancellationToken ct = default)
    {
        CardPresented?.Invoke(card);
        return Task.CompletedTask;
    }

    public Task EraseCardAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task UnlockCardAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<CardInfo> GetCardInfoAsync(CancellationToken ct = default) =>
        Task.FromResult(new CardInfo(true, null, false, Localization.T("sim.cardinfo"), true, null));

    private CardData RandomCard()
    {
        byte[] raw = new byte[5];
        _rng.NextBytes(raw);
        raw[0] &= 0x0F;
        return CardData.FromBytes(raw);
    }

    public void Dispose() => Close();
}
