using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LibraryCare;

/// <summary>
/// Report of a completed library job (<c>LibraryJob.report</c>, backend-api 1.12.0, feature 013): one of
/// <see cref="BackupReportDto"/>, <see cref="VerifyReportDto"/> or <see cref="RestoreReportDto"/>, told apart by the JSON
/// member <c>type</c> (<c>backup</c>, <c>verify</c>, <c>restore</c>), which is written first.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(BackupReportDto), "backup")]
[JsonDerivedType(typeof(VerifyReportDto), "verify")]
[JsonDerivedType(typeof(RestoreReportDto), "restore")]
public abstract record LibraryJobReportDto;
