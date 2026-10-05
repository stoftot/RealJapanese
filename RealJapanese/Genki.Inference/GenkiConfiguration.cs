using Genki.Generation;

namespace Genki.Inference;

/// <summary>The same editable configuration is consumed by the command line and PC studio.</summary>
public sealed record GenkiConfiguration
{
    public required string DataRoot { get; init; }
    public required string StateRoot { get; init; }
    public required AiLibraryOptions Models { get; init; }
    public BatchOptions Batch { get; init; } = new();
    public string? PublishPath { get; init; }

    public GenkiConfiguration Resolve(string basePath)
    {
        if (string.IsNullOrWhiteSpace(DataRoot) || string.IsNullOrWhiteSpace(StateRoot))
            throw new InvalidDataException("Choose both the source data and generated state folders.");
        var data = Path.GetFullPath(DataRoot, basePath);
        var state = Path.GetFullPath(StateRoot, basePath);
        if (IsWithin(state, data) || IsWithin(data, state))
            throw new InvalidDataException("Keep generated state and source vocabulary in separate directories.");
        return this with
        {
            DataRoot = data, StateRoot = state,
            PublishPath = Path.GetFullPath(PublishPath ?? Path.Combine(data, "Genki", "questions.jsonl"), basePath),
            Models = Models with { ModelsDirectory = string.IsNullOrWhiteSpace(Models.ModelsDirectory) ? "" : Path.GetFullPath(Models.ModelsDirectory, basePath) }
        };
    }
    public static bool IsWithin(string path, string directory)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(directory), Path.GetFullPath(path));
        return relative == "." || !Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
