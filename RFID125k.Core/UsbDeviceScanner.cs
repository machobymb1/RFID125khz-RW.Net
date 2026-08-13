using System.Runtime.InteropServices;
using System.Text;

namespace RFID125k.Core;

/// <summary>
/// USB eszközök felsorolása a Windows SetupAPI-n keresztül (csak diagnosztikai célra,
/// pl. annak ellenőrzésére, hogy az olvasó a rendszerben látható-e).
/// </summary>
public static class UsbDeviceScanner
{
    private const uint DIGCF_PRESENT = 0x2;
    private const uint DIGCF_ALLCLASSES = 0x4;

    private static readonly Guid GuidDeviceClassUsb = new("36fc9e60-c465-11cf-8056-444553540000");

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr devInfoSet);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr devInfoSet, uint memberIndex, out SP_DEVINFO_DATA devInfoData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetupDiGetDeviceInstanceId(IntPtr devInfoSet, ref SP_DEVINFO_DATA devInfoData,
        StringBuilder deviceInstanceId, uint deviceInstanceIdSize, out uint requiredSize);

    /// <summary>
    /// Visszaadja azoknak a csatlakoztatott USB eszközöknek az instance ID-jait,
    /// amelyek a megadott VID:PID értékkel rendelkeznek. Üres lista = nincs ilyen eszköz.
    /// </summary>
    public static List<string> FindByVidPid(ushort vid, ushort pid)
    {
        string needle = $"USB\\VID_{vid:X4}&PID_{pid:X4}";
        var result = new List<string>();

        Guid classGuid = GuidDeviceClassUsb;
        IntPtr set = SetupDiGetClassDevs(ref classGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT);
        if (set == IntPtr.Zero || set == new IntPtr(-1))
            return result;

        try
        {
            var data = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
            for (uint i = 0; SetupDiEnumDeviceInfo(set, i, out data); i++)
            {
                uint size = 0;
                SetupDiGetDeviceInstanceId(set, ref data, null!, 0, out size);
                if (size == 0) continue;

                var sb = new StringBuilder((int)size);
                if (SetupDiGetDeviceInstanceId(set, ref data, sb, size, out _))
                {
                    string id = sb.ToString();
                    if (id.StartsWith(needle, StringComparison.OrdinalIgnoreCase))
                        result.Add(id);
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }

        return result;
    }
}