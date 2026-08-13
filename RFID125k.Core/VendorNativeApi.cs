using System.Runtime.InteropServices;

namespace RFID125k.Core;

/// <summary>
/// A gyártói IDReader.dll natív függvényei (32 bites, __stdcall).
/// Az aláírások a DLL disassemblálásából és a demo programból lettek rekonstruálva.
/// </summary>
public static class VendorNativeApi
{
    public const string DefaultDllName = "IDReader.dll";

    /// <summary>Az olvasó USB VID:PID azonosítója (CH341-alapú HID eszköz).</summary>
    public const ushort ReaderVid = 0x1A86;
    public const ushort ReaderPid = 0xDD01;

    public static bool IsAvailable { get; private set; }
    public static string? LoadedDll { get; private set; }

    public static bool TryLoad(string? dllName = null)
    {
        string? path = dllName;
        if (string.IsNullOrWhiteSpace(path))
            path = Path.Combine(AppContext.BaseDirectory, DefaultDllName);

        try
        {
            IntPtr handle = NativeLibrary.Load(path);
            IsAvailable = handle != IntPtr.Zero;
            LoadedDll = IsAvailable ? path : null;
            return IsAvailable;
        }
        catch
        {
            IsAvailable = false;
            LoadedDll = null;
            return false;
        }
    }

    private const string Dll = "IDReader.dll";
    private const CallingConvention Conv = CallingConvention.Winapi;

    /// <summary>Megnyitja az olvasót. handle kezdőértéke -1. Visszatérés: 0=nyitva, 1=már nyitva, 2=eszköz nem található.</summary>
    [DllImport(Dll, CallingConvention = Conv, EntryPoint = "DEV_OpenReader")]
    public static extern int OpenReader(ref int handle, int type);

    [DllImport(Dll, CallingConvention = Conv, EntryPoint = "DEV_CloseReader")]
    public static extern int CloseReader(ref int handle);

    /// <summary>Egyszeri kártyaolvasás (~1 s időtúllépés). 0 = sikeres olvasás, az ID az outBuffer elején.</summary>
    [DllImport(Dll, CallingConvention = Conv, EntryPoint = "DEV_ReadIdCard")]
    public static extern int ReadIdCard(int handle, byte[] outBuffer);

    [DllImport(Dll, CallingConvention = Conv, EntryPoint = "DEV_SetAutoRead")]
    public static extern int SetAutoRead(int handle, byte enable);

    [DllImport(Dll, CallingConvention = Conv, EntryPoint = "DEV_SetBeep")]
    public static extern int SetBeep(int handle, byte enable);

    [DllImport(Dll, CallingConvention = Conv, EntryPoint = "DEV_SetLed")]
    public static extern int SetLed(int handle, byte enable);

    [DllImport(Dll, CallingConvention = Conv, EntryPoint = "DEV_SetFrequency")]
    public static extern int SetFrequency(int handle, byte frequency);

    [DllImport(Dll, CallingConvention = Conv, EntryPoint = "DEV_SetOutFormat")]
    public static extern int SetOutFormat(int handle, byte[] formatBuffer);

    [DllImport(Dll, CallingConvention = Conv, EntryPoint = "DEV_GetOutFormat")]
    public static extern int GetOutFormat(int handle, byte[] outBuffer);

    /// <summary>Kártyaírás EM4100 üzemmódban. mode: 0 = írás, 1 = lezárás (ha támogatott).</summary>
    [DllImport(Dll, CallingConvention = Conv, EntryPoint = "DEV_E4100")]
    public static extern int WriteE4100(int handle, byte[] cardId, byte mode);

    /// <summary>Kártyaírás T5577 üzemmódban. mode: 0 = írás, 1 = lezárás (ha támogatott).</summary>
    [DllImport(Dll, CallingConvention = Conv, EntryPoint = "DEV_T4100")]
    public static extern int WriteT4100(int handle, byte[] cardId, byte mode);

    /// <summary>Kártyaírás EL4100 üzemmódban.</summary>
    [DllImport(Dll, CallingConvention = Conv, EntryPoint = "DEV_EL4100")]
    public static extern int WriteEL4100(int handle, byte[] cardId);

    [DllImport(Dll, CallingConvention = Conv, EntryPoint = "LIB_GetVer")]
    public static extern int GetLibVersion(out int version);
}