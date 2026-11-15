using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace StemStudio.Persistence.Configurations;

/// <summary>
/// Stores timestamps as UTC <see cref="DateTime"/> and marks values read back as UTC. SQLite cannot order
/// <see cref="DateTimeOffset"/> columns (data-model.md, SQLite note), but ISO text of UTC DateTimes sorts correctly.
/// </summary>
public sealed class UtcDateTimeConverter()
    : ValueConverter<DateTime, DateTime>(value => value.ToUniversalTime(), value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
