namespace Auto2Do;

internal sealed class TaskEditorForm : Form
{
    private readonly TextBox _name = new() { Dock = DockStyle.Fill, MaxLength = 80 };
    private readonly ComboBox _action = CreateComboBox();
    private readonly ComboBox _repeat = CreateComboBox();
    private readonly DateTimePicker _date = new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "yyyy-MM-dd",
        ShowUpDown = false
    };
    private readonly DateTimePicker _time = new()
    {
        Format = DateTimePickerFormat.Time,
        ShowUpDown = true
    };
    private readonly ComboBox _weekday = CreateComboBox();

    public ScheduledTask? Result { get; private set; }

    public TaskEditorForm(ScheduledTask? existing = null)
    {
        Text = existing is null ? "添加定时任务" : "编辑定时任务";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(460, 330);
        Font = new Font("Microsoft YaHei UI", 9F);

        _action.Items.AddRange(["关机（延迟 60 秒）", "重启（延迟 60 秒）", "锁屏"]);
        _repeat.Items.AddRange(["一次", "每天", "每周"]);
        _weekday.Items.AddRange(["星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日"]);
        _weekday.SelectedIndex = 0;
        _action.SelectedIndex = 0;
        _repeat.SelectedIndex = 0;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(18)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 6; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        AddField(layout, 0, "任务名称", _name);
        AddField(layout, 1, "执行操作", _action);
        AddField(layout, 2, "重复方式", _repeat);
        AddField(layout, 3, "执行日期", _date);
        AddField(layout, 4, "执行时间", _time);
        AddField(layout, 5, "每周星期", _weekday);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var save = new Button { Text = "保存", AutoSize = true };
        save.Click += (_, _) => SaveResult();
        var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        layout.Controls.Add(buttons, 0, 6);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);

        _repeat.SelectedIndexChanged += (_, _) => UpdateDateVisibility();
        AcceptButton = save;
        CancelButton = cancel;

        if (existing is not null)
        {
            _name.Text = existing.Name;
            _action.SelectedIndex = existing.Action switch
            {
                ScheduledAction.Shutdown => 0,
                ScheduledAction.Restart => 1,
                _ => 2
            };
            _repeat.SelectedIndex = existing.Repeat switch
            {
                RepeatMode.Once => 0,
                RepeatMode.Daily => 1,
                _ => 2
            };
            _date.Value = existing.ScheduledAt;
            _time.Value = DateTime.Today.Add(existing.ScheduledAt.TimeOfDay);
            _weekday.SelectedIndex = DayIndex(existing.WeeklyDay);
        }
        else
        {
            var defaultScheduledAt = DateTime.Now.AddMinutes(2);
            _date.Value = defaultScheduledAt;
            _time.Value = defaultScheduledAt;
        }

        UpdateDateVisibility();
    }

    private static ComboBox CreateComboBox() => new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList
    };

    private static void AddField(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.Controls.Add(new Label
        {
            Text = label,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill
        }, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private void UpdateDateVisibility()
    {
        var isOnce = _repeat.SelectedIndex == 0;
        _date.Enabled = isOnce;
        _weekday.Enabled = _repeat.SelectedIndex == 2;
    }

    private void SaveResult()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            MessageBox.Show(this, "请输入任务名称。", "输入不完整", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _name.Focus();
            return;
        }

        var repeat = _repeat.SelectedIndex switch
        {
            0 => RepeatMode.Once,
            1 => RepeatMode.Daily,
            2 => RepeatMode.Weekly,
            _ => throw new InvalidOperationException("请选择重复方式。")
        };
        var scheduledAt = _date.Value.Date.Add(_time.Value.TimeOfDay);
        if (repeat == RepeatMode.Once && scheduledAt <= DateTime.Now)
        {
            MessageBox.Show(this, "一次性任务的执行时间必须晚于当前时间。", "时间无效",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Result = new ScheduledTask
        {
            Name = _name.Text.Trim(),
            Action = _action.SelectedIndex switch
            {
                0 => ScheduledAction.Shutdown,
                1 => ScheduledAction.Restart,
                _ => ScheduledAction.Lock
            },
            Repeat = repeat,
            ScheduledAt = scheduledAt,
            WeeklyDay = IndexDay(_weekday.SelectedIndex),
            IsEnabled = true
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private static int DayIndex(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => 0,
        DayOfWeek.Tuesday => 1,
        DayOfWeek.Wednesday => 2,
        DayOfWeek.Thursday => 3,
        DayOfWeek.Friday => 4,
        DayOfWeek.Saturday => 5,
        DayOfWeek.Sunday => 6,
        _ => 0
    };

    private static DayOfWeek IndexDay(int index) => index switch
    {
        0 => DayOfWeek.Monday,
        1 => DayOfWeek.Tuesday,
        2 => DayOfWeek.Wednesday,
        3 => DayOfWeek.Thursday,
        4 => DayOfWeek.Friday,
        5 => DayOfWeek.Saturday,
        6 => DayOfWeek.Sunday,
        _ => DayOfWeek.Monday
    };
}
