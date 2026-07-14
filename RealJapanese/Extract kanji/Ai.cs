using AiLibrary;
using AiLibrary.LlamaServer;
using DataLoaders.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Extract_kanji;

public class Ai : IAsyncDisposable
{
    private IAiModelService AiModelService { get; init; }
    public Ai(string modelDirectoryPath)
    {
        AiModelService = AiLibraryBuilder.Create()
            .UseModelsDirectory(modelDirectoryPath)
            .UseLlamaServer(options =>
            {
                options.ExecutablePath = Environment.GetEnvironmentVariable("LLAMA_SERVER_PATH");
            })
            .AddQwenInstructModel()
            .Configure(options =>
            {
                options.Lifecycle.ShutdownWhenQueueIsEmpty = false;
            })
            .Build();
    }

    public async Task<Kanji> FillOutForKanji(NewKanji newKanji)
    {
        const string systemPrompt = """
                              You receive exactly one Japanese kanji character as input.

                              Return exactly one valid JSON object using this structure:

                              {
                                "symbol": "水",
                                "onReading": "スイ",
                                "kunReading": "みず",
                                "commonEnglish": "water",
                                "otherEnglish": [
                                  "liquid"
                                ]
                              }

                              Rules:

                              1. Return only the JSON object.
                              2. Do not use Markdown or code fences.
                              3. Do not include explanations or additional text.
                              4. Use exactly the property names shown in the template.
                              5. The "symbol" value must be the input kanji.
                              6. "onReading" must contain exactly one common on'yomi reading written in katakana.
                              7. "kunReading" must contain exactly one common kun'yomi reading written in hiragana.
                              8. For kun'yomi, include only the kana pronounced by the kanji itself.
                              9. Do not include okurigana in "kunReading".
                              10. Do not include dots, periods, middle dots, hyphens, dashes, spaces, commas, slashes, or multiple readings in either reading field.
                              11. If multiple readings exist, choose the most common reading used in modern everyday Japanese.
                              12. If no on'yomi exists, use "NA".
                              13. If no kun'yomi exists, use "NA".
                              14. "commonEnglish" must be the most common English meaning, written in lowercase.
                              15. "otherEnglish" must contain only genuine alternative meanings, not explanations or example words.
                              16. If there are no useful alternative English meanings, return an empty array.

                              Examples:

                              Input:
                              暑

                              Output:
                              {
                                "symbol": "暑",
                                "onReading": "ショ",
                                "kunReading": "あつ",
                                "commonEnglish": "hot",
                                "otherEnglish": [
                                  "summer heat"
                                ]
                              }

                              Input:
                              寒

                              Output:
                              {
                                "symbol": "寒",
                                "onReading": "カン",
                                "kunReading": "さむ",
                                "commonEnglish": "cold",
                                "otherEnglish": [
                                  "chilly"
                                ]
                              }

                              Input:
                              古

                              Output:
                              {
                                "symbol": "古",
                                "onReading": "コ",
                                "kunReading": "ふる",
                                "commonEnglish": "old",
                                "otherEnglish": [
                                  "ancient"
                                ]
                              }

                              Input:
                              新

                              Output:
                              {
                                "symbol": "新",
                                "onReading": "シン",
                                "kunReading": "あたら",
                                "commonEnglish": "new",
                                "otherEnglish": [
                                  "fresh"
                                ]
                              }
                              """;
        var result = await Ask(systemPrompt, newKanji.Kanji);
        if (result.ValidationFailed)
        {
            return new Kanji
            {
                Id = newKanji.KanjiId,
                Symbol = $"{newKanji.Kanji} - FAILED",
                CommonEnglish = result.Text,
                KunReading = "NA",
                OnReading = "NA",
                OtherEnglish = []
            };
        }
        var Kanji = JsonSerializer.Deserialize<Kanji>(result.Text);
        Kanji.Id = newKanji.KanjiId;
        return Kanji;
    }

    public async Task<GenerationResult> Ask(string systemMessage, string input)
    {
        var result = await AiModelService.GenerateAsync(new GenerationRequest
        {
            ModelId = BuiltInModelIds.Qwen25_14B_Instruct,
            Messages =
            [
                new ChatMessage(ChatRole.System, systemMessage),
                new ChatMessage(ChatRole.User, input),
            ],
            Options = new GenerationOptions
            {
                Temperature = 0,
                MaxTokens = 1000,
                Seed = 42,
                TopP = 1.0,
            },
            Validator = new KanjiInfoValidator(),
        });

        return result;
    }

    private class KanjiInfoValidator : IGenerationValidator
    {
        public int MaxRetries => 2;
        public string ValidationFailedMessage { get; private set; } = "The failed message is not created yet.";
        public ValueTask<bool> ValidateAsync(string generatedText, CancellationToken cancellationToken = default)
        {
            List<string> errors = [];
            Kanji kanji;
            try
            {
                kanji = JsonSerializer.Deserialize<Kanji>(generatedText);
            }
            catch (Exception e)
            {
                ValidationFailedMessage = $"Couldn't convert the json: {e.Message}";
                return ValueTask.FromResult(false);
            }
            

            if (!IsKatakanaReading(kanji.OnReading))
            {
                errors.Add(
                    "onReading must be \"NA\" or exactly one katakana reading " +
                    "without spaces, dots, hyphens, or punctuation."
                );
            }

            if (!IsHiraganaReading(kanji.KunReading))
            {
                errors.Add(
                    "kunReading must be \"NA\" or only the hiragana pronounced by " +
                    "the kanji itself, without okurigana, spaces, dots, or hyphens."
                );
            }

            if (string.IsNullOrWhiteSpace(kanji.CommonEnglish))
                errors.Add("commonEnglish must not be empty.");

            if (kanji.OtherEnglish is null)
                errors.Add("otherEnglish must be a JSON array.");

            if (errors.Any())
            {
                ValidationFailedMessage = CreateCorrectionPrompt(kanji.Symbol, generatedText, errors);
                return ValueTask.FromResult(false);
            }

            return ValueTask.FromResult(true);
        }
        private static bool IsHiraganaReading(string? value)
        {
            return value == "NA" ||
                   (!string.IsNullOrWhiteSpace(value) &&
                    Regex.IsMatch(value, @"^[\p{IsHiragana}ー]+$"));
        }

        private static bool IsKatakanaReading(string? value)
        {
            return value == "NA" ||
                   (!string.IsNullOrWhiteSpace(value) &&
                    Regex.IsMatch(value, @"^[\p{IsKatakana}ー]+$"));
        }
        
        private static string CreateCorrectionPrompt(
            string symbol,
            string previousResponse,
            IEnumerable<string> errors)
        {
            string errorText = string.Join(
                Environment.NewLine,
                errors.Select(error => $"- {error}")
            );

            return $"""
                    Your previous response for the kanji "{symbol}" was invalid.

                    Validation errors:
                    {errorText}

                    Previous response:
                    {previousResponse}

                    Return a corrected JSON object only.

                    Important:
                    - Do not use Markdown code fences.
                    - Do not include explanations.
                    - Include only one on-reading and one kun-reading.
                    - Remove okurigana from the kun-reading.
                    """;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await AiModelService.DisposeAsync();
    }
}