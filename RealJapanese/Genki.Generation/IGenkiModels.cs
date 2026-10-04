namespace Genki.Generation;

/// <summary>Only offline workflows depend on local inference. Responses are validated by each stage.</summary>
public interface IGenkiModels
{
    Task<string> QwenAsync(string stage, string systemPrompt, string inputJson, CancellationToken cancellationToken);
    Task<string> TranslateAsync(string japanese, string? setting, string register, CancellationToken cancellationToken);
    string Provenance { get; }
}
