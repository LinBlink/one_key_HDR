namespace OneKeyHdr;

internal sealed class SettingsForm : Form
{
    private sealed record DisplayOption(string Path, string Label) { public override string ToString() => Label; }
    public SettingsForm(Settings current, Action<Settings> save)
    {
        Text = "OneKey HDR · 设置";
        ClientSize = new Size(500, 300);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 10);
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20) };
        layout.Controls.Add(new Label { Text = "全局快捷键", AutoSize = true });
        var modifiers = new FlowLayoutPanel { Width = 450, Height = 35 };
        var checks = new[] { ("Ctrl", 2u), ("Alt", 1u), ("Shift", 4u), ("Win", 8u) }
            .Select(item => (Box: new CheckBox { Text = item.Item1, Checked = (current.Modifiers & item.Item2) != 0, AutoSize = true }, Flag: item.Item2)).ToArray();
        foreach (var item in checks) modifiers.Controls.Add(item.Box);
        var key = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        foreach (var value in Enum.GetValues<Keys>().Distinct().Where(Settings.IsValidKey)) key.Items.Add(value);
        key.SelectedItem = current.Key;
        modifiers.Controls.Add(key);
        layout.Controls.Add(modifiers);
        layout.Controls.Add(new Label { Text = "切换目标（所有显示器：统一开启或关闭）", AutoSize = true, Margin = new Padding(3, 14, 3, 8) });
        var display = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 450 };
        display.Items.Add(new DisplayOption("", "所有支持 HDR 的显示器"));
        try
        {
            foreach (var item in DisplayService.GetDisplays()) display.Items.Add(new DisplayOption(item.DevicePath, item.ToString()));
        }
        catch (Exception ex)
        {
            display.Items.Add(new DisplayOption("", "无法读取显示器（保存后可重试）"));
            display.Tag = ex;
        }
        var selected = display.Items.Cast<DisplayOption>().FirstOrDefault(d => d.Path == current.DisplayPath);
        if (selected == null)
        {
            selected = new DisplayOption(current.DisplayPath, "已保存的显示器（当前未连接）");
            display.Items.Add(selected);
        }
        display.SelectedItem = selected;
        layout.Controls.Add(display);
        layout.Controls.Add(new Label { Text = "Windows 保留组合或其他程序占用的快捷键无法保存。", AutoSize = true, Margin = new Padding(3, 12, 3, 8) });
        var button = new Button { Text = "保存", AutoSize = true, Margin = new Padding(3, 10, 3, 3) };
        button.Click += (_, _) =>
        {
            try
            {
                uint flags = 0;
                foreach (var item in checks) if (item.Box.Checked) flags |= item.Flag;
                if (flags == 0 || key.SelectedItem is not Keys chosen) throw new InvalidOperationException("请至少选择一个修饰键和一个字母、数字或功能键。");
                save(new Settings(flags, chosen, ((DisplayOption)display.SelectedItem!).Path));
                Close();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        layout.Controls.Add(button);
        Controls.Add(layout);
        AcceptButton = button;
    }
}
