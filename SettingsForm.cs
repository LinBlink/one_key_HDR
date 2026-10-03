namespace OneKeyHdr;

internal sealed class SettingsForm : Form
{
    private sealed record DisplayOption(string Path, string Label) { public override string ToString() => Label; }
    public SettingsForm(Settings current, Action<Settings> save)
    {
        Text = "OneKey HDR · 设置";
        ClientSize = new Size(520, 560);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 10);
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(20) };
        layout.Controls.Add(new Label { Text = "HDR 全局快捷键", AutoSize = true });
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
        layout.Controls.Add(new Label { Text = "刷新率全局快捷键", AutoSize = true, Margin = new Padding(3, 14, 3, 8) });
        var refreshModifiers = new FlowLayoutPanel { Width = 470, Height = 35 };
        var refreshChecks = new[] { ("Ctrl", 2u), ("Alt", 1u), ("Shift", 4u), ("Win", 8u) }
            .Select(item => (Box: new CheckBox { Text = item.Item1, Checked = (current.RefreshModifiers & item.Item2) != 0, AutoSize = true }, Flag: item.Item2)).ToArray();
        foreach (var item in refreshChecks) refreshModifiers.Controls.Add(item.Box);
        var refreshKey = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        foreach (var value in Enum.GetValues<Keys>().Distinct().Where(Settings.IsValidKey)) refreshKey.Items.Add(value);
        refreshKey.SelectedItem = current.RefreshKey;
        refreshModifiers.Controls.Add(refreshKey);
        layout.Controls.Add(refreshModifiers);
        layout.Controls.Add(new Label { Text = "刷新率目标屏幕（独立于 HDR 目标）", AutoSize = true });
        var refreshDisplay = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 470 };
        refreshDisplay.Items.Add(new DisplayOption("", "当前主屏幕"));
        foreach (var item in RefreshRateService.GetDisplays()) refreshDisplay.Items.Add(new DisplayOption(item.Device, item.ToString()));
        var refreshSelected = refreshDisplay.Items.Cast<DisplayOption>().FirstOrDefault(d => d.Path == current.RefreshDisplay);
        if (refreshSelected == null)
        {
            refreshSelected = new DisplayOption(current.RefreshDisplay, "已保存的屏幕（当前未连接）");
            refreshDisplay.Items.Add(refreshSelected);
        }
        refreshDisplay.SelectedItem = refreshSelected;
        layout.Controls.Add(refreshDisplay);
        layout.Controls.Add(new Label { Text = "两档刷新率（保持当前分辨率）", AutoSize = true, Margin = new Padding(3, 10, 3, 4) });
        var ratesPanel = new FlowLayoutPanel { Width = 470, Height = 38 };
        var rateA = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 215 };
        var rateB = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 215 };
        ratesPanel.Controls.AddRange([rateA, rateB]);
        layout.Controls.Add(ratesPanel);
        void LoadRates(uint a, uint b)
        {
            rateA.Items.Clear(); rateB.Items.Clear();
            rateA.Items.Add(new DisplayOption("0", "自动：最低刷新率"));
            rateB.Items.Add(new DisplayOption("0", "自动：最高刷新率"));
            try
            {
                var target = RefreshRateService.Resolve(((DisplayOption)refreshDisplay.SelectedItem!).Path);
                foreach (var rate in RefreshRateService.Rates(target.Device))
                {
                    rateA.Items.Add(new DisplayOption(rate.ToString(), $"{rate} Hz"));
                    rateB.Items.Add(new DisplayOption(rate.ToString(), $"{rate} Hz"));
                }
            }
            catch { /* Keep saved rates available for disconnected displays. */ }
            foreach (var item in new[] { (Box: rateA, Rate: a), (Box: rateB, Rate: b) })
            {
                var option = item.Box.Items.Cast<DisplayOption>().FirstOrDefault(r => r.Path == item.Rate.ToString());
                if (option == null)
                {
                    option = new DisplayOption(item.Rate.ToString(), $"{item.Rate} Hz（当前不可用）");
                    item.Box.Items.Add(option);
                }
                item.Box.SelectedItem = option;
            }
        }
        LoadRates(current.RefreshRateA, current.RefreshRateB);
        refreshDisplay.SelectedIndexChanged += (_, _) => LoadRates(0, 0);
        layout.Controls.Add(new Label { Text = "Windows 保留组合或其他程序占用的快捷键无法保存。", AutoSize = true, Margin = new Padding(3, 12, 3, 8) });
        var button = new Button { Text = "保存", AutoSize = true, Margin = new Padding(3, 10, 3, 3) };
        button.Click += (_, _) =>
        {
            try
            {
                uint flags = 0;
                foreach (var item in checks) if (item.Box.Checked) flags |= item.Flag;
                if (flags == 0 || key.SelectedItem is not Keys chosen) throw new InvalidOperationException("请至少选择一个修饰键和一个字母、数字或功能键。");
                uint refreshFlags = 0;
                foreach (var item in refreshChecks) if (item.Box.Checked) refreshFlags |= item.Flag;
                if (refreshKey.SelectedItem is not Keys refreshChosen) throw new InvalidOperationException("请选择刷新率快捷键。");
                save(new Settings(flags, chosen, ((DisplayOption)display.SelectedItem!).Path,
                    refreshFlags, refreshChosen, ((DisplayOption)refreshDisplay.SelectedItem!).Path,
                    uint.Parse(((DisplayOption)rateA.SelectedItem!).Path), uint.Parse(((DisplayOption)rateB.SelectedItem!).Path)));
                Close();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        layout.Controls.Add(button);
        Controls.Add(layout);
        AcceptButton = button;
    }
}
