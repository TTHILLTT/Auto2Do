using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Auto2Do;

internal static class CliProgram
{
    private const int PollIntervalMilliseconds = 3000;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LockWorkStation();

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintHelp();
                return 0;
            }

            var store = new TaskStore();
            switch (args[0].ToLowerInvariant())
            {
                case "list":
                    RequireArgumentCount(args, 1);
                    ListTasks(store);
                    return 0;
                case "about":
                    RequireArgumentCount(args, 1);
                    AboutInfo.WriteTo(Console.Out);
                    return 0;
                case "add":
                    AddTask(store, ParseOptions(args, 1));
                    return 0;
                case "remove":
                    ChangeTask(store, args, "remove");
                    return 0;
                case "enable":
                    ChangeTask(store, args, "enable");
                    return 0;
                case "disable":
                    ChangeTask(store, args, "disable");
                    return 0;
                case "cancel":
                    RequireArgumentCount(args, 1);
                    CancelPendingShutdown();
                    return 0;
                case "daemon":
                case "run":
                    RequireArgumentCount(args, 1);
                    RunDaemon(store);
                    return 0;
                case "help":
                case "--help":
                case "-h":
                case "/?":
                    PrintHelp();
                    return 0;
                default:
                    throw new ArgumentException($"未知命令：{args[0]}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"错误：{ex.Message}");
            Console.Error.WriteLine("运行 Auto2Do.Cli.exe help 查看用法。");
            return 1;
        }
    }

    private static bool IsHelp(string value) =>
        value.Equals("help", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("/?", StringComparison.OrdinalIgnoreCase);

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            Auto2Do 命令行版

            用法：
              Auto2Do.Cli.exe list
              Auto2Do.Cli.exe about
              Auto2Do.Cli.exe add --name <名称> --action <shutdown|restart|lock> --repeat <once|daily|weekly> --at <时间> [--day <星期>]
              Auto2Do.Cli.exe remove <任务ID前缀>
              Auto2Do.Cli.exe enable <任务ID前缀>
              Auto2Do.Cli.exe disable <任务ID前缀>
              Auto2Do.Cli.exe cancel
              Auto2Do.Cli.exe daemon

            时间格式：
              once    yyyy-MM-dd HH:mm
              daily   HH:mm
              weekly  HH:mm，并使用 --day mon|tue|wed|thu|fri|sat|sun

            示例：
              Auto2Do.Cli.exe add --name "夜间关机" --action shutdown --repeat daily --at 23:30
              Auto2Do.Cli.exe add --name "周末锁屏" --action lock --repeat weekly --at 18:00 --day sat
              Auto2Do.Cli.exe add --name "重启电脑" --action restart --repeat once --at "2026-12-31 22:00"
              Auto2Do.Cli.exe daemon

            关机/重启延迟 60 秒；daemon 必须保持运行。按 Ctrl+C 安全退出。
            任务数据与图形版共享，保存在 %LOCALAPPDATA%\Auto2Do\tasks.json。
            """);
    }

    private static void AddTask(TaskStore store, Dictionary<string, string> options)
    {
        EnsureAllowedOptions(options, "name", "action", "repeat", "at", "day");
        var name = GetRequiredOption(options, "name").Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80)
        {
            throw new ArgumentException("任务名称不能为空，且不能超过 80 个字符。");
        }

        var action = ParseAction(GetRequiredOption(options, "action"));
        var repeat = ParseRepeat(GetRequiredOption(options, "repeat"));
        var at = GetRequiredOption(options, "at");
        var scheduledAt = ParseScheduledAt(repeat, at);
        if (repeat == RepeatMode.Weekly && !options.ContainsKey("day"))
        {
            throw new ArgumentException("每周任务必须指定 --day。");
        }

        if (repeat != RepeatMode.Weekly && options.ContainsKey("day"))
        {
            throw new ArgumentException("--day 仅适用于每周任务。");
        }

        var weeklyDay = repeat == RepeatMode.Weekly
            ? ParseDayOfWeek(GetRequiredOption(options, "day"))
            : DayOfWeek.Monday;

        if (repeat == RepeatMode.Once && scheduledAt <= DateTime.Now)
        {
            throw new ArgumentException("一次性任务的执行时间必须晚于当前时间。");
        }

        var task = new ScheduledTask
        {
            Name = name,
            Action = action,
            Repeat = repeat,
            ScheduledAt = scheduledAt,
            WeeklyDay = weeklyDay,
            IsEnabled = true
        };
        var tasks = store.Load();
        tasks.Add(task);
        store.Save(tasks);

        Console.WriteLine($"任务已添加：{task.Name}");
        Console.WriteLine($"任务 ID：{FormatId(task.Id)}");
        Console.WriteLine($"操作：{ActionName(task.Action)}；计划：{ScheduleName(task)}");
    }

    private static DateTime ParseScheduledAt(RepeatMode repeat, string value)
    {
        if (repeat == RepeatMode.Once)
        {
            var formats = new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-dd'T'HH:mm" };
            if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var oneTime))
            {
                throw new ArgumentException("--repeat once 的 --at 格式必须为 yyyy-MM-dd HH:mm。");
            }

            return DateTime.SpecifyKind(oneTime, DateTimeKind.Local);
        }

        if (!DateTime.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var time))
        {
            throw new ArgumentException("--repeat daily/weekly 的 --at 格式必须为 HH:mm。");
        }

        return DateTime.Today.Add(time.TimeOfDay);
    }

    private static ScheduledAction ParseAction(string value) => value.ToLowerInvariant() switch
    {
        "shutdown" => ScheduledAction.Shutdown,
        "restart" => ScheduledAction.Restart,
        "lock" => ScheduledAction.Lock,
        _ => throw new ArgumentException("--action 只能是 shutdown、restart 或 lock。")
    };

    private static RepeatMode ParseRepeat(string value) => value.ToLowerInvariant() switch
    {
        "once" => RepeatMode.Once,
        "daily" => RepeatMode.Daily,
        "weekly" => RepeatMode.Weekly,
        _ => throw new ArgumentException("--repeat 只能是 once、daily 或 weekly。")
    };

    private static DayOfWeek ParseDayOfWeek(string value) => value.ToLowerInvariant() switch
    {
        "mon" or "monday" or "周一" or "星期一" => DayOfWeek.Monday,
        "tue" or "tues" or "tuesday" or "周二" or "星期二" => DayOfWeek.Tuesday,
        "wed" or "wednesday" or "周三" or "星期三" => DayOfWeek.Wednesday,
        "thu" or "thur" or "thurs" or "thursday" or "周四" or "星期四" => DayOfWeek.Thursday,
        "fri" or "friday" or "周五" or "星期五" => DayOfWeek.Friday,
        "sat" or "saturday" or "周六" or "星期六" => DayOfWeek.Saturday,
        "sun" or "sunday" or "周日" or "周天" or "星期日" or "星期天" => DayOfWeek.Sunday,
        _ => throw new ArgumentException("--day 必须是 mon、tue、wed、thu、fri、sat 或 sun。")
    };

    private static Dictionary<string, string> ParseOptions(string[] args, int startIndex)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = startIndex; i < args.Length; i++)
        {
            var key = args[i];
            if (!key.StartsWith("--", StringComparison.Ordinal) || key.Length == 2)
            {
                throw new ArgumentException($"无效参数：{key}");
            }

            key = key.Substring(2);
            if (options.ContainsKey(key))
            {
                throw new ArgumentException($"参数 --{key} 重复。");
            }

            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"参数 --{key} 缺少值。");
            }

            options.Add(key, args[++i]);
        }

        return options;
    }

    private static string GetRequiredOption(Dictionary<string, string> options, string name) =>
        options.TryGetValue(name, out var value)
            ? value
            : throw new ArgumentException($"缺少必需参数 --{name}。");

    private static void EnsureAllowedOptions(Dictionary<string, string> options, params string[] allowed)
    {
        var allowedSet = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
        var unknown = options.Keys.FirstOrDefault(key => !allowedSet.Contains(key));
        if (unknown is not null)
        {
            throw new ArgumentException($"未知参数：--{unknown}");
        }
    }

    private static void ListTasks(TaskStore store)
    {
        var tasks = store.Load().OrderBy(task => task.ScheduledAt).ToList();
        if (tasks.Count == 0)
        {
            Console.WriteLine("目前没有任务。");
            return;
        }

        Console.WriteLine("ID       状态   操作   重复       时间               名称");
        foreach (var task in tasks)
        {
            Console.WriteLine(
                $"{FormatId(task.Id),-8} {(task.IsEnabled ? "启用" : "停用"),-4} " +
                $"{ActionName(task.Action),-4} {RepeatName(task),-10} " +
                $"{TaskTime(task),-18} {task.Name}");
        }
    }

    private static void ChangeTask(TaskStore store, string[] args, string command)
    {
        RequireArgumentCount(args, 2);
        var tasks = store.Load();
        var task = FindTask(tasks, args[1]);

        if (command == "remove")
        {
            tasks.Remove(task);
            store.Save(tasks);
            Console.WriteLine($"已删除任务：{task.Name}");
            return;
        }

        task.IsEnabled = command == "enable";
        store.Save(tasks);
        Console.WriteLine($"任务“{task.Name}”已{(task.IsEnabled ? "启用" : "停用")}。");
    }

    private static ScheduledTask FindTask(List<ScheduledTask> tasks, string prefix)
    {
        if (prefix.Length < 4 || prefix.Length > 32 ||
            prefix.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("请输入至少 4 位的十六进制任务 ID 前缀。");
        }

        var matches = tasks.Where(task =>
            task.Id.ToString("N").StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new ArgumentException($"找不到任务 ID：{prefix}"),
            _ => throw new ArgumentException($"任务 ID 前缀“{prefix}”不唯一，请使用更长的前缀。")
        };
    }

    private static void RunDaemon(TaskStore store)
    {
        var stopping = false;
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stopping = true;
        };

        Console.CancelKeyPress += cancelHandler;
        try
        {
            Console.WriteLine($"Auto2Do 调度器运行中。任务文件：{store.FilePath}");
            Console.WriteLine("按 Ctrl+C 退出。关机/重启任务会延迟 60 秒执行。");
            while (!stopping)
            {
                try
                {
                    CheckSchedules(store);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 读取任务失败：{ex.Message}");
                }

                if (!stopping)
                {
                    Thread.Sleep(PollIntervalMilliseconds);
                }
            }

            Console.WriteLine("调度器已退出。");
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static void CheckSchedules(TaskStore store)
    {
        var now = DateTime.Now;
        var tasks = store.Load();
        var changed = false;

        foreach (var task in tasks.Where(item => item.IsEnabled))
        {
            if (task.Repeat == RepeatMode.Once && now >= task.ScheduledAt.AddMinutes(1))
            {
                task.IsEnabled = false;
                changed = true;
                Console.WriteLine($"[{now:yyyy-MM-dd HH:mm:ss}] 一次性任务“{task.Name}”已错过执行时间，已停用。");
                continue;
            }

            var occurrence = task.GetOccurrence(now);
            if (occurrence is null || occurrence == task.LastOccurrence)
            {
                continue;
            }

            var previousLastOccurrence = task.LastOccurrence;
            var wasEnabled = task.IsEnabled;
            task.LastOccurrence = occurrence;
            if (task.Repeat == RepeatMode.Once)
            {
                task.IsEnabled = false;
            }

            try
            {
                store.Save(tasks);
                changed = false;
            }
            catch
            {
                task.LastOccurrence = previousLastOccurrence;
                task.IsEnabled = wasEnabled;
                throw;
            }

            try
            {
                ExecuteTask(task);
                Console.WriteLine($"[{now:yyyy-MM-dd HH:mm:ss}] 已触发任务“{task.Name}”：{ActionName(task.Action)}。");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{now:yyyy-MM-dd HH:mm:ss}] 执行任务“{task.Name}”失败：{ex.Message}");
            }

            return;
        }

        if (changed)
        {
            store.Save(tasks);
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

    private static void CancelPendingShutdown()
    {
        using var process = Process.Start(new ProcessStartInfo("shutdown.exe", "/a")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("无法启动 Windows 关机程序。");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("当前没有可取消的关机/重启，或取消操作失败。");
        }

        Console.WriteLine("已取消待执行的关机或重启。");
    }

    private static void RequireArgumentCount(string[] args, int expected)
    {
        if (args.Length != expected)
        {
            throw new ArgumentException($"命令参数数量不正确：{args[0]}。");
        }
    }

    private static string FormatId(Guid id) => id.ToString("N").Substring(0, 8);

    private static string ActionName(ScheduledAction action) => action switch
    {
        ScheduledAction.Shutdown => "关机",
        ScheduledAction.Restart => "重启",
        ScheduledAction.Lock => "锁屏",
        _ => "未知"
    };

    private static string RepeatName(ScheduledTask task) => task.Repeat switch
    {
        RepeatMode.Once => "一次",
        RepeatMode.Daily => "每天",
        RepeatMode.Weekly => $"每周{DayName(task.WeeklyDay)}",
        _ => "未知"
    };

    private static string ScheduleName(ScheduledTask task) =>
        $"{RepeatName(task)} {TaskTime(task)}";

    private static string TaskTime(ScheduledTask task) => task.Repeat == RepeatMode.Once
        ? task.ScheduledAt.ToString("yyyy-MM-dd HH:mm")
        : task.ScheduledAt.ToString("HH:mm");

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
