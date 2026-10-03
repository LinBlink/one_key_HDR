using System.ComponentModel;
using System.Runtime.InteropServices;

namespace OneKeyHdr;

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private int activeId;
    public event Action? Pressed;
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
    public HotkeyWindow() => CreateHandle(new CreateParams { Caption = "OneKeyHdr.Hotkey", Parent = new IntPtr(-3) });
    public void Register(Settings settings) => Register(settings.Modifiers, settings.Key);
    public void Register(uint modifiers, Keys key)
    {
        var next = activeId == 1 ? 2 : 1;
        if (!RegisterHotKey(Handle, next, modifiers | 0x4000, (uint)key))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "快捷键注册失败，可能已被其他程序占用。请选择其他组合。");
        if (activeId != 0) UnregisterHotKey(Handle, activeId);
        activeId = next;
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && m.WParam.ToInt32() == activeId) Pressed?.Invoke();
        base.WndProc(ref m);
    }
    public void Unregister() { if (activeId != 0) UnregisterHotKey(Handle, activeId); activeId = 0; }
    public void Dispose() { Unregister(); DestroyHandle(); }
}
