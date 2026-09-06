using System.Text.Json;
using Microsoft.Win32;

namespace OneKeyHdr;

internal sealed record Settings(uint Modifiers = 3, Keys Key = Keys.H, string DisplayPath = "")
{
    public static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OneKeyHdr");
    public static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");
    public string HotkeyText => string.Join(" + ", new[] { (Modifiers & 2) != 0 ? "Ctrl" : null, (Modifiers & 1) != 0 ? "Alt" : null, (Modifiers & 4) != 0 ? "Shift" : null, (Modifiers & 8) != 0 ? "Win" : null, Key.ToString() }.Where(s => s != null));
    public static Settings Load()
    {
        if (!File.Exists(FilePath)) return new();
        var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? throw new InvalidDataException("配置为空。");
        if (settings.Modifiers == 0 || settings.Modifiers > 15 || !IsValidKey(settings.Key)) throw new InvalidDataException("快捷键配置无效。");
        return settings;
    }
    public static bool IsValidKey(Keys key) => (key >= Keys.A && key <= Keys.Z) || (key >= Keys.D0 && key <= Keys.D9) || (key >= Keys.F1 && key <= Keys.F11);
    public void Save()
    {
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
