using DataLoaders.Models;
using DataLoaders.Models.Genki;
using RealJapanese.TestSupport;
using Repositories.Genki;

namespace RealJapanese.IntegrationTests;

/// <summary>Checks embedded schemas against source vocabulary without enumerating sentence combinations.</summary>
public sealed class GenkiSourceCoverageIntegrationTests
{
    [Fact]
    public void Every_embedded_schema_segment_has_source_words_and_licensed_forms()
    {
        using var workspace = new TestWorkspace();
        var catalog = new GenkiCatalog();
        var vocabulary = GenkiVocabulary.Load(workspace.CatalogRoot);
        catalog.ValidateVocabularyReferences(vocabulary);

        foreach (var schema in catalog.Schemas)
        {
            var allowedGrammar = catalog.AllowedGrammar(schema);
            foreach (var segment in Flatten(schema.Segments))
            {
                IReadOnlyList<string> forms = segment.FormChoices.Count == 0 ? [segment.Form] : segment.FormChoices;
                if (segment.Kind == "fixed")
                {
                    var sourceWord = vocabulary.Resolve(segment.Word!);
                    AssertFormsSupported(schema, segment, forms, [sourceWord]);
                }
                else if (segment.Kind == "slot")
                {
                    var slot = schema.Slots[segment.Name!];
                    var slotWords = vocabulary.Entries.Where(entry => slot.WordTypes.Contains(entry.Ref.WordType) &&
                        (slot.ConjugationTypes.Count == 0 || entry.Word is Conjugatabel conjugatable &&
                            slot.ConjugationTypes.Contains(conjugatable.Type))).ToArray();
                    Assert.NotEmpty(slotWords);
                    AssertFormsSupported(schema, segment, forms, slotWords);
                }

                void AssertFormsSupported(GenkiSchema currentSchema, GenkiSegment currentSegment,
                    IReadOnlyList<string> requestedForms, IReadOnlyList<VocabularyEntry> words)
                {
                    foreach (var form in requestedForms)
                    {
                        var supportedWords = words.Where(entry => GenkiForms.Supports(entry.Word, form)).ToArray();
                        Assert.True(supportedWords.Length > 0,
                            $"{currentSchema.Id} segment {currentSegment.Kind}/{currentSegment.Name} has no source word supporting '{form}'.");
                        // Slots may contain later counters/forms: the enumerator prunes those words.
                        // Every fixed reference, but only one compatible slot word, must be licensed.
                        var licensed = supportedWords.Where(entry => GenkiForms.RequiredGrammar(form, entry.Word).All(allowedGrammar.Contains)).ToArray();
                        Assert.NotEmpty(licensed);
                        if (currentSegment.Kind == "fixed") Assert.Equal(supportedWords.Length, licensed.Length);
                    }
                }
            }
        }
    }

    private static IEnumerable<GenkiSegment> Flatten(IEnumerable<GenkiSegment> segments)
    {
        foreach (var segment in segments)
        {
            yield return segment;
            if (segment.Kind == "optional")
                foreach (var child in Flatten(segment.Children)) yield return child;
        }
    }
}
