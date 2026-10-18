using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Auto2Do;

internal sealed class MainForm : Form
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Auto2Do";

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LockWorkStation();

    private readonly TaskStore _store = new();
    private readonly List<ScheduledTask> _tasks;
    private readonly List<ScheduledTask> _persistedTasks = [];
    private readonly HashSet<string> _failedOccurrences = [];
    private readonly DataGridView _grid = new();
    private readonly ToolStripStatusLabel _status = new("就绪");
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 3000 };
    private readonly CheckBox _startAtLogin = new()
    {
        AutoSize = true,
        Text = "登录 Windows 时自动启动"
    };
    private bool _allowClose;

    public MainForm(bool startMinimized)
    {
        Text = "Auto2Do - 定时任务";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 480);
        Size = new Size(940, 590);
        Font = new Font("Microsoft YaHei UI", 9F);
        ShowInTaskbar = true;

        _tasks = _store.Load();
        _persistedTasks.AddRange(_tasks.Select(task => task.Copy()));
        BuildInterface();

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("打开 Auto2Do", null, (_, _) => ShowWindow());
        trayMenu.Items.Add("退出", null, (_, _) => ExitApplication());
        _notifyIcon = new NotifyIcon
        {
            Text = "Auto2Do - 定时任务运行中",
            Icon = SystemIcons.Application,
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowWindow();

        _timer.Tick += (_, _) => CheckSchedules();
        _timer.Start();
        FormClosing += OnFormClosing;
        Shown += (_, _) =>
        {
            if (startMinimized)
            {
                Hide();
                ShowTrayMessage("Auto2Do 已在后台运行。");
            }
        };
        RefreshTasks();
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(root);

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.Controls.Add(new Label
        {
            Text = "定时任务",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            Dock = DockStyle.Fill
        }, 0, 0);
        heading.Controls.Add(new Label
        {
            Text = "添加一次性、每日或每周任务。窗口关闭后程序会继续在系统托盘运行。",
            ForeColor = Color.DimGray,
            Dock = DockStyle.Fill
        }, 0, 1);
        root.Controls.Add(heading, 0, 0);

        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.BorderStyle = BorderStyle.Fixed3D;
        _grid.MultiSelect = false;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "任务名称", FillWeight = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Action", HeaderText = "操作", FillWeight = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Repeat", HeaderText = "重复", FillWeight = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "计划时间", FillWeight = 140 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Enabled", HeaderText = "状态", FillWeight = 65 });
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                EditSelectedTask();
            }
        };
        root.Controls.Add(_grid, 0, 1);

        var controls = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 12, 0, 0)
        };
        controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        controls.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight
        };
        buttons.Controls.Add(CreateButton("添加任务", (_, _) => AddTask()));
        buttons.Controls.Add(CreateButton("编辑", (_, _) => EditSelectedTask()));
        buttons.Controls.Add(CreateButton("启用 / 停用", (_, _) => ToggleSelectedTask()));
        buttons.Controls.Add(CreateButton("删除", (_, _) => DeleteSelectedTask()));
        buttons.Controls.Add(CreateButton("取消待执行的关机/重启", (_, _) => CancelPendingShutdown()));
        controls.Controls.Add(buttons, 0, 0);

        var preferences = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(2, 7, 0, 0) };
        preferences.Controls.Add(_startAtLogin);
        _startAtLogin.Checked = IsRegisteredForStartup();
        _startAtLogin.CheckedChanged += StartupCheckboxChanged;
        controls.Controls.Add(preferences, 0, 1);
        root.Controls.Add(controls, 0, 2);

        var footer = new StatusStrip { Dock = DockStyle.Fill, SizingGrip = false };
        footer.Items.Add(_status);
        root.Controls.Add(footer, 0, 3);

        var warning = new Label
        {
            Text = "提示：关机和重启会延迟 60 秒执行，可在倒计时期间取消；锁屏立即生效。请保存工作后再设置。",
            ForeColor = Color.Firebrick,
            AutoSize = true
        };
        preferences.Controls.Add(warning);
    }

    private static Button CreateButton(string text, EventHandler onClick)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 30, Margin = new Padding(0, 0, 8, 0) };
        button.Click += onClick;
        return button;
    }

    private void RefreshTasks()
    {
        _grid.Rows.Clear();
        foreach (var task in _tasks.OrderBy(t => t.ScheduledAt))
        {
            var repeat = task.Repeat switch
            {
                RepeatMode.Once => "一次",
                RepeatMode.Daily => "每天",
                RepeatMode.Weekly => $"每周{DayName(task.WeeklyDay)}",
                _ => "未知"
            };
            var time = task.Repeat == RepeatMode.Once
                ? task.ScheduledAt.ToString("yyyy-MM-dd HH:mm")
                : task.ScheduledAt.ToString("HH:mm");
            var rowIndex = _grid.Rows.Add(task.Name, ActionName(task.Action), repeat, time,
                task.IsEnabled ? "已启用" : "已停用");
            _grid.Rows[rowIndex].Tag = task;
        }
    }

    private ScheduledTask? SelectedTask => _grid.CurrentRow?.Tag as ScheduledTask;

    private void AddTask()
    {
        using var dialog = new TaskEditorForm();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result is not { } task)
        {
            return;
        }

        _tasks.Add(task);
        SaveTasks("任务已添加。");
    }

    private void EditSelectedTask()
    {
        var selected = SelectedTask;
        if (selected is null)
        {
            SetStatus("请先选择一个任务。");
            return;
        }

        using var dialog = new TaskEditorForm(selected);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result is not { } edited)
        {
            return;
        }

        selected.Name = edited.Name;
        selected.Action = edited.Action;
        selected.Repeat = edited.Repeat;
        selected.ScheduledAt = edited.ScheduledAt;
        selected.WeeklyDay = edited.WeeklyDay;
        selected.LastOccurrence = null;
        SaveTasks("任务已更新。");
    }

    private void ToggleSelectedTask()
    {
        var selected = SelectedTask;
        if (selected is null)
        {
            SetStatus("请先选择一个任务。");
            return;
        }

        selected.IsEnabled = !selected.IsEnabled;
        SaveTasks(selected.IsEnabled ? "任务已启用。" : "任务已停用。");
    }

    private void DeleteSelectedTask()
    {
        var selected = SelectedTask;
        if (selected is null)
        {
            SetStatus("请先选择一个任务。");
            return;
        }

        if (MessageBox.Show(this, $"确定删除任务“{selected.Name}”吗？", "删除任务",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        _tasks.Remove(selected);
        SaveTasks("任务已删除。");
    }

    private void CheckSchedules()
    {
        var now = DateTime.Now;
        foreach (var task in _tasks.Where(t => t.IsEnabled).ToList())
        {
            if (task.Repeat == RepeatMode.Once && now >= task.ScheduledAt.AddMinutes(1))
            {
                task.IsEnabled = false;
                var taskId = task.Id;
                if (!SaveTasks($"一次性任务“{task.Name}”已错过执行时间，已停用。"))
                {
                    var skippedTask = _tasks.FirstOrDefault(item => item.Id == taskId);
                    if (skippedTask is not null)
                    {
                        skippedTask.IsEnabled = false;
                        RefreshTasks();
                    }
                }

                continue;
            }

            var occurrence = task.GetOccurrence(now);
            if (occurrence is null || occurrence == task.LastOccurrence ||
                _failedOccurrences.Contains($"{task.Id}:{occurrence}"))
            {
                continue;
            }

            task.LastOccurrence = occurrence;
            if (task.Repeat == RepeatMode.Once)
            {
                task.IsEnabled = false;
            }

            try
            {
                PersistTasks();
            }
            catch (Exception ex)
            {
                _failedOccurrences.Add($"{task.Id}:{occurrence}");
                RestorePersistedTasks();
                SetStatus($"无法保存任务“{task.Name}”，未执行：{ex.Message}");
                ShowError($"无法保存任务“{task.Name}”，因此未执行该操作。\n\n{ex.Message}");
                continue;
            }

            try
            {
                ExecuteTask(task);
                SetStatus($"已触发任务“{task.Name}”。");
                ShowTrayMessage($"已触发任务：{task.Name}");
            }
            catch (Exception ex)
            {
                SetStatus($"执行任务“{task.Name}”失败：{ex.Message}");
                ShowError($"无法执行任务“{task.Name}”。\n\n{ex.Message}");
            }

            RefreshTasks();
        }
    }

    private static void ExecuteTask(ScheduledTask task)
    {
        if (task.Action == ScheduledAction.Lock)
        {
            if (!LockWorkStation())
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            return;
        }

        var arguments = task.Action switch
        {
            ScheduledAction.Shutdown => "/s /t 60",
            ScheduledAction.Restart => "/r /t 60",
            _ => throw new InvalidOperationException("不支持的任务操作。")
        };
        using var process = Process.Start(new ProcessStartInfo("shutdown.exe", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("无法启动 Windows 关机程序。");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Windows 关机程序返回错误代码 {process.ExitCode}。");
        }
    }

    private void CancelPendingShutdown()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("shutdown.exe", "/a")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("无法启动 Windows 关机程序。");
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                SetStatus("当前没有可取消的关机或重启，或取消操作失败。");
                return;
            }

            SetStatus("已取消待执行的关机或重启。");
        }
        catch (Exception ex)
        {
            ShowError($"取消操作失败。\n\n{ex.Message}");
        }
    }

    private bool SaveTasks(string successMessage)
    {
        try
        {
            PersistTasks();
            RefreshTasks();
            SetStatus(successMessage);
            return true;
        }
        catch (Exception ex)
        {
            RestorePersistedTasks();
            ShowError($"无法保存任务。\n\n{ex.Message}");
            SetStatus("保存失败；请检查任务文件和磁盘权限。");
            return false;
        }
    }

    private void PersistTasks()
    {
        _store.Save(_tasks);
        _persistedTasks.Clear();
        _persistedTasks.AddRange(_tasks.Select(task => task.Copy()));
    }

    private void RestorePersistedTasks()
    {
        _tasks.Clear();
        _tasks.AddRange(_persistedTasks.Select(task => task.Copy()));
        RefreshTasks();
    }

    private bool IsRegisteredForStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        var command = key?.GetValue(RunValueName) as string;
        return string.Equals(command, StartupCommand(), StringComparison.OrdinalIgnoreCase);
    }

    private void SetStartupRegistration(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException("无法打开 Windows 登录启动注册表项。");
            if (enabled)
            {
                key.SetValue(RunValueName, StartupCommand());
                SetStatus("已设置为登录 Windows 时启动。");
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
                SetStatus("已取消登录时自动启动。");
            }
        }
        catch (Exception ex)
        {
            _startAtLogin.CheckedChanged -= StartupCheckboxChanged;
            _startAtLogin.Checked = !enabled;
            _startAtLogin.CheckedChanged += StartupCheckboxChanged;
            ShowError($"无法更改登录启动设置。\n\n{ex.Message}");
        }
    }

    private void StartupCheckboxChanged(object? sender, EventArgs e) =>
        SetStartupRegistration(_startAtLogin.Checked);

    private static string StartupCommand() => $"\"{Application.ExecutablePath}\" --minimized";

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        ShowTrayMessage("Auto2Do 已最小化到系统托盘，定时任务仍会运行。");
    }

    private void ShowWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _allowClose = true;
        _timer.Stop();
        _timer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Close();
    }

    private void ShowTrayMessage(string message) =>
        _notifyIcon.ShowBalloonTip(3000, "Auto2Do", message, ToolTipIcon.Info);

    private void SetStatus(string message) => _status.Text = message;

    private void ShowError(string message) =>
        MessageBox.Show(this, message, "Auto2Do - 错误", MessageBoxButtons.OK, MessageBoxIcon.Error);

    private static string ActionName(ScheduledAction action) => action switch
    {
        ScheduledAction.Shutdown => "关机",
        ScheduledAction.Restart => "重启",
        ScheduledAction.Lock => "锁屏",
        _ => "未知"
    };

    private static string DayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "一",
        DayOfWeek.Tuesday => "二",
        DayOfWeek.Wednesday => "三",
        DayOfWeek.Thursday => "四",
        DayOfWeek.Friday => "五",
        DayOfWeek.Saturday => "六",
        DayOfWeek.Sunday => "日",
        _ => "?"
    };
}
