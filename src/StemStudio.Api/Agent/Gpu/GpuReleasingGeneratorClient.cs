using StemStudio.Api.Generator;

namespace StemStudio.Api.Agent.Gpu;

/// <summary>
/// Decorates the generator client (feature 011, T078): restyle, song and voice-conversion submissions first pass the
/// <see cref="GpuJobGate"/>, so <c>POST /api/tracks/{id}/restyles</c>, <c>/api/songs</c>, <c>/api/generations/{id}/retry</c> and
/// the voice conversions free the local model's GPU memory before the generator starts; everything else is forwarded as is.
/// </summary>
/// <param name="inner">The real generator client.</param>
/// <param name="gate">The GPU release gate.</param>
public class GpuReleasingGeneratorClient(IGeneratorClient inner, GpuJobGate gate) : IGeneratorClient
{
    /// <inheritdoc />
    public bool IsConfigured => inner.IsConfigured;

    /// <inheritdoc />
    public Task<GeneratorHealth> GetHealthAsync(CancellationToken cancellationToken) => inner.GetHealthAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<GeneratorJob> CreateRestyleAsync(GeneratorRestyleRequest request, CancellationToken cancellationToken)
    {
        using (await gate.BeforeGeneratorJobAsync(cancellationToken))
        {
            return await inner.CreateRestyleAsync(request, cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task<GeneratorJob?> GetAsync(string generationId, CancellationToken cancellationToken) => inner.GetAsync(generationId, cancellationToken);

    /// <inheritdoc />
    public Task<GeneratorJob?> CancelAsync(string generationId, CancellationToken cancellationToken) => inner.CancelAsync(generationId, cancellationToken);

    /// <inheritdoc />
    public Task DownloadTakeAsync(GeneratorTakeRef take, Stream destination, CancellationToken cancellationToken) =>
        inner.DownloadTakeAsync(take, destination, cancellationToken);

    /// <inheritdoc />
    public async Task<GeneratorJob> CreateSongAsync(GeneratorSongParams song, CancellationToken cancellationToken)
    {
        using (await gate.BeforeGeneratorJobAsync(cancellationToken))
        {
            return await inner.CreateSongAsync(song, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<GeneratorJob> CreateVoiceConversionAsync(GeneratorVoiceConversionRequest request, CancellationToken cancellationToken)
    {
        using (await gate.BeforeGeneratorJobAsync(cancellationToken))
        {
            return await inner.CreateVoiceConversionAsync(request, cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task<GeneratorSampleCheck> CheckVoiceSampleAsync(Stream sample, string fileName, CancellationToken cancellationToken) =>
        inner.CheckVoiceSampleAsync(sample, fileName, cancellationToken);

    /// <inheritdoc />
    public Task DownloadCheckedSampleAsync(string checkId, Stream destination, CancellationToken cancellationToken) =>
        inner.DownloadCheckedSampleAsync(checkId, destination, cancellationToken);

    /// <inheritdoc />
    public Task DownloadTakeVocalAsync(string generationId, int index, Stream destination, CancellationToken cancellationToken) =>
        inner.DownloadTakeVocalAsync(generationId, index, destination, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(string generationId, CancellationToken cancellationToken) => inner.DeleteAsync(generationId, cancellationToken);
}

/// <summary>The <see cref="GpuReleasingGeneratorClient"/> of the singer service (voice conversions, feature 010).</summary>
/// <param name="inner">The real singer client.</param>
/// <param name="gate">The GPU release gate.</param>
public sealed class GpuReleasingSingerClient(ISingerClient inner, GpuJobGate gate) : GpuReleasingGeneratorClient(inner, gate), ISingerClient;
