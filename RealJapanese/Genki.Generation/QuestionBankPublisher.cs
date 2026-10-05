using System.Text.Json;
using Repositories.Genki;

namespace Genki.Generation;

public static class QuestionBankPublisher
{
    /// <summary>Caller holds the state writer lock. Validation completes before atomic bank replacement.</summary>
    public static long Publish(BatchStore store, GenkiCatalog catalog, GenkiVocabulary vocabulary, string output)
    {
        long count = 0;
        foreach (var state in store.ReadAll<QuestionState>("questions").Where(s => s.Status == "completed"))
        {
            if (state.Question is null) throw new InvalidDataException("Completed state has no question.");
            GenkiPracticeService.ValidateQuestion(state.Question, catalog, vocabulary);
            if (!state.Stages.TryGetValue("A-candidate", out var rendered)) throw new InvalidDataException("Missing canonical provenance.");
            var candidate = rendered.Deserialize<Candidate>(GenkiJson.Options) ?? throw new InvalidDataException("Empty canonical provenance.");
            var schema = catalog.Schemas.Single(s => s.Id == state.SchemaId);
            var current = GenkiJson.Fingerprint(new { schema, words = candidate.RequiredWords.Select(w => vocabulary.Resolve(w).ModelInput) });
            if (current != state.InputFingerprint) throw new InvalidDataException($"Stale candidate {state.CandidateId}; reprocess before publication.");
            count++;
        }
        store.ExportBank(output);
        return count;
    }
}
