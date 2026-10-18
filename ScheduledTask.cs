namespace Auto2Do;

internal enum ScheduledAction
{
    Shutdown,
    Restart,
    Lock
}

internal enum RepeatMode
{
    Once,
    Daily,
    Weekly
}

internal sealed class ScheduledTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public ScheduledAction Action { get; set; }
    public RepeatMode Repeat { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DayOfWeek WeeklyDay { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? LastOccurrence { get; set; }

    public ScheduledTask Copy() => new()
    {
        Id = Id,
        Name = Name,
        Action = Action,
        Repeat = Repeat,
        ScheduledAt = ScheduledAt,
        WeeklyDay = WeeklyDay,
        IsEnabled = IsEnabled,
        LastOccurrence = LastOccurrence
    };

    public string? GetOccurrence(DateTime now)
    {
        DateTime due;
        switch (Repeat)
        {
            case RepeatMode.Once:
                due = ScheduledAt;
                break;
            case RepeatMode.Daily:
                due = now.Date.Add(ScheduledAt.TimeOfDay);
                break;
            case RepeatMode.Weekly:
                if (now.DayOfWeek != WeeklyDay)
                {
                    return null;
                }

                due = now.Date.Add(ScheduledAt.TimeOfDay);
                break;
            default:
                return null;
        }

        if (now < due || now >= due.AddMinutes(1))
        {
            return null;
        }

        return due.ToString("yyyyMMddHHmm");
    }
}
