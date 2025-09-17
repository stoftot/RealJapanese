using DataLoaders;
using Repositories.DTOs;

namespace Repositories;

public class NumbersData() : QuestionAnswerBase(FolderPath, FileName)
{
    private static string FolderPath => "../Data";
    private static string FileName => "Numbers.csv";
}