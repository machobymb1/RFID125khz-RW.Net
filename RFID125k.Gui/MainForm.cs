using RFID125k.Core;

namespace RFID125k.Gui;

public sealed class MainForm : Form
{
    private readonly IRfidDevice _device;
    private readonly VendorDllDevice? _vendor;
    private CancellationTokenSource? _readCts;

    private readonly MenuStrip _menuStrip;
    private readonly ToolStripMenuItem _miLanguage;
    private readonly Dictionary<string, ToolStripMenuItem> _miLangs = new();

    private readonly Label _lblDeviceStatus;
    private readonly GroupBox _grpRead;
    private readonly GroupBox _grpWrite;
    private readonly Button _btnRead;
    private readonly Button _btnDiagnostics;
    private readonly Button _btnCardInfo;
    private readonly Label _lblHex;
    private readonly Label _lblDecimal;
    private readonly Label _lblEightHex;
    private readonly Label _lblWiegand;
    private readonly Label _lblCardState;
    private readonly TextBox _txtId;
    private readonly ComboBox _cmbWriteMethod;
    private readonly CheckBox _chkLock;
    private readonly Label _lblMethodDesc;
    private readonly Button _btnWrite;
    private readonly Button _btnErase;
    private readonly Button _btnUnlock;
    private readonly TextBox _txtLog;
    private readonly Label _lblId;
    private readonly Label _lblMethod;
    private readonly Label _lblLog;
    private readonly ToolTip _tip = new();

    private CardData? _lastCard;

    public MainForm()
    {
        Font = new Font("Segoe UI", 10F);
        ClientSize = new Size(580, 662);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        _device = RfidDeviceFactory.CreateDevice();
        _vendor = _device as VendorDllDevice;
        _device.CardPresented += OnCardPresented;

        // --- Nyelv menü (futás közbeni váltás) ---
        _menuStrip = new MenuStrip();
        _miLanguage = new ToolStripMenuItem();
        foreach (string code in Localization.SupportedLanguages)
        {
            var item = new ToolStripMenuItem { Tag = code };
            item.Click += (_, _) => SwitchLanguage(code);
            _miLanguage.DropDownItems.Add(item);
            _miLangs[code] = item;
        }
        _menuStrip.Items.Add(_miLanguage);
        Controls.Add(_menuStrip);
        MainMenuStrip = _menuStrip;

        int x = 16, y = 16 + _menuStrip.Height, w = ClientSize.Width - 32, h = 24;

        _lblDeviceStatus = new Label { Location = new Point(x, y), Size = new Size(w, 30) };
        Controls.Add(_lblDeviceStatus);

        y += h + 10;
        _grpRead = new GroupBox { Location = new Point(x, y), Size = new Size(w, 176) };
        _btnRead = new Button { Location = new Point(12, 28), Size = new Size(150, 32) };
        _btnRead.Click += async (_, _) => await ReadButton_Click();
        _grpRead.Controls.Add(_btnRead);
        _btnDiagnostics = new Button { Location = new Point(172, 28), Size = new Size(150, 32) };
        _btnDiagnostics.Click += (_, _) => DiagnosticsButton_Click();
        _grpRead.Controls.Add(_btnDiagnostics);
        _btnCardInfo = new Button { Location = new Point(332, 28), Size = new Size(150, 32) };
        _btnCardInfo.Click += async (_, _) => await CardInfoButton_Click();
        _grpRead.Controls.Add(_btnCardInfo);

        _lblHex = new Label { Location = new Point(12, 70), Size = new Size(w - 24, 18) };
        _lblDecimal = new Label { Location = new Point(12, 90), Size = new Size(w - 24, 18) };
        _lblEightHex = new Label { Location = new Point(12, 110), Size = new Size(w - 24, 18) };
        _lblWiegand = new Label { Location = new Point(12, 130), Size = new Size(w - 24, 18) };
        _lblCardState = new Label { Location = new Point(12, 150), Size = new Size(w - 24, 18), ForeColor = Color.FromArgb(0, 96, 0) };
        _grpRead.Controls.Add(_lblHex);
        _grpRead.Controls.Add(_lblDecimal);
        _grpRead.Controls.Add(_lblEightHex);
        _grpRead.Controls.Add(_lblWiegand);
        _grpRead.Controls.Add(_lblCardState);
        Controls.Add(_grpRead);

        y += 176 + 8;
        _grpWrite = new GroupBox { Location = new Point(x, y), Size = new Size(w, 180) };
        _lblId = new Label { Location = new Point(12, 32), Size = new Size(100, 22), TextAlign = ContentAlignment.MiddleLeft };
        _txtId = new TextBox
        {
            Location = new Point(116, 30),
            Size = new Size(150, 24),
            MaxLength = 10,
            CharacterCasing = CharacterCasing.Normal,
            Font = new Font("Consolas", 11F)
        };
        _txtId.TextChanged += (_, _) => ValidateIdInput();
        _tip.SetToolTip(_txtId, "");
        _btnWrite = new Button { Location = new Point(276, 28), Size = new Size(150, 30) };
        _btnWrite.Click += async (_, _) => await WriteButton_Click();
        _grpWrite.Controls.Add(_lblId);
        _grpWrite.Controls.Add(_txtId);
        _grpWrite.Controls.Add(_btnWrite);

        _lblMethod = new Label { Location = new Point(12, 66), Size = new Size(120, 24), TextAlign = ContentAlignment.MiddleLeft };
        _cmbWriteMethod = new ComboBox
        {
            Location = new Point(140, 66),
            Size = new Size(190, 24),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbWriteMethod.SelectedIndexChanged += (_, _) => WriteMethod_Changed();
        _chkLock = new CheckBox { Location = new Point(340, 68), Size = new Size(130, 22) };
        _grpWrite.Controls.Add(_lblMethod);
        _grpWrite.Controls.Add(_cmbWriteMethod);
        _grpWrite.Controls.Add(_chkLock);

        _btnErase = new Button { Location = new Point(12, 100), Size = new Size(150, 30) };
        _btnErase.Click += async (_, _) => await EraseButton_Click();
        _grpWrite.Controls.Add(_btnErase);
        _btnUnlock = new Button { Location = new Point(172, 100), Size = new Size(150, 30) };
        _btnUnlock.Click += async (_, _) => await UnlockButton_Click();
        _grpWrite.Controls.Add(_btnUnlock);

        _lblMethodDesc = new Label { Location = new Point(12, 136), Size = new Size(w - 24, 38), ForeColor = Color.Gray };
        _grpWrite.Controls.Add(_lblMethodDesc);
        Controls.Add(_grpWrite);

        y += 180 + 8;
        _lblLog = new Label { Location = new Point(x, y), Size = new Size(w, 18) };
        Controls.Add(_lblLog);
        y += 22;
        _txtLog = new TextBox
        {
            Location = new Point(x, y),
            Size = new Size(w, ClientSize.Height - y - 16),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9.5F)
        };
        Controls.Add(_txtLog);
        _txtLog.Focus();

        ApplyLanguage();
        OpenDevice();
        ValidateIdInput();
    }

    // ---------- nyelv ----------

    private void SwitchLanguage(string code)
    {
        Localization.LoadLanguage(code);
        Localization.SaveConfigLanguage(Localization.CurrentLanguage);
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        Text = Localization.T("app.title");
        _miLanguage.Text = Localization.T("menu.language");
        foreach (string code in Localization.SupportedLanguages)
        {
            var item = _miLangs[code];
            item.Text = Localization.T($"menu.language.{code}");
            item.Checked = code == Localization.CurrentLanguage;
        }

        _lblDeviceStatus.Text = Localization.T("device.connected", RfidDeviceFactory.DescribeDevice());
        _grpRead.Text = Localization.T("grp.read");
        _btnRead.Text = _readCts is null ? Localization.T("btn.read") : Localization.T("btn.read.cancel");
        _btnDiagnostics.Text = Localization.T("btn.diagnostics");
        _btnCardInfo.Text = Localization.T("btn.cardinfo");
        _grpWrite.Text = Localization.T("grp.write");
        _lblId.Text = Localization.T("lbl.id");
        _btnWrite.Text = Localization.T("btn.write");
        _lblMethod.Text = Localization.T("lbl.method");
        _chkLock.Text = Localization.T("chk.lock");
        _btnErase.Text = Localization.T("btn.erase");
        _btnUnlock.Text = Localization.T("btn.unlock");
        _tip.SetToolTip(_txtId, Localization.T("tip.id"));
        _lblLog.Text = Localization.T("lbl.log");

        RebuildMethodCombo();

        if (_lastCard is not null)
            ShowCard(_lastCard);
        else
            ClearCardLabels();
    }

    private void RebuildMethodCombo()
    {
        int index = Math.Max(0, _cmbWriteMethod.SelectedIndex);
        _cmbWriteMethod.Items.Clear();
        _cmbWriteMethod.Items.Add(Localization.T("cmb.method.t4100"));
        _cmbWriteMethod.Items.Add(Localization.T("cmb.method.e4100"));
        _cmbWriteMethod.Items.Add(Localization.T("cmb.method.el4100"));
        _cmbWriteMethod.SelectedIndex = Math.Min(index, _cmbWriteMethod.Items.Count - 1);
    }

    private void WriteMethod_Changed()
    {
        bool el4100 = _cmbWriteMethod.SelectedIndex == 2;
        _chkLock.Enabled = !el4100;
        _lblMethodDesc.Text = _cmbWriteMethod.SelectedIndex switch
        {
            1 => Localization.T("method.desc.e4100"),
            2 => Localization.T("method.desc.el4100"),
            _ => Localization.T("method.desc.t4100")
        };
    }

    private void ApplyWriteMethod()
    {
        if (_vendor is null || _cmbWriteMethod.SelectedIndex < 0) return;
        _vendor.WriteMethod = _cmbWriteMethod.SelectedIndex switch
        {
            1 => WriteMethod.E4100,
            2 => WriteMethod.EL4100,
            _ => WriteMethod.T4100
        };
        _vendor.LockAfterWrite = _chkLock.Checked;
    }

    private void OpenDevice()
    {
        try
        {
            _device.Open();
            Log(Localization.T("log.device.opened", _device.DeviceName));
            if (_vendor is not null)
                Log(_vendor.GetReaderInfo());
        }
        catch (Exception ex)
        {
            Log(Localization.T("log.device.open.error", ex.Message));
        }
    }

    private void DiagnosticsButton_Click()
    {
        _btnRead.Enabled = false;
        try
        {
            Log(Localization.T("log.diag.start"));
            string dllState = VendorNativeApi.IsAvailable
                ? Localization.T("log.diag.dll.yes", VendorNativeApi.LoadedDll)
                : Localization.T("log.diag.dll.no");
            Log(Localization.T("log.diag.dll", dllState));
            if (VendorNativeApi.IsAvailable && VendorNativeApi.GetLibVersion(out int ver) == 0)
                Log(Localization.T("log.diag.dll.version", ver));

            var devices = UsbDeviceScanner.FindByVidPid(VendorNativeApi.ReaderVid, VendorNativeApi.ReaderPid);
            if (devices.Count == 0)
                Log(Localization.T("err.usb.notfound", VendorNativeApi.ReaderVid, VendorNativeApi.ReaderPid));
            else
                foreach (string dev in devices)
                    Log(Localization.T("log.diag.found", dev));

            if (_vendor is null)
            {
                Log(Localization.T("log.diag.dll.unavailable"));
                return;
            }

            Log(Localization.T("log.diag.opentype"));
            foreach ((int type, int rc) in _vendor.ProbeOpenTypes())
            {
                string state = rc switch
                {
                    0 => Localization.T("rc.success"),
                    1 => Localization.T("rc.alreadyopen"),
                    2 => Localization.T("rc.notfound"),
                    _ => Localization.T("rc.other")
                };
                Log(Localization.T("log.diag.opentype.row", type, rc, state));
            }

            if (_vendor.IsOpen)
                Log(Localization.T("log.diag.open", _vendor.GetReaderInfo()));
            else
                Log(Localization.T("log.diag.notopen"));
        }
        catch (Exception ex)
        {
            Log(Localization.T("log.diag.error", ex.Message));
        }
        finally
        {
            _btnRead.Enabled = true;
        }
    }

    private async Task ReadButton_Click()
    {
        if (_readCts is not null)
        {
            _readCts.Cancel();
            return;
        }

        _readCts = new CancellationTokenSource();
        _btnRead.Text = Localization.T("btn.read.cancel");
        Log(Localization.T("log.read.start"));

        try
        {
            CardReadResult result = await _device.ReadCardAsync(_readCts.Token);
            if (result.Status == CardReadStatus.BlankCard)
            {
                ClearCardLabels();
                _lastCard = null;
                SetCardStateLabel(Localization.T("state.blank"));
                Log(Localization.T("log.blank.found"));
                Log(Localization.T("log.blank.hint"));
                return;
            }

            CardData card = result.Card!;
            ShowCard(card);
            _lastCard = card;
            Log(Localization.T("log.read.done", card.HexId, card.DecimalId, card.EightHexTenDecimal, card.Wiegand26.Value));
            _txtId.Text = card.EightHexTenDecimal.TrimStart('0');
            if (_txtId.Text.Length == 0) _txtId.Text = "0";
            SetCardStateLabel(Localization.T("state.nochip"));
        }
        catch (OperationCanceledException)
        {
            Log(Localization.T("log.read.cancelled"));
        }
        catch (Exception ex)
        {
            Log(Localization.T("log.read.error", ex.Message));
        }
        finally
        {
            _readCts.Dispose();
            _readCts = null;
            _btnRead.Text = Localization.T("btn.read");
        }
    }

    private async Task WriteButton_Click()
    {
        string text = _txtId.Text.Trim();
        if (!CardData.IsValidEightHexTenDecimal(text))
        {
            Log(Localization.T("log.write.invalid"));
            return;
        }

        CardData card = CardData.FromEightHexTenDecimal(text);
        ApplyWriteMethod();
        _btnWrite.Enabled = false;
        try
        {
            Log(Localization.T("log.write.start", card.EightHexTenDecimal, card.HexId,
                card.DecimalId.PadLeft(13, '0'), card.Wiegand26.Value));
            Log(Localization.T("log.write.place"));
            await _device.WriteCardAsync(card);
            Log(Localization.T("log.write.done", card.HexId, card.EightHexTenDecimal));
        }
        catch (Exception ex)
        {
            Log(Localization.T("log.write.error", ex.Message));
        }
        finally
        {
            _btnWrite.Enabled = true;
        }
    }

    private async Task CardInfoButton_Click()
    {
        _btnCardInfo.Enabled = false;
        try
        {
            Log(Localization.T("log.cardinfo.start"));
            CardInfo info = await _device.GetCardInfoAsync();

            if (!info.CardPresent)
            {
                Log(Localization.T("log.cardinfo.nocard"));
                SetCardStateLabel(Localization.T("state.nocard"));
                return;
            }

            if (info.Message is not null)
                Log(info.Message);

            string state = info.IsWritable
                ? Localization.T("state.content.writable")
                : Localization.T("state.content.readonly");
            SetCardStateLabel(Localization.T("state.cardinfo", info.ChipDescription, state));
            Log(Localization.T("log.cardinfo.chip", info.ChipDescription));
            Log(Localization.T("log.cardinfo.content", state));

            if (info.Card is not null)
            {
                ShowCard(info.Card);
                _lastCard = info.Card;
            }
        }
        catch (Exception ex)
        {
            Log(Localization.T("log.cardinfo.error", ex.Message));
        }
        finally
        {
            _btnCardInfo.Enabled = true;
        }
    }

    private async Task EraseButton_Click()
    {
        if (MessageBox.Show(this,
                Localization.T("dlg.erase.text"),
                Localization.T("dlg.erase.title"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        _btnErase.Enabled = false;
        try
        {
            Log(Localization.T("log.erase.start"));
            await _device.EraseCardAsync();
            Log(Localization.T("log.erase.done"));
            ClearCardLabels();
            _lastCard = null;
        }
        catch (Exception ex)
        {
            Log(Localization.T("log.erase.error", ex.Message));
        }
        finally
        {
            _btnErase.Enabled = true;
        }
    }

    private async Task UnlockButton_Click()
    {
        if (MessageBox.Show(this,
                Localization.T("dlg.unlock.text"),
                Localization.T("dlg.unlock.title"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
            return;

        _btnUnlock.Enabled = false;
        try
        {
            Log(Localization.T("log.unlock.start"));
            await _device.UnlockCardAsync();
            Log(Localization.T("log.unlock.done"));
            ClearCardLabels();
            _lastCard = null;
        }
        catch (Exception ex)
        {
            Log(Localization.T("log.unlock.error", ex.Message));
        }
        finally
        {
            _btnUnlock.Enabled = true;
        }
    }

    private void SetCardStateLabel(string text) => _lblCardState.Text = text;

    private void ClearCardLabels()
    {
        _lblHex.Text = Localization.T("lbl.hex");
        _lblDecimal.Text = Localization.T("lbl.decimal");
        _lblEightHex.Text = Localization.T("lbl.eighthex");
        _lblWiegand.Text = Localization.T("lbl.wiegand");
    }

    private void ValidateIdInput()
    {
        string text = _txtId.Text.Trim();
        bool valid = CardData.IsValidEightHexTenDecimal(text);
        _btnWrite.Enabled = valid && _device.CanWrite;
    }

    private void OnCardPresented(CardData card)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnCardPresented(card));
            return;
        }
        ShowCard(card);
        _lastCard = card;
    }

    private void ShowCard(CardData card)
    {
        _lblHex.Text = Localization.T("lbl.hex.value", card.HexId);
        _lblDecimal.Text = Localization.T("lbl.decimal.value", card.DecimalId.PadLeft(13, '0'));
        _lblEightHex.Text = Localization.T("lbl.eighthex.value", card.EightHexTenDecimal);
        Wiegand26 w = card.Wiegand26;
        _lblWiegand.Text = Localization.T("lbl.wiegand.value", w.Value, w.FacilityCode, w.CardNumber);
    }

    private void Log(string message)
    {
        _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _readCts?.Cancel();
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _device.CardPresented -= OnCardPresented;
        _device.Dispose();
        base.OnFormClosed(e);
    }
}