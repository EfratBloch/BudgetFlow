namespace YouthGroups.Service.Common;

internal static class Dates
{
    // Npgsql only accepts UTC DateTimes for timestamptz columns. A date sent without
    // an offset ("2026-09-01T16:00") arrives with Kind=Unspecified, so treat it as UTC.
    public static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
