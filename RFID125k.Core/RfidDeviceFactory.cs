using System.Text.Json;

namespace RFID125k.Core;

/// <summary>
/// Létrehozza a használandó eszközt: ha az IDReader.dll elérhető, a valódi
/// olvasót, különben szimulált eszközt (hardver nélküli kipróbáláshoz).
///
/// Konfigurálható a program mappájában lévő rfid125k.json fájllal:
/// {
///   "vendorDll": "IDReader.dll",   // DLL neve
///   "openType": 0,                 // OpenReader típuskód (tesztelés szerint 0 működik)
///   "writeMethod": "T4100"         // T4100 | E4100 | EL4100
/// }
/// </summary>
public static class RfidDeviceFactory
{
    private const string ConfigFileName = "rfid125k.json";

    public static IRfidDevice CreateDevice()
    {
        AppConfig cfg = LoadConfig() ?? new AppConfig();
        string vendorDll = string.IsNullOrWhiteSpace(cfg.VendorDll)
            ? VendorNativeApi.DefaultDllName
            : cfg.VendorDll;

        if (VendorNativeApi.TryLoad(vendorDll))
        {
            return new VendorDllDevice
            {
                OpenType = cfg.OpenType ?? 0,
                WriteMethod = ParseWriteMethod(cfg.WriteMethod)
            };
        }

        return new SimulatedDevice();
    }

    public static string DescribeDevice() =>
        VendorNativeApi.IsAvailable
            ? Localization.T("factory.dll", VendorNativeApi.LoadedDll)
            : Localization.T("factory.simulated");

    private static WriteMethod ParseWriteMethod(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            "E4100" => WriteMethod.E4100,
            "EL4100" => WriteMethod.EL4100,
            _ => WriteMethod.T4100
        };

    private static AppConfig? LoadConfig()
    {
        string path = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private sealed class AppConfig
    {
        public string? VendorDll { get; set; }
        public int? OpenType { get; set; }
        public string? WriteMethod { get; set; }
    }
}