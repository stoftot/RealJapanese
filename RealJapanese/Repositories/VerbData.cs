using DataLoaders.Models;
using Repositories.DTOs;
using WanaKanaSharp;

namespace Repositories;

public class VerbData
{
    private IEnumerable<Verb> RawVerbs { get; }
    public VerbData()
    {
        var loader = new DataLoaders.VerbLoader("Verbs.json");
        
        RawVerbs = loader.Load();
    }
    
    public IEnumerable<QuestionAnswerDto> DictionaryVerbs =>
        RawVerbs.Select(v => new QuestionAnswerDto
        {
            Answer = WanaKana.ToRomaji(v.Kana),
            Question = v.English
        });
}