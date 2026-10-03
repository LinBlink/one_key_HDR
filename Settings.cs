using System.Text.Json;
using Microsoft.Win32;

namespace OneKeyHdr;

internal sealed record Settings(uint Modifiers = 3, Keys Key = Keys.H, string DisplayPath = "",
    uint RefreshModifiers = 3, Keys RefreshKey = Keys.R, string RefreshDisplay = "",
    uint RefreshRateA = 0, uint RefreshRateB = 0)
{
    public static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OneKeyHdr");
    public static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");
    public string HotkeyText => string.Join(" + ", new[] { (Modifiers & 2) != 0 ? "Ctrl" : null, (Modifiers & 1) != 0 ? "Alt" : null, (Modifiers & 4) != 0 ? "Shift" : null, (Modifiers & 8) != 0 ? "Win" : null, Key.ToString() }.Where(s => s != null));
    public static Settings Load()
    {
        if (!File.Exists(FilePath)) return new();
        var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? throw new InvalidDataException("配置为空。");
        settings.Validate();
        return settings;
    }
    public static bool IsValidKey(Keys key) => (key >= Keys.A && key <= Keys.Z) || (key >= Keys.D0 && key <= Keys.D9) || (key >= Keys.F1 && key <= Keys.F11);
    public string RefreshHotkeyText => (this with { Modifiers = RefreshModifiers, Key = RefreshKey }).HotkeyText;
    public void Validate()
    {
        if (Modifiers == 0 || Modifiers > 15 || !IsValidKey(Key) ||
            RefreshModifiers == 0 || RefreshModifiers > 15 || !IsValidKey(RefreshKey))
            throw new InvalidDataException("快捷键配置无效，请选择修饰键和字母、数字或功能键。");
        if (Modifiers == RefreshModifiers && Key == RefreshKey)
            throw new InvalidDataException("HDR 与刷新率快捷键不能相同。");
        if (DisplayPath == null || RefreshDisplay == null || RefreshRateA == 1 || RefreshRateB == 1 ||
            (RefreshRateA != 0 && RefreshRateA == RefreshRateB))
            throw new InvalidDataException("显示器或刷新率配置无效，两档刷新率必须不同。");
    }
    public void Save()
    {
        Validate();
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(FilePath + ".tmp", FilePath, true);
    }
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool StartupEnabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue("OneKeyHdr") is string; }
    }
    public static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue("OneKeyHdr", $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue("OneKeyHdr", false);
    }
}
