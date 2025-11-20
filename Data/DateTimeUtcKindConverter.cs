namespace ByProxy.Data {
    public class DateTimeUtcKindConverter : ValueConverter<DateTime, DateTime> {
        public DateTimeUtcKindConverter()
            : base(
                dt => dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime() : dt,
                dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc)) { }
    }
}
