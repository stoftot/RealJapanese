namespace Repositories;

public class GreetingsData() : QuestionAnswerBase(FolderPath, FileName)
{
    private static string FolderPath => "../Data";
    private static string FileName => "Greetings.csv";
}