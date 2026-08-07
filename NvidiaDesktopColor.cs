using System.Runtime.InteropServices;
using Microsoft.Win32;

/// <summary>
/// NVIDIA Control Panel desktop brightness/contrast/gamma.
/// Uses the driver formula (values centered on 100) and undocumented
/// NvAPI_DISP_SetTargetGammaCorrection — same path as the panel.
/// </summary>
internal static class NvidiaDesktopColor
{
    private const uint IdInitialize = 0x0150E828;
    private const uint IdUnload = 0xD22BDD7E;
    private const uint IdEnumPhysicalGpus = 0xE5AC921F;
    private const uint IdGetConnectedDisplayIds = 0x0078DBA2;
    private const uint IdGetGdiPrimaryDisplayId = 0x1E9D8A31;
    private const uint IdSetTargetGammaCorrection = 0x7082A053;

    private const int NvApiOk = 0;
    private const int RampLen = 1024;

    private const int RegBrightnessR = 3538946;
    private const int RegContrastR = 3538949;
    private const int RegGammaR = 3538952;

    [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr QueryInterface(uint id);

    private delegate int NvApiInitialize();
    private delegate int NvApiUnload();
    private delegate int NvApiEnumPhysicalGpus([Out] IntPtr[] gpus, out int count);
    private delegate int NvApiGpuGetConnectedDisplayIds(IntPtr gpu, IntPtr ids, ref int count, uint flags);
    private delegate int NvApiDispGetGdiPrimaryDisplayId(out uint displayId);
    private delegate int NvApiDispSetTargetGammaCorrection(uint displayId, IntPtr gammaCorrection);

    public static void Apply(int brightnessPercent, int contrastPercent, double gamma)
    {
        var b = PanelPercentToNv(brightnessPercent);
        var c = PanelPercentToNv(contrastPercent);
        var g = Math.Clamp(gamma, 0.30, 1.80) * 100.0;

        var initPtr = Require(IdInitialize, "Initialize");
        var status = Marshal.GetDelegateForFunctionPointer<NvApiInitialize>(initPtr)();
        if (status != NvApiOk)
            throw new InvalidOperationException($"NvAPI_Initialize failed ({status}).");

        try
        {
            // Primary monitor only (same idea as DV). Multi-monitor users were getting CG on every NVIDIA output.
            var displayId = GetPrimaryDisplayId();
            var setPtr = Require(IdSetTargetGammaCorrection, "SetTargetGammaCorrection");
            var set = Marshal.GetDelegateForFunctionPointer<NvApiDispSetTargetGammaCorrection>(setPtr);

            var block = BuildGammaCorrectionBlock(b, c, g);
            try
            {
                var r = set(displayId, block);
                if (r != NvApiOk)
                    throw new InvalidOperationException(
                        $"SetTargetGammaCorrection failed on primary display 0x{displayId:X8} (status={r}).");
            }
            finally
            {
                Marshal.FreeHGlobal(block);
            }

            WriteRegistryAllDevices(b, c, g);
            Console.WriteLine($"      primary display 0x{displayId:X8}  NV scale B={b:0} C={c:0} G={g:0}");
        }
        finally
        {
            var unload = QueryInterface(IdUnload);
            if (unload != IntPtr.Zero)
                Marshal.GetDelegateForFunctionPointer<NvApiUnload>(unload)();
        }
    }

    private static double PanelPercentToNv(int percent) =>
        100.0 + (Math.Clamp(percent, 0, 100) - 50) * 0.4;

    private static float CalculateSample(int index, double brightness, double contrast, double gamma)
    {
        var c = (contrast - 100.0) / 100.0;
        double x = index / 1023.0 - 0.5;
        c = c <= 0.0 ? (c + 1.0) * x : x / (1.0 - c);

        var y = (brightness - 100.0) / 100.0 + c + 0.5;
        y = Math.Clamp(y, 0.0, 1.0);
        y = Math.Pow(y, 1.0 / (gamma / 100.0));
        return (float)Math.Clamp(y, 0.0, 1.0);
    }

    private static IntPtr BuildGammaCorrectionBlock(double brightness, double contrast, double gamma)
    {
        const int floatCount = 3 * RampLen;
        var size = 4 + floatCount * 4 + 4;
        var ptr = Marshal.AllocHGlobal(size);
        Marshal.WriteInt32(ptr, size | (1 << 16));
        Marshal.WriteInt32(ptr, size - 4, 1);

        for (var i = 0; i < RampLen; i++)
        {
            var sample = CalculateSample(i, brightness, contrast, gamma);
            for (var ch = 0; ch < 3; ch++)
                Marshal.WriteInt32(ptr, 4 + (i * 3 + ch) * 4, BitConverter.SingleToInt32Bits(sample));
        }

        return ptr;
    }

    private static uint GetPrimaryDisplayId()
    {
        var primaryPtr = QueryInterface(IdGetGdiPrimaryDisplayId);
        if (primaryPtr != IntPtr.Zero)
        {
            var getPrimary = Marshal.GetDelegateForFunctionPointer<NvApiDispGetGdiPrimaryDisplayId>(primaryPtr);
            if (getPrimary(out var primaryId) == NvApiOk && primaryId != 0)
                return primaryId;
        }

        // Fallback: first active/connected NVIDIA output (better than painting every monitor).
        var ids = GetActiveDisplayIds();
        if (ids.Count == 0)
            throw new InvalidOperationException("No active NVIDIA displays found (and GDI primary lookup failed).");
        Console.WriteLine("      warn: GetGDIPrimaryDisplayId unavailable — using first active NVIDIA display.");
        return ids[0];
    }

    private static List<uint> GetActiveDisplayIds()
    {
        var enumPtr = Require(IdEnumPhysicalGpus, "EnumPhysicalGPUs");
        var getIdsPtr = Require(IdGetConnectedDisplayIds, "GPU_GetConnectedDisplayIds");
        var enumerate = Marshal.GetDelegateForFunctionPointer<NvApiEnumPhysicalGpus>(enumPtr);
        var getIds = Marshal.GetDelegateForFunctionPointer<NvApiGpuGetConnectedDisplayIds>(getIdsPtr);

        var gpus = new IntPtr[64];
        var st = enumerate(gpus, out var gpuCount);
        if (st != NvApiOk || gpuCount == 0)
            throw new InvalidOperationException($"EnumPhysicalGPUs failed ({st}).");

        var result = new List<uint>();
        for (var g = 0; g < gpuCount; g++)
        {
            var count = 0;
            st = getIds(gpus[g], IntPtr.Zero, ref count, 0);
            if (st != NvApiOk || count == 0)
                continue;

            var stride = Marshal.SizeOf<NvGpuDisplayIds>();
            var mem = Marshal.AllocHGlobal(stride * count);
            try
            {
                var first = new NvGpuDisplayIds { version = (uint)(stride | (3 << 16)) };
                Marshal.StructureToPtr(first, mem, false);

                st = getIds(gpus[g], mem, ref count, 0);
                if (st != NvApiOk)
                    continue;

                for (var i = 0; i < count; i++)
                {
                    var d = Marshal.PtrToStructure<NvGpuDisplayIds>(mem + i * stride);
                    if ((d.flags & 0x4) != 0 || (d.flags & 0x40) != 0)
                        result.Add(d.displayId);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(mem);
            }
        }

        return result.Distinct().ToList();
    }

    private static void WriteRegistryAllDevices(double brightness, double contrast, double gamma)
    {
        try
        {
            using var devices = Registry.CurrentUser.OpenSubKey(
                @"Software\NVIDIA Corporation\Global\NVTweak\Devices", writable: false);
            if (devices is null)
                return;

            foreach (var name in devices.GetSubKeyNames())
            {
                using var color = Registry.CurrentUser.OpenSubKey(
                    $@"Software\NVIDIA Corporation\Global\NVTweak\Devices\{name}\Color", writable: true);
                if (color is null)
                    continue;

                var b = (int)Math.Round(brightness);
                var c = (int)Math.Round(contrast);
                var g = (int)Math.Round(gamma);
                for (var i = 0; i < 3; i++)
                {
                    color.SetValue((RegBrightnessR + i).ToString(), b, RegistryValueKind.DWord);
                    color.SetValue((RegContrastR + i).ToString(), c, RegistryValueKind.DWord);
                    color.SetValue((RegGammaR + i).ToString(), g, RegistryValueKind.DWord);
                }

                color.SetValue("NvCplGammaSet", 1, RegistryValueKind.DWord);
            }
        }
        catch
        {
            // Registry is best-effort for panel UI; visual path is NVAPI.
        }
    }

    private static IntPtr Require(uint id, string name)
    {
        var p = QueryInterface(id);
        if (p == IntPtr.Zero)
            throw new InvalidOperationException($"nvapi QueryInterface({name}) failed.");
        return p;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NvGpuDisplayIds
    {
        public uint version;
        public int connectorType;
        public uint displayId;
        public uint flags;
    }
}
