using System.Text.Json;
using System.Runtime.InteropServices;

namespace OneKeyHdr;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        // Explicit diagnostic modes never change HDR or create startup entries.
        if (args.Length == 2 && args[0] == "--diagnose")
        {
            try
            {
                using var hotkey = new HotkeyWindow();
                hotkey.Register(new Settings());
                File.WriteAllText(args[1], JsonSerializer.Serialize(new
                {
                    PathSize = Marshal.SizeOf<DisplayService.PathInfo>(),
                    ModeSize = Marshal.SizeOf<DisplayService.ModeInfo>(),
                    HotkeyRegistered = true,
                    Displays = DisplayService.GetDisplays().Select(d => new { d.Name, d.Supported, d.Enabled, d.Modern })
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { File.WriteAllText(args[1], ex.ToString()); Environment.ExitCode = 1; }
            return;
        }
        using var mutex = new Mutex(true, @"Local\OneKeyHdr.Tray", out var created);
        if (!created) { MessageBox.Show("OneKey HDR 已在运行，请查看右下角托盘（或隐藏图标区域）。", "OneKey HDR"); return; }
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => MessageBox.Show(e.Exception.Message, "OneKey HDR 错误");
        using var app = new TrayApplication();
        Application.Run(app);
    }
}

internal sealed class TrayApplication : ApplicationContext
{
    private Settings settings = new();
    private readonly HotkeyWindow hotkey = new();
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip menu = new();
    private readonly ToolStripMenuItem status = new("正在读取 HDR 状态…") { Enabled = false };
    private readonly ToolStripMenuItem startup = new("登录 Windows 时启动");
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 5000 };
    private SettingsForm? settingsForm;
    private bool busy;
    private bool registered;

    public TrayApplication()
    {
        tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "OneKey HDR", ContextMenuStrip = menu, Visible = true };
        menu.Items.Add(status);
        menu.Items.Add("切换 HDR", null, (_, _) => Toggle());
        menu.Items.Add("设置快捷键与显示器…", null, (_, _) => OpenSettings());
        menu.Items.Add(startup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitThread());
        startup.Click += (_, _) =>
        {
            try { Settings.SetStartup(!Settings.StartupEnabled); startup.Checked = Settings.StartupEnabled; }
            catch (Exception ex) { Notify(ex.Message, true); }
        };
        menu.Opening += (_, _) => { RefreshStatus(); startup.Checked = Settings.StartupEnabled; };
        tray.DoubleClick += (_, _) => Toggle();
        hotkey.Pressed += Toggle;
        try { settings = Settings.Load(); }
        catch (Exception ex) { Notify($"配置读取失败，使用默认快捷键：{ex.Message}", true); }
        try { hotkey.Register(settings); registered = true; }
        catch (Exception ex) { Notify(ex.Message + " 可在托盘设置中更换。", true); }
        timer.Tick += (_, _) => RefreshStatus();
        timer.Start();
        RefreshStatus();
    }

    private void Notify(string message, bool error = false)
    {
        tray.ShowBalloonTip(4000, "OneKey HDR", message, error ? ToolTipIcon.Warning : ToolTipIcon.Info);
    }

    private List<Display> Targets() => DisplayService.GetDisplays()
        .Where(d => d.Supported && (settings.DisplayPath.Length == 0 || d.DevicePath == settings.DisplayPath)).ToList();

    private void RefreshStatus()
    {
        if (busy) return;
        try
        {
            var displays = Targets();
            var text = displays.Count == 0 ? "无可用 HDR 显示器" : displays.All(d => d.Enabled) ? "HDR 已开启" : displays.All(d => !d.Enabled) ? "HDR 已关闭" : "HDR 部分开启";
            status.Text = text + (registered ? $" · {settings.HotkeyText}" : " · 快捷键未注册");
            tray.Text = ("OneKey HDR · " + text)[..Math.Min(63, ("OneKey HDR · " + text).Length)];
        }
        catch (Exception ex) { status.Text = "读取失败：" + ex.Message; }
    }

    private async void Toggle()
    {
        if (busy) return;
        busy = true;
        try
        {
            var displays = Targets();
            if (displays.Count == 0) throw new InvalidOperationException("未找到可用的 HDR 显示器。请检查显示器连接及托盘设置中的目标。");
            var enable = !displays.All(d => d.Enabled);
            var errors = new List<string>();
            foreach (var display in displays.Where(d => d.Enabled != enable))
            {
                try { DisplayService.Set(display, enable); }
                catch (Exception ex) { errors.Add($"{display.Name}：{ex.Message}"); }
            }
            // Drivers may return before the display transition finishes.
            List<Display> after = [];
            for (var attempt = 0; attempt < 6; attempt++)
            {
                await Task.Delay(400);
                after = DisplayService.GetDisplays();
                if (displays.All(d => after.Any(a => a.DevicePath == d.DevicePath && a.Enabled == enable))) break;
            }
            foreach (var display in displays)
                if (!after.Any(d => d.DevicePath == display.DevicePath && d.Enabled == enable)) errors.Add($"{display.Name}：未确认达到目标状态。");
            Notify(errors.Count == 0 ? $"已{(enable ? "开启" : "关闭")} HDR（{displays.Count} 个显示器）。" : string.Join("\n", errors), errors.Count > 0);
        }
        catch (Exception ex) { Notify(ex.Message, true); }
        finally { busy = false; RefreshStatus(); }
    }

    private void OpenSettings()
    {
        if (settingsForm != null) { settingsForm.Activate(); return; }
        try
        {
            settingsForm = new SettingsForm(settings, candidate =>
            {
                var changed = !registered || candidate.Modifiers != settings.Modifiers || candidate.Key != settings.Key;
                if (changed) hotkey.Register(candidate);
                try { candidate.Save(); }
                catch
                {
                    if (changed)
                    {
                        if (registered) hotkey.Register(settings);
                        else hotkey.Unregister();
                    }
                    throw;
                }
                settings = candidate;
                registered = true;
                RefreshStatus();
            });
            settingsForm.FormClosed += (_, _) => { settingsForm?.Dispose(); settingsForm = null; };
            // Run it modally so the window is always foreground and remains tied
            // to this tray action even though ApplicationContext has no main form.
            settingsForm.ShowDialog();
        }
        catch (Exception ex)
        {
            settingsForm = null;
            MessageBox.Show(ex.ToString(), "OneKey HDR 设置窗口无法打开", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Stop(); timer.Dispose();
            settingsForm?.Dispose();
            hotkey.Dispose();
            tray.Visible = false; tray.Dispose(); menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
