namespace RealJapanese.Components.Shared;

public abstract class MultipleAnswerBase : SingleAnwserBase
{
    protected List<string> GivenAnswers { get; set; } = [];
    protected List<string> MissingAnswers { get; set; } = [];

    protected override void OnQuestionsUpdated()
    {
        ResetCurrentQuestionAnswers();
    }

    protected override bool IsCorrect()
    {
        var givenAnswer = Normalize(UserInput);

        if (string.IsNullOrWhiteSpace(givenAnswer))
            return MissingAnswers.Count == 0;

        var matchingAnswer = MissingAnswers.FirstOrDefault(answer => Normalize(answer) == givenAnswer);
        if (matchingAnswer is null)
            return MissingAnswers.Count == 0;

        GivenAnswers.Add(matchingAnswer);
        MissingAnswers.Remove(matchingAnswer);
        UserInput = string.Empty;

        if (MissingAnswers.Count > 0)
        {
            _ = InvokeAsync(async () =>
            {
                StateHasChanged();
                await FocusAnswerInputAsync();
            });
        }

        return MissingAnswers.Count == 0;
    }

    protected override void NextQuestion()
    {
        base.NextQuestion();
        ResetCurrentQuestionAnswers();
    }

    protected virtual IEnumerable<string> GetAnswersForCurrentQuestion()
    {
        if (Questions.Count == 0)
            return [];

        return CurrentAnswer
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private void ResetCurrentQuestionAnswers()
    {
        GivenAnswers.Clear();
        MissingAnswers = GetAnswersForCurrentQuestion().ToList();
    }
}
