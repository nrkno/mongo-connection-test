namespace MongoConnectionTester.Events;

public static class Utc
{
    private static Func<DateTimeOffset> _utcNow;

    public static DateTimeOffset Now => _utcNow();
    public static DateTimeOffset Epoch => new DateTimeOffset(1970, 01, 01, 0, 0, 0, TimeSpan.Zero);
    public static bool IsHacked { get; private set; }

    static Utc()
    {
        Reset();
    }

    public static void Reset()
    {
        IsHacked = false;
        _utcNow = () => DateTimeOffset.UtcNow;
    }

    public static void SetNow(DateTimeOffset now)
    {
        IsHacked = true;
        _utcNow = () => now;
    }

    public static void SetNow(Func<DateTimeOffset> now)
    {
        IsHacked = true;
        _utcNow = now;
    }

    public static DateTimeOffset GetMondayThisWeek()
    {
        var candidate = Now;
        while (candidate.DayOfWeek != DayOfWeek.Monday)
        {
            candidate = candidate.Subtract(TimeSpan.FromHours(24));
        }
        return candidate.Date;
    }

    public static DateTimeOffset FromEpochTime(long epochTime)
    {
        return Epoch.AddSeconds(epochTime);
    }

    public static long SecondsSinceEpoch(this DateTimeOffset offset)
    {
        return (long)(offset - Epoch).TotalSeconds;
    }

    public static long ToEpochTime(this DateTimeOffset offset)
    {
        return (offset.ToUniversalTime().Ticks - Epoch.Ticks) / TimeSpan.TicksPerSecond;
    }

    public static int CalculateAge(DateTimeOffset input)
    {
        var birthday = input.Date;
        var today = Utc.Now.Date;
        if (birthday > today) return 0; //min
        if (birthday < today.AddYears(-120)) return 120; //max
        var age = today.Year - birthday.Year;
        if (birthday.Date > today.AddYears(-age)) age--; //leap year handling
        return age;
    }
    public static int? CalculateAge(DateTimeOffset? input)
    {
        if (input.HasValue)
        {
            return CalculateAge(input.Value);
        }

        return null;
    }
}