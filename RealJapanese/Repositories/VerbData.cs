using DataLoaders.Exstensions;
using DataLoaders.Models;
using Repositories.Bases;
using Repositories.DTOs;
using WanaKanaSharp;

namespace Repositories;

public class VerbData() : WordDataBase<Verb>(FolderPath, DataFileName)
{
    private const string FolderPath = "../Data/Verbs";
    private const string DataFileName = "Verbs.json";
    
    
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