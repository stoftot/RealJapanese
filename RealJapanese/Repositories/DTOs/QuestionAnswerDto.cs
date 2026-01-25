namespace Repositories.DTOs;

public class QuestionAnswerDto
{
    public required string Question { get; set; }
    public required string Answer { get; set; }
    
    public QuestionAnswerDto Flip()
    {
        return new QuestionAnswerDto
        {
            Question = Answer,
            Answer = Question
        };
    }
}