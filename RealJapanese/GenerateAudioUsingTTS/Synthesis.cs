using AiLibrary;
using AiLibrary.Speech;
using AiLibrary.StyleBertVits2;

namespace GenerateAudioUsingTTS;

public static class Synthesis
{
    public static string BertId(string voice) => voice switch
    {
        "M1" => StyleBertVits2Voices.M1, "M2" => StyleBertVits2Voices.M2,
        "F1" => StyleBertVits2Voices.F1, "F2" => StyleBertVits2Voices.F2,
        _ => throw new ArgumentException("Unknown BERT voice.")
    };

    public static SpeechSynthesisRequest Request(string engine, Verb verb, Settings settings, string folder, string? reference)
    {
        var request = new SpeechSynthesisRequest
        {
            ModelId = engine switch
            {
                "bert" => BertId(settings.BertVoice), "fish" => LocalSpeechBuilderExtensions.FishAudioS2Pro,
                "irodori" => LocalSpeechBuilderExtensions.IrodoriTts, _ => throw new ArgumentException("Unknown engine.")
            },
            // One dictionary word, no carrier sentence or spoken instructions.
            Text = engine == "bert" ? verb.Japanese : null, KanaReading = verb.Kana,
            OutputFolderPath = folder, OutputFileName = verb.Id.ToString("D3"),
            OutputQuality = SpeechOutputQuality.OriginalWav, Timeout = TimeSpan.FromMinutes(3),
            Seed = settings.Seed,
            FishOptions = engine == "fish" ? new FishSpeechOptions { MaxNewTokens = 128, Temperature = 0.65, TopP = 0.7, RepetitionPenalty = 1.2 } : null,
            IrodoriOptions = engine == "irodori" ? new IrodoriSpeechOptions { MinSeconds = 0.5, MaxSeconds = 2.5, DurationScale = 1 } : null,
            ReferenceAudioPath = engine == "bert" ? null : reference,
            ReferenceText = engine == "fish" && reference is not null ? settings.ReferenceTranscript : null,
            VoiceDescription = engine == "irodori" ? settings.VoiceDescription : null
        };
        if (engine == "fish" && reference is not null && string.IsNullOrWhiteSpace(settings.ReferenceTranscript))
            throw new ArgumentException("Fish requires the exact transcript of the cropped reference, not the complete podcast.");
        if (engine == "bert") request = request with
        {
            Style = "Neutral", StyleWeight = 1, LengthScale = 1.05,
            NoiseScale = 0.35, DurationNoiseScale = 0.3, SdpRatio = 0.1,
            SplitLines = false, LinePauseSeconds = 0
        };
        return request.Validate();
    }

    public static IAiModelService Create(string engine, Settings settings)
    {
        var builder = AiLibraryBuilder.Create().UseModelsDirectory(settings.ModelsDirectory);
        SpeechGpuOptions? Gpu(SpeechGpuProfile profile) => settings.UseGpu ? new() { Profile = profile } : null;
        switch (engine)
        {
            case "bert":
                builder.UseStyleBertVits2(o => { o.PythonExecutablePath = settings.BertPython; o.Gpu = Gpu(SpeechGpuProfile.Desktop); })
                    .AddStyleBertVits2JpExtraModel(BertId(settings.BertVoice));
                break;
            case "fish":
                builder.AddFishAudioS2ProModel(o => { o.PythonExecutablePath = settings.FishPython; o.Gpu = Gpu(SpeechGpuProfile.Generation); });
                break;
            case "irodori":
                builder.AddIrodoriTtsModel(o => { o.PythonExecutablePath = settings.IrodoriPython; o.Gpu = Gpu(SpeechGpuProfile.Desktop); });
                break;
            default: throw new ArgumentException("Unknown engine.");
        }
        return builder.Build();
    }

    public static void ConfigureRuntime(string folder, string libraryRoot)
    {
        Paths.RequireOutsideLibrary(folder, libraryRoot);
        Directory.CreateDirectory(folder);
        Environment.SetEnvironmentVariable("PYTHONDONTWRITEBYTECODE", "1");
        Environment.SetEnvironmentVariable("HF_HUB_OFFLINE", "1");
        Environment.SetEnvironmentVariable("TRANSFORMERS_OFFLINE", "1");
        foreach (string name in new[] { "TEMP", "TMP", "TMPDIR", "TORCH_HOME", "TORCHINDUCTOR_CACHE_DIR", "TRITON_CACHE_DIR", "MPLCONFIGDIR", "XDG_CACHE_HOME" })
        {
            string path = Path.Combine(folder, name.ToLowerInvariant());
            Directory.CreateDirectory(path);
            Environment.SetEnvironmentVariable(name, path);
        }
    }
}
