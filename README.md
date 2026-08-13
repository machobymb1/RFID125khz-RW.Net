# RFID 125 kHz Card Reader/Writer (.NET / WinForms)

A Windows WinForms application for a 125 kHz RFID card reader/writer
(EM4100 / T5577 compatible cards): card reading with ID display, card
status inspection, writing (T4100 / E4100 / EL4100), erasing, unlocking,
lock-after-write, and diagnostics.

The device is controlled through the vendor `IDReader.dll` (in the
`vendor/` folder). Multilingual UI (Hungarian / English), switchable at
runtime.

## Features

- **Card reading** with a single click (cancellable while reading); cards
  detected automatically by the device are shown immediately. IDs are
  displayed as HEX, DEC (13 digits), 8H10D and Wiegand 26 (facility code
  + card number).
- **Card inspection** ("Card info"): chip number and writability, verified
  with a safe test write.
- **Writing**:
  - `T4100` – rewritable T5577 chip, EM4100 format (recommended);
  - `E4100` – EM4100 type card (only on rewritable devices);
  - `EL4100` – EL4100 series chip (locking unavailable in this mode).
- **Lock after write** option (T5577; a locked card can be made writable
  again with "Unlock"), **erase**, **unlock**.
- **Diagnostics**: list of connected USB devices (matched by VID 1A86,
  PID DD01), status of the loaded DLL, output format bytes, and probing of
  open-type codes (0-8).
- **Multilingual UI** (Hungarian / English), switchable at runtime from the
  menu; the selection is persisted to `config.json`.
- **Simulated mode**: if `IDReader.dll` is missing, the program starts with
  a simulated device (for trying the UI without hardware).

## Hardware

- 125 kHz RFID reader/writer for EM4100 compatible cards (reading) and
  T5577 (writing), a CH341-based USB HID device (VID `1A86`, PID `DD01`).
- The vendor `IDReader.dll` is provided in the `vendor/` folder; the build
  copies it next to the executable, and the program loads it at startup
  (the file name can be changed via the `vendorDll` field in
  `rfid125k.json`).

## Requirements and build

- Windows 10/11, .NET 9 SDK (build) or .NET 9 Desktop Runtime (run).
- The project targets `x86` (the vendor DLL is 32-bit).

```sh
dotnet build RFID125k.sln -c Release
dotnet run --project RFID125k.Gui      # or bin\Release\... \RFID125k.Gui.exe
```

Publish (all files go to the `publish/` folder):

```sh
dotnet publish RFID125k.Gui\RFID125k.Gui.csproj -c Release -o publish
```

A pre-built release is included in `publish/`; running it requires the
.NET 9 Desktop Runtime.

## Configuration

The files live next to the executable.

### `rfid125k.json` – device settings

```json
{
  "vendorDll": "IDReader.dll",
  "openType": 0,
  "writeMethod": "T4100"
}
```

- `vendorDll`: name of the vendor DLL to load (default: `IDReader.dll`).
- `openType`: the `OpenReader` type code (per testing, `0` works).
- `writeMethod`: default write method (`T4100` | `E4100` | `EL4100`).

### `config.json` – UI language

```json
{
  "language": "hu"
}
```

The language selected in the "Language" menu is saved here; if the program
folder is not writable, it falls back to
`%APPDATA%\RFID125k\config.json`.

## Languages

Translations are JSON dictionaries (`lang.hu.json`, `lang.en.json`) with
keys shared with the Python port - one dictionary works in both
applications. To add a language: copy `lang.en.json` to `lang.xx.json`
(two-letter code), translate the values, and pick it from the menu.
Unknown keys fall back to the Hungarian text, then to the key name.

## Logging

- The UI "Log" box shows events (reading/writing/diagnostics).
- The device layer also writes `device_trace.log` next to the executable
  (opens, open-type codes, error codes) for troubleshooting.

## Project layout

```
RFID125k.sln
RFID125k.Core/        # device layer: VendorDllDevice, RfidDeviceFactory,
                      # UsbDeviceScanner, CardData/Em4100Codec, Localization
RFID125k.Gui/         # WinForms UI (MainForm, Program)
vendor/               # vendor IDReader.dll (copied to the output by the build)
python/               # cross-platform Python port (see: python/README.md)
rfid125k.ico          # application icon (embedded in the exe)
```

## Python port

The same program is also available as a cross-platform Python
(CustomTkinter) version:
```
python/README.md
```
The Python port does not use the vendor DLL but talks to the HID protocol
directly (so it runs on Windows, Linux and macOS). The language
dictionaries, card formats (HEX / DEC / 8H10D / Wiegand 26) and card
handling rules are shared.