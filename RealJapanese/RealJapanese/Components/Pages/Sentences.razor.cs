using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.DTOs;

namespace RealJapanese.Components.Pages;

public partial class SentenceBase : PracticeBase
{
    [Inject] public SentencesData SentencesData { get; set; } = null!;
    private string _oldUserInput = string.Empty;

    // ref to the shared UI so we can focus the input
    protected PracticeCard? cardRef;

    protected override void OnInitialized()
    {
        Questions = SentencesData.Sentences.Where(s => s.Level.Equals("beginner"))
            .Select(s => new QuestionAnswerDto
        {
            Answer = s.Romaji,
            Question = s.English + "\n" + s.Kanji + "\n" + s.Japanese.Replace(" ", "")
        }).OrderBy(_ => Guid.NewGuid()).ToList();
    }

    protected override Task FocusAnswerInputAsync()
        => cardRef.FocusInput();

    protected override void OnUserInputChanged(string value)
    {
        base.OnUserInputChanged(value);

        // if (IsCorrect())
        //     _ = GoToNextQuestionAsync();
    }

    protected override void OnEnterPressed()
    {
        if (showAnswer && _oldUserInput == UserInput)
        {
            _ = GoToNextQuestionAsync();
        }
        else
        {
            if (IsCorrect())
                _ = GoToNextQuestionAsync();
            else
                RevealAnswer();

            _oldUserInput = UserInput;
        }
    }
}