namespace Repositories.DTOs;

public class QuestionAnswerDto
{
    public required string Question { get; set; }
    public required string Answer { get; set; }
    
    public static QuestionAnswerDto FromModel(DataLoaders.Models.QuestionAnswerModel model)
    {
        return new QuestionAnswerDto
        {
            Question = model.Question,
            Answer = model.Answer
        };
    }
    
    public QuestionAnswerDto Flip()
    {
        return new QuestionAnswerDto
        {
            Question = Answer,
            Answer = Question
        };
    }
}