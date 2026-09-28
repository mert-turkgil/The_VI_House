namespace VIHouse.Business.Concrete;

public static class UtcDates
{
    /// <summary>A DateTime that is UTC by contract (an admin form's datetime-local, a provider
    /// timestamp) as a DateTimeOffset at +00:00, whatever Kind it arrived with.</summary>
    public static DateTimeOffset? ToOffset(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
}
