using CsvHelper.Configuration;
using DataLoaders.Models;

namespace DataLoaders;

public class QuestionAnswerLoader(string folderPath, string filename) : Loader<QuestionAnswerModel>(folderPath, new QuestionAnswerMap())
{
    protected override string FileName => filename;
    private sealed class QuestionAnswerMap : ClassMap<QuestionAnswerModel>
    {
        public QuestionAnswerMap()
        {
            Map(x => x.Question).Name("question");
            Map(x => x.Answer).Name("answer");
        }
    }
}