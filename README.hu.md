# RFID 125 kHz kártyaolvasó/író (.NET / WinForms)

Windowsos WinForms alkalmazás 125 kHz-es RFID olvasó/író kártyaolvasóhoz
(EM4100 / T5577 kompatibilis kártyák): kártyaolvasás azonosító
megjelenítéssel, kártya-állapot vizsgálat, írás (T4100 / E4100 / EL4100),
törlés, feloldás, lezárás-írás után, diagnosztika.

Az eszközt a gyári `IDReader.dll`-en keresztül vezérli.
Többnyelvű felület (magyar / angol), futás közben váltható.

## Funkciók

- **Kártyaolvasás** egy gombbal (olvasás közben megszakítható); az eszköz által
  automatikusan észlelt kártyák azonnal megjelennek. Azonosítók: HEX,
  DEC (13 számjegy), 8H10D, Wiegand 26 (létesítmény kód + kártyaszám).
- **Kártya adatainak vizsgálata** („Kártya info”): chipszám, írhatóság
  biztonságos próbaírással ellenőrizve.
- **Írás**:
  - `T4100` – T5577 chipes, újraírható kártya EM4100 formátumban (ajánlott);
  - `E4100` – EM4100 típusú kártya (csak újraírható eszközökön);
  - `EL4100` – EL4100 chipes kártya (ezzel a móddal lezárás nem elérhető).
- **Lezárás írás után** opció (T5577; a lezárt kártya „Feloldás” gombbal
  újraírhatóvá tehető), **törlés**, **feloldás**.
- **Diagnosztika**: a csatlakoztatott USB eszközök listája (VID 1A86,
  PID DD01 alapján), a megnyitáshoz használt DLL állapota, a kimeneti
  formátum bájtjai, nyitási típuskódok kipróbálása (0–8).
- **Többnyelvű felület** (magyar / angol), menüből futás közben váltás;
  a választás `config.json`-ba mentődik.
- **Szimulált mód**: ha az `IDReader.dll` nem található, a program szimulált
  eszközzel indul (hardver nélküli kipróbáláshoz).

## Hardver

- 125 kHz RFID olvasó/író, EM4100 kompatibilis kártyákkal (olvasás),
  T5577-el (írás), CH341-alapú USB HID eszköz (VID `1A86`, PID `DD01`).
- A gyári `IDReader.dll`-t a program megnyitáskor betölti (a fájlnév a
  `rfid125k.json` `vendorDll` mezőjében módosítható).

## Követelmények és build

- Windows 10/11, .NET 9 SDK (build) vagy .NET 9 Desktop Runtime (futtatás).
- A projekt `x86` platformot használ (a gyári DLL 32 bites).

```sh
dotnet build RFID125k.sln -c Release
dotnet run --project RFID125k.Gui      # vagy a bin\Release\... \RFID125k.Gui.exe
```

Publikálás (minden fájl a `publish/` mappába kerül):

```sh
dotnet publish RFID125k.Gui\RFID125k.Gui.csproj -c Release -o publish
```

A `publish/` mappában előre összeállított kiadás is van; futtatáshoz .NET 9
Desktop Runtime szükséges.

## Konfiguráció

A fájlok a program mappájában vannak (az `.exe` mellett).

### `rfid125k.json` – eszközbeállítások

```json
{
  "vendorDll": "IDReader.dll",
  "openType": 0,
  "writeMethod": "T4100"
}
```

- `vendorDll`: a betöltendő gyári DLL neve (alapértelmezés: `IDReader.dll`).
- `openType`: az `OpenReader` típuskódja (tesztelés szerint a `0` működik).
- `writeMethod`: alapértelmezett írási mód (`T4100` | `E4100` | `EL4100`).

### `config.json` – felület nyelv

```json
{
  "language": "hu"
}
```

A „Nyelv" menüben választott nyelv ide mentődik; ha a program mappájába nem
lehet írni, `%APPDATA%\RFID125k\config.json`-ba esik vissza.

## Nyelvek

A fordítások JSON-szótárakban vannak (`lang.hu.json`, `lang.en.json`), a
Python változattal közös kulcsokkal – egy szótár mindkét alkalmazásban
működik. Új nyelv: másold a `lang.en.json`-t `lang.xx.json`-ra
(kétjegyű nyelvkód), fordítsd le az értékeket, és válaszd ki a menüben.
Ismeretlen kulcsra a magyar szöveg esik vissza, ha az sincs, a kulcs neve.

## Naplózás

- A felület „Napló" mezője az eseményeket mutatja (olvasás/írás/diagnosztika).
- Az eszközréteg a program mappájába `device_trace.log` fájlba is ír
  (nyitások, nyitási kódok, hibakódok) – hibakereséshez.

## Projektfelépítés

```
RFID125k.sln
RFID125k.Core/        # eszközréteg: VendorDllDevice, RfidDeviceFactory,
                      # UsbDeviceScanner, CardData/Em4100Codec, Localization
RFID125k.Gui/         # WinForms felület (MainForm, Program)
vendor/               # gyári IDReader.dll (a build a program mellé másolja)
python/               # platformfüggetlen Python port (lásd: python/README.md)
rfid125k.ico          # alkalmazásikon (az exe-be ágyazva)
```

## Python változat

Ugyanez a program platformfüggetlen Python (CustomTkinter) kiadásban:
```
python/README.md
```
A Python változat nem a gyári DLL-t, hanem közvetlenül a HID protokollt
használja (ezért Windows, Linux és macOS alatt is fut), de a nyelvi
szótárak, a kártyaformátumok (HEX / DEC / 8H10D / Wiegand 26) és a
kártyakezelés szabályai közösek.
