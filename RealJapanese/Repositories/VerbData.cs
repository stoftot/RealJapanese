using DataLoaders.Exstensions;
using DataLoaders.Models;
using Repositories.Bases;
using Repositories.DTOs;
using WanaKanaSharp;

namespace Repositories;

public class VerbData : WordDataBase<Verb>
{
    private const string DataFileName = "Verbs.json";

    public VerbData()
        : this(RepositoryPaths.Default)
    {
    }

    public VerbData(RepositoryPaths paths)
        : base(paths.CatalogFolder("Verbs"), DataFileName, paths.ProgressFolder("Verbs"))
    {
    }
    
    
    // private IEnumerable<Verb> RawVerbs { get; }
    // public VerbData()
    // {
    //     var loader = new DataLoaders.VerbLoader("Verbs.json");
    //     
    //     RawVerbs = loader.Load();
    // }
    //
    // public IEnumerable<QuestionAnswerDto> DictionaryVerbs =>
    //     RawVerbs.Skip(0).Take(15).EnglishToRomajiQuestions();
    // //     Select(v => new QuestionAnswerDto
    // // {
    // //     Answer = v.Kana.ToRomaji(),
    // //     Question = v.English
    // // });
}
