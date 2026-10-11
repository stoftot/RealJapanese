namespace Repositories.DTOs;

public class QuestionAnswerDto
{
    public required string Question { get; set; }
    public required string Answer { get; set; }
    public DataLoaders.Models.Word? SourceWord { get; set; }
    /// <summary>Receives answer correctness and returns whether the item moved to another study list.</summary>
    public Func<bool, bool>? RecordAnswer { get; set; }
    
    public QuestionAnswerDto Flip()
    {
        return new QuestionAnswerDto
        {
            Question = Answer,
            Answer = Question,
            SourceWord = SourceWord,
            RecordAnswer = RecordAnswer
        };
    }
}
