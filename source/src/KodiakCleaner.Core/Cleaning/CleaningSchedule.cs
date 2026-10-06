using System.Globalization;
using System.Security;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.Core.Cleaning;

public enum ScheduleRepeat
{
    Weekly,
    Monthly,
}

/// <summary>
/// A named cleaning that Windows Task Scheduler starts on chosen days (Ted, 0.8.21): a set of Cleaner items, weekly on some
/// days or monthly on one date, at a time of day. Kept in KodiakCleaner.ini as a [Schedule id] section. Kodiak Cleaner runs
/// only when the task starts it, so there is still nothing running in the background.
/// </summary>
public sealed class CleaningSchedule
{
    public const string SectionPrefix = "Schedule ";
    /// <summary>The DayOfMonth value that means the last day of every month.</summary>
    public const int LastDay = 0;

    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public List<string> Targets { get; set; } = new();
    public ScheduleRepeat Repeat { get; set; } = ScheduleRepeat.Weekly;
    public List<DayOfWeek> Days { get; set; } = new();
    /// <summary>1 to 28, or <see cref="LastDay"/>.</summary>
    public int DayOfMonth { get; set; } = 1;
    public TimeSpan Time { get; set; } = new(9, 0, 0);
    /// <summary>Runs with administrator rights (only offered for the installed copy, by an administrator).</summary>
    public bool AsAdmin { get; set; }
    public DateTime? LastRunUtc { get; set; }
    public string LastResult { get; set; } = "";
    /// <summary>The exe the task was made for; a different one at startup means the task is made again.</summary>
    public string RegisteredFor { get; set; } = "";

    /// <summary>Monday first, the way the days are shown.</summary>
    public static readonly DayOfWeek[] Week =
        { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

    /// <summary>What is missing before it can be saved, or null when it is ready.</summary>
    public string? Problem() =>
        Name.Trim().Length == 0 ? "Give the schedule a name."
        : Targets.Count == 0 ? "Tick at least one item to clean."
        : Repeat == ScheduleRepeat.Weekly && Days.Count == 0 ? "Pick at least one day of the week."
        : Repeat == ScheduleRepeat.Monthly && DayOfMonth is < LastDay or > 28 ? "Pick a day of the month."
        : null;

    public static string TimeText(TimeSpan t) => DateTime.Today.Add(t).ToString("h:mm tt", CultureInfo.InvariantCulture);

    public static string DayOfMonthText(int day) => day == LastDay ? "Last Day" : Ordinal(day);

    private static string Ordinal(int n) => n + (n % 100 is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });

    /// <summary>"Every Monday and Friday at 9:00 AM", "Every day at 9:00 AM", "Monthly on the 1st at 9:00 AM".</summary>
    public string Describe()
    {
        var at = " at " + TimeText(Time);
        if (Repeat == ScheduleRepeat.Monthly)
            return (DayOfMonth == LastDay ? "Monthly on the last day" : $"Monthly on the {Ordinal(DayOfMonth)}") + at;
        var days = Week.Where(Days.Contains).ToList();
        if (days.Count == 7) return "Every day" + at;
        var names = days.Select(d => d.ToString()).ToList();
        return "Every " + (names.Count <= 1 ? string.Concat(names) : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1]) + at;
    }

    /// <summary>The next time it is due after <paramref name="now"/> (local time).</summary>
    public DateTime? NextRun(DateTime now)
    {
        if (Repeat == ScheduleRepeat.Weekly && Days.Count == 0) return null;
        for (var day = now.Date; day < now.Date.AddDays(400); day = day.AddDays(1))
        {
            var at = day + Time;
            if (at <= now) continue;
            bool due = Repeat == ScheduleRepeat.Weekly
                ? Days.Contains(day.DayOfWeek)
                : DayOfMonth == LastDay ? day.AddDays(1).Month != day.Month : day.Day == DayOfMonth;
            if (due) return at;
        }
        return null;
    }

    public static IEnumerable<CleaningSchedule> ReadAll(IniFile ini) => ini.SectionNames
        .Where(n => n.StartsWith(SectionPrefix, StringComparison.OrdinalIgnoreCase))
        .Select(n => Read(ini, n))
        .Where(s => s is not null)
        .Select(s => s!)
        .ToList();

    private static CleaningSchedule? Read(IniFile ini, string section)
    {
        var id = section[SectionPrefix.Length..].Trim();
        if (id.Length == 0 || !id.All(char.IsLetterOrDigit)) return null;
        var s = new CleaningSchedule
        {
            Id = id,
            Name = ini.GetString(section, "Name", ""),
            Targets = Split(ini.Get(section, "Items")).ToList(),
            Repeat = Enum.TryParse<ScheduleRepeat>(ini.Get(section, "Repeat"), true, out var r) ? r : ScheduleRepeat.Weekly,
            Days = Split(ini.Get(section, "Days")).Select(d => Enum.TryParse<DayOfWeek>(d, true, out var w) ? (DayOfWeek?)w : null)
                .Where(d => d is not null).Select(d => d!.Value).Distinct().ToList(),
            DayOfMonth = Math.Clamp(ini.GetInt(section, "DayOfMonth", 1), LastDay, 28),
            Time = TimeSpan.TryParseExact(ini.Get(section, "Time"), @"hh\:mm", CultureInfo.InvariantCulture, out var t) && t < TimeSpan.FromDays(1)
                ? t : new TimeSpan(9, 0, 0),
            AsAdmin = ini.GetBool(section, "AsAdmin", false),
            LastResult = ini.GetString(section, "LastResult", ""),
            RegisteredFor = ini.GetString(section, "RegisteredFor", ""),
        };
        if (DateTime.TryParse(ini.Get(section, "LastRunUtc"), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var last))
            s.LastRunUtc = last;
        return s;
    }

    public void Write(IniFile ini)
    {
        var section = SectionPrefix + Id;
        ini.Set(section, "Name", Name.Trim());
        ini.Set(section, "Items", string.Join(",", Targets));
        ini.Set(section, "Repeat", Repeat.ToString());
        ini.Set(section, "Days", string.Join(",", Week.Where(Days.Contains).Select(d => d.ToString())));
        ini.Set(section, "DayOfMonth", DayOfMonth);
        ini.Set(section, "Time", Time.ToString(@"hh\:mm", CultureInfo.InvariantCulture));
        ini.Set(section, "AsAdmin", AsAdmin);
        ini.Set(section, "LastRunUtc", LastRunUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) ?? "");
        ini.Set(section, "LastResult", LastResult);
        ini.Set(section, "RegisteredFor", RegisteredFor);
    }

    private static IEnumerable<string> Split(string? text) =>
        (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>The Task Scheduler definition that starts <paramref name="exe"/> with <paramref name="arguments"/> when it is due.</summary>
    public string TaskXml(string exe, string arguments, string user, string description, DateTime now)
    {
        var start = (now.Date + Time).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        string schedule = Repeat == ScheduleRepeat.Weekly
            ? "<ScheduleByWeek><WeeksInterval>1</WeeksInterval><DaysOfWeek>" +
              string.Concat(Week.Where(Days.Contains).Select(d => $"<{d} />")) + "</DaysOfWeek></ScheduleByWeek>"
            : "<ScheduleByMonth><DaysOfMonth><Day>" + (DayOfMonth == LastDay ? "Last" : DayOfMonth.ToString(CultureInfo.InvariantCulture)) +
              "</Day></DaysOfMonth><Months>" + string.Concat(CultureInfo.InvariantCulture.DateTimeFormat.MonthNames.Where(m => m.Length > 0).Select(m => $"<{m} />")) +
              "</Months></ScheduleByMonth>";
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo><Description>{SecurityElement.Escape(description)}</Description></RegistrationInfo>
              <Triggers>
                <CalendarTrigger><StartBoundary>{start}</StartBoundary><Enabled>true</Enabled>{schedule}</CalendarTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author"><UserId>{SecurityElement.Escape(user)}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>{(AsAdmin ? "HighestAvailable" : "LeastPrivilege")}</RunLevel></Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <StartWhenAvailable>true</StartWhenAvailable>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <ExecutionTimeLimit>PT2H</ExecutionTimeLimit>
                <Priority>7</Priority>
              </Settings>
              <Actions Context="Author"><Exec><Command>{SecurityElement.Escape(exe)}</Command><Arguments>{SecurityElement.Escape(arguments)}</Arguments></Exec></Actions>
            </Task>
            """;
    }
}
