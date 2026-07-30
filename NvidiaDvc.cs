using System.Runtime.InteropServices;

/// <summary>Digital Vibrance via NVIDIA nvapi64.dll.</summary>
internal static class NvidiaDvc
{
    private const uint IdInitialize = 0x0150E828;
    private const uint IdUnload = 0xD22BDD7E;
    private const uint IdEnumNvidiaDisplayHandle = 0x9ABDD40D;
    private const uint IdGetAssociatedNvidiaDisplayHandle = 0x35C29134;
    private const uint IdGetDvcInfoEx = 0x0E45002D;
    private const uint IdSetDvcLevelEx = 0x4A80657C;
    private const uint IdSetDvcLevel = 0x172409B4;
    private const int NvApiOk = 0;

    [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr QueryInterface(uint id);

    private delegate int NvApiInitialize();
    private delegate int NvApiUnload();
    private delegate int NvApiEnumNvidiaDisplayHandle(int thisEnum, out int displayHandle);
    private delegate int NvApiGetAssociatedNvidiaDisplayHandle(
        [MarshalAs(UnmanagedType.LPStr)] string displayName, out int displayHandle);
    private delegate int NvApiGetDvcInfoEx(int displayHandle, ref DvcInfoEx info);
    private delegate int NvApiSetDvcLevelEx(int displayHandle, ref DvcInfoEx info);
    private delegate int NvApiSetDvcLevel(int displayHandle, uint outputId, int level);

    [StructLayout(LayoutKind.Sequential)]
    private struct DvcInfoEx
    {
        public uint version;
        public int currentLevel, minLevel, maxLevel, defaultLevel;
    }

    public static void SetDigitalVibrancePercent(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        var initPtr = QueryInterface(IdInitialize);
        if (initPtr == IntPtr.Zero)
            throw new InvalidOperationException("nvapi Initialize QI failed.");

        var st = Marshal.GetDelegateForFunctionPointer<NvApiInitialize>(initPtr)();
        if (st != NvApiOk)
            throw new InvalidOperationException($"NvAPI_Initialize failed ({st}).");

        try
        {
            var handle = GetPrimaryDisplayHandle();
            if (!TrySetEx(handle, percent, out var path) && !TrySetLegacy(handle, percent, out path))
                throw new InvalidOperationException("DVC set failed.");
            Console.WriteLine($"      DV path={path}");
        }
        finally
        {
            var unload = QueryInterface(IdUnload);
            if (unload != IntPtr.Zero)
                Marshal.GetDelegateForFunctionPointer<NvApiUnload>(unload)();
        }
    }

    private static int GetPrimaryDisplayHandle()
    {
        var primary = GetPrimaryGdiDeviceName();
        if (primary is not null)
        {
            var assocPtr = QueryInterface(IdGetAssociatedNvidiaDisplayHandle);
            if (assocPtr != IntPtr.Zero)
            {
                var assoc = Marshal.GetDelegateForFunctionPointer<NvApiGetAssociatedNvidiaDisplayHandle>(assocPtr);
                if (assoc(primary, out var h) == NvApiOk)
                    return h;
            }
        }

        var enumPtr = QueryInterface(IdEnumNvidiaDisplayHandle);
        if (enumPtr == IntPtr.Zero)
            throw new InvalidOperationException("EnumNvidiaDisplayHandle missing.");
        var enumerate = Marshal.GetDelegateForFunctionPointer<NvApiEnumNvidiaDisplayHandle>(enumPtr);
        if (enumerate(0, out var first) != NvApiOk)
            throw new InvalidOperationException("No NVIDIA display handle.");
        return first;
    }

    private static string? GetPrimaryGdiDeviceName()
    {
        var device = new DisplayDevice { cb = Marshal.SizeOf<DisplayDevice>() };
        for (uint i = 0; EnumDisplayDevices(null, i, ref device, 0); i++)
        {
            if ((device.StateFlags & 0x1) != 0 && (device.StateFlags & 0x4) != 0)
                return device.DeviceName;
        }

        return null;
    }

    private static bool TrySetEx(int handle, int percent, out string path)
    {
        path = "Ex";
        var getPtr = QueryInterface(IdGetDvcInfoEx);
        var setPtr = QueryInterface(IdSetDvcLevelEx);
        if (getPtr == IntPtr.Zero || setPtr == IntPtr.Zero)
            return false;

        var get = Marshal.GetDelegateForFunctionPointer<NvApiGetDvcInfoEx>(getPtr);
        var set = Marshal.GetDelegateForFunctionPointer<NvApiSetDvcLevelEx>(setPtr);
        var info = new DvcInfoEx { version = (uint)(Marshal.SizeOf<DvcInfoEx>() | (1 << 16)) };
        if (get(handle, ref info) != NvApiOk)
            return false;

        info.currentLevel = PanelPercentToApiLevel(percent, info.minLevel, info.maxLevel, info.defaultLevel);
        path = $"Ex(level={info.currentLevel})";
        return set(handle, ref info) == NvApiOk;
    }

    private static bool TrySetLegacy(int handle, int percent, out string path)
    {
        path = "Legacy";
        var setPtr = QueryInterface(IdSetDvcLevel);
        if (setPtr == IntPtr.Zero)
            return false;

        var level = PanelPercentToApiLevel(percent, 0, 63, 0);
        path = $"Legacy(level={level})";
        return Marshal.GetDelegateForFunctionPointer<NvApiSetDvcLevel>(setPtr)(handle, 0, level) == NvApiOk;
    }

    private static int PanelPercentToApiLevel(int panelPercent, int minLevel, int maxLevel, int defaultLevel)
    {
        panelPercent = Math.Clamp(panelPercent, 0, 100);
        if (maxLevel >= 100)
            return Math.Clamp(panelPercent, minLevel, maxLevel);

        if (panelPercent <= 50)
        {
            if (panelPercent == 50)
                return defaultLevel;
            var t = panelPercent / 50.0;
            return (int)Math.Round(minLevel + (defaultLevel - minLevel) * t);
        }

        var u = (panelPercent - 50) / 50.0;
        return (int)Math.Round(defaultLevel + (maxLevel - defaultLevel) * u);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DisplayDevice lpDisplayDevice, uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }
}
