namespace StemStudio.Agent.LyricAssist;

/// <summary>
/// One schema-constrained model call (implemented by <c>LyricModelClient</c> for Ollama <c>format</c> / the Claude forced tool, and
/// by the fakes). Tool-less: the model only returns JSON text that the parser and validator check.
/// </summary>
public interface ILyricModel
{
    /// <summary>Asks the model for the output of one block.</summary>
    /// <param name="request">Prompts, temperature and block.</param>
    /// <param name="cancellationToken">Cancellation (also the per-block timeout).</param>
    /// <returns>The raw JSON text, the model that answered and the token usage.</returns>
    Task<LyricModelReply> CompleteAsync(LyricModelRequest request, CancellationToken cancellationToken);
}
