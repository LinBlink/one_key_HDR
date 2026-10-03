using System.Runtime.InteropServices;

namespace OneKeyHdr;

internal sealed record RefreshDisplay(string Device, string Name, bool Primary)
{
    public override string ToString() => $"{Name} ({Device}){(Primary ? " · 主屏幕" : "")}";
}

internal static class RefreshRateService
{
    // DEVMODEW's display union occupies offsets 76..91; total size is 220 bytes.
    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode, Size = 220)]
    internal struct Mode
    {
        [FieldOffset(68)] public ushort Size;
        [FieldOffset(72)] public uint Fields;
        [FieldOffset(76)] public int X;
        [FieldOffset(80)] public int Y;
        [FieldOffset(84)] public uint Orientation;
        [FieldOffset(88)] public uint FixedOutput;
        [FieldOffset(168)] public uint Bits;
        [FieldOffset(172)] public uint Width;
        [FieldOffset(176)] public uint Height;
        [FieldOffset(180)] public uint Flags;
        [FieldOffset(184)] public uint Frequency;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DeviceInfo
    {
        public uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Name;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool EnumDisplayDevicesW(string? device, uint index, ref DeviceInfo info, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool EnumDisplaySettingsW(string device, int index, ref Mode mode);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int ChangeDisplaySettingsExW(string device, ref Mode mode, IntPtr window, uint flags, IntPtr param);

    public static List<RefreshDisplay> GetDisplays()
    {
        var result = new List<RefreshDisplay>();
        for (uint i = 0; ; i++)
        {
            var info = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>(), Device = "", Name = "", Id = "", Key = "" };
            if (!EnumDisplayDevicesW(null, i, ref info, 0)) break;
            if ((info.Flags & 1) != 0 && (info.Flags & 8) == 0)
                result.Add(new(info.Device, info.Name, (info.Flags & 4) != 0));
        }
        return result;
    }

    public static RefreshDisplay Resolve(string device) => GetDisplays().FirstOrDefault(d =>
        device.Length == 0 ? d.Primary : d.Device == device)
        ?? throw new InvalidOperationException("未找到刷新率目标屏幕，请检查连接和设置。");

    public static Mode Current(string device)
    {
        var mode = new Mode { Size = (ushort)Marshal.SizeOf<Mode>() };
        if (!EnumDisplaySettingsW(device, -1, ref mode)) throw new InvalidOperationException("无法读取屏幕刷新率。");
        return mode;
    }

    public static List<uint> Rates(string device)
    {
        var current = Current(device);
        var rates = new SortedSet<uint>();
        for (var i = 0; ; i++)
        {
            var mode = new Mode { Size = (ushort)Marshal.SizeOf<Mode>() };
            if (!EnumDisplaySettingsW(device, i, ref mode)) break;
            if (mode.Width == current.Width && mode.Height == current.Height && mode.Bits == current.Bits &&
                mode.Flags == current.Flags && mode.Frequency > 1) rates.Add(mode.Frequency);
        }
        return rates.ToList();
    }

    public static uint Toggle(Settings settings)
    {
        var display = Resolve(settings.RefreshDisplay);
        var mode = Current(display.Device);
        var rates = Rates(display.Device);
        if (rates.Count < 2) throw new InvalidOperationException("当前分辨率下没有两档可切换的刷新率。");
        var a = settings.RefreshRateA == 0 ? rates[0] : settings.RefreshRateA;
        var b = settings.RefreshRateB == 0 ? rates[^1] : settings.RefreshRateB;
        if (a == b || !rates.Contains(a) || !rates.Contains(b))
            throw new InvalidOperationException("所选刷新率不适用于当前分辨率，请重新设置两档刷新率。");
        var target = mode.Frequency == b ? a : b;
        mode.Frequency = target;
        mode.Fields = 0x400000; // DM_DISPLAYFREQUENCY: preserve resolution and desktop layout.
        Check(ChangeDisplaySettingsExW(display.Device, ref mode, IntPtr.Zero, 2, IntPtr.Zero)); // CDS_TEST
        Check(ChangeDisplaySettingsExW(display.Device, ref mode, IntPtr.Zero, 0, IntPtr.Zero));
        return target;
    }

    private static void Check(int code)
    {
        if (code != 0) throw new InvalidOperationException($"刷新率切换失败（驱动返回 {code}）。");
    }
}
