using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Recording;

namespace StemStudio.Persistence;

/// <summary>Play-along recording (feature 015): takes and input-latency calibrations.</summary>
public sealed partial class StemStudioDbContext
{
    /// <summary>Recorded takes (feature 015).</summary>
    public DbSet<RecordingTake> RecordingTakes => Set<RecordingTake>();

    /// <summary>Input-latency calibrations per device (feature 015).</summary>
    public DbSet<InputCalibration> InputCalibrations => Set<InputCalibration>();
}
