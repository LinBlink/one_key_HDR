using System.ComponentModel;
using System.Runtime.InteropServices;

namespace OneKeyHdr;

internal sealed record Display(DisplayService.Luid Adapter, uint Id, string Name, string DevicePath,
    bool Supported, bool Enabled, bool Modern)
{
    public override string ToString() => $"{Name} — {(Supported ? (Enabled ? "HDR 已开启" : "HDR 已关闭") : "不支持 HDR")}";
}

internal static class DisplayService
{
    [StructLayout(LayoutKind.Sequential)] internal struct Luid { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential)] internal struct Header { public uint Type, Size; public Luid Adapter; public uint Id; }
    [StructLayout(LayoutKind.Sequential)] internal struct Source { public Luid Adapter; public uint Id, Mode, Flags; }
    [StructLayout(LayoutKind.Sequential)] internal struct Target
    {
        public Luid Adapter;
        public uint Id, Mode, Technology, Rotation, Scaling, Numerator, Denominator, Scanline;
        public int Available;
        public uint Flags;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct PathInfo { public Source Source; public Target Target; public uint Flags; }
    // DISPLAYCONFIG_MODE_INFO includes an eight-byte aligned 48-byte union.
    [StructLayout(LayoutKind.Explicit, Size = 64)] internal struct ModeInfo { [FieldOffset(16)] public long Alignment; }
    [StructLayout(LayoutKind.Sequential)] internal struct Color { public Header Header; public uint Flags, Encoding, Bits; }
    [StructLayout(LayoutKind.Sequential)] internal struct Color2 { public Header Header; public uint Flags, Encoding, Bits, ActiveMode; }
    [StructLayout(LayoutKind.Sequential)] internal struct State { public Header Header; public uint Enabled; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct TargetName
    {
        public Header Header;
        public uint Flags, Technology;
        public ushort Manufacturer, Product;
        public uint Connector;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DevicePath;
    }
    [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
    [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint paths, [Out] PathInfo[] pathArray, ref uint modes, [Out] ModeInfo[] modeArray, IntPtr topology);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] private static extern int GetColor(ref Color info);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] private static extern int GetColor2(ref Color2 info);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] private static extern int GetName(ref TargetName info);
    [DllImport("user32.dll")] private static extern int DisplayConfigSetDeviceInfo(ref State state);

    private static Header MakeHeader<T>(uint type, Luid adapter, uint id) => new() { Type = type, Size = (uint)Marshal.SizeOf<T>(), Adapter = adapter, Id = id };
    private static void Check(int code) { if (code != 0) throw new Win32Exception(code); }

    public static List<Display> GetDisplays()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Check(GetDisplayConfigBufferSizes(2, out var paths, out var modes));
            var pathArray = new PathInfo[paths];
            var modeArray = new ModeInfo[modes];
            var result = QueryDisplayConfig(2, ref paths, pathArray, ref modes, modeArray, IntPtr.Zero);
            if (result == 122) continue; // Topology changed between sizing and querying.
            Check(result);
            var displays = new List<Display>();
            foreach (var path in pathArray.Take((int)paths))
            {
                var t = path.Target;
                if (displays.Any(d => d.Adapter.Equals(t.Adapter) && d.Id == t.Id)) continue;
                var name = new TargetName { Header = MakeHeader<TargetName>(2, t.Adapter, t.Id), Name = "", DevicePath = "" };
                Check(GetName(ref name));
                var modern = new Color2 { Header = MakeHeader<Color2>(15, t.Adapter, t.Id) };
                var code = GetColor2(ref modern);
                bool supported, enabled;
                if (code == 0)
                {
                    supported = (modern.Flags & 16) != 0;
                    enabled = (modern.Flags & 32) != 0;
                }
                else if (code is 87 or 50 or 120)
                {
                    var legacy = new Color { Header = MakeHeader<Color>(9, t.Adapter, t.Id) };
                    Check(GetColor(ref legacy));
                    supported = (legacy.Flags & 1) != 0 && (legacy.Flags & 4) == 0;
                    enabled = (legacy.Flags & 2) != 0;
                }
                else { Check(code); return []; }
                displays.Add(new(t.Adapter, t.Id, string.IsNullOrWhiteSpace(name.Name) ? $"显示器 {t.Id}" : name.Name,
                    name.DevicePath, supported, enabled, code == 0));
            }
            return displays;
        }
        throw new InvalidOperationException("显示器配置正在变化，请稍后重试。");
    }

    public static void Set(Display display, bool enabled)
    {
        var state = new State { Header = MakeHeader<State>(display.Modern ? 16u : 10u, display.Adapter, display.Id), Enabled = enabled ? 1u : 0u };
        Check(DisplayConfigSetDeviceInfo(ref state));
    }
}
