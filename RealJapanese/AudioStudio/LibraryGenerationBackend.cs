using AiLibrary;
using GenerateAudioUsingTTS;

namespace AudioStudio;

public sealed class LibraryGenerationBackend(StudioOptions options, StudioStore store, MediaTools media, PitchProfiles pitch) : IGenerationBackend
{
    public void Prepare(StudioRun run)
    {
        run.SpeechSettings ??= options.Speech with { ReferenceTranscript = run.ReferenceSnapshot?.Transcript };
        run.SpeechSettings.Validate();
        string root = store.RunFolder(run.Id);
        Directory.CreateDirectory(root);
        string reference = Path.Combine(root, "reference.wav");
        if (run.ReferenceId is not null && !File.Exists(reference))
            File.Copy(store.ReferenceAudio(run.ReferenceId), reference, false);
    }
    public IVoiceBatch OpenVoice(StudioRun run, string engine) => new VoiceBatch(options, store, media, pitch, run, engine);
    public ICheckBatch OpenChecks(StudioRun run) => new CheckBatch(options.CreateTranscriber(), store.RunFolder(run.Id));

    private sealed class CheckBatch(LibraryTranscriber transcriber, string runFolder) : ICheckBatch
    {
        public async Task<CheckedAudio> CheckAsync(StudioClip clip, CancellationToken token)
        {
            var result = await transcriber.TranscribeOneAsync(clip.Engine + ":" + clip.Key,
                StudioStore.SafePath(runFolder, clip.File!), false, token);
            return new(result.Text, result.Error, "AI library Whisper small", result.Diagnostics);
        }
        public ValueTask DisposeAsync() => transcriber.DisposeAsync();
    }
    private sealed class VoiceBatch : IVoiceBatch
    {
        private readonly StudioStore store;
        private readonly MediaTools media;
        private readonly PitchProfiles pitch;
        private readonly StudioRun run;
        private readonly string engine;
        private readonly Settings settings;
        private readonly IAiModelService service;

        public VoiceBatch(StudioOptions options, StudioStore store, MediaTools media, PitchProfiles pitch, StudioRun run, string engine)
        {
            this.store = store; this.media = media; this.pitch = pitch; this.run = run; this.engine = engine;
            settings = run.SpeechSettings ?? options.Speech;
            service = Synthesis.Create(engine, settings);
        }
        public async Task<GeneratedAudio> GenerateAsync(CatalogWord word, CancellationToken token)
        {
            string runFolder = store.RunFolder(run.Id);
            // Each uncommitted attempt owns a fresh folder. A retry never overwrites a saved clip.
            string relativeRoot = $"{engine}/{word.Dataset}/{word.Id}/{Guid.NewGuid():N}";
            string folder = StudioStore.SafePath(runFolder, relativeRoot);
            string rawFolder = Path.Combine(folder, "raw");
            Directory.CreateDirectory(rawFolder);
            bool completed = false;
            try
            {
                string? reference = run.ReferenceId is not null && engine != "bert" ? Path.Combine(runFolder, "reference.wav") : null;
                var request = Synthesis.Request(engine, new Verb(word.Id, word.SpokenJapanese, word.SpokenKana, word.English), settings, rawFolder, reference);
                var pronunciation = engine == "bert" ? pitch.For(word)?.Profile : null;
                if (pronunciation is not null)
                {
                    request = request with { PronunciationProfile = pronunciation.Validate(), SplitLines = false };
                    StudioStore.AtomicWrite(Path.Combine(folder, "pitch-profile.json"), pronunciation);
                }
                if (engine == "irodori") request = request with
                {
                    IrodoriOptions = new IrodoriSpeechOptions { MinSeconds = 0.5, MaxSeconds = Math.Clamp(word.Kana.Length * 0.22 + 1.5, 2.5, 20), DurationScale = 1 },
                    VoiceDescription = "自然で落ち着いた声。指定された日本語だけを明瞭に、普通の速さで発音する。説明や他の言葉を付け加えない。"
                };
                if (engine == "fish") request = request with
                {
                    FishOptions = new FishSpeechOptions { MaxNewTokens = Math.Clamp(word.Kana.Length * 20, 128, 1024), Temperature = 0.65, TopP = 0.7, RepetitionPenalty = 1.2 }
                };
                StudioStore.AtomicWrite(Path.Combine(folder, "request.json"), request);
                await service.SynthesizeSpeechAsync(request, token);
                string extension = run.OutputQuality switch { "Mp3Mono64Kbps" => ".mp3", "OpusMono32Kbps" => ".opus", _ => ".wav" };
                string file = relativeRoot + "/clip" + extension;
                string output = StudioStore.SafePath(runFolder, file);
                double seconds = await media.ProcessWordAsync(request.OutputFilePath, output, run.OutputQuality, token);
                var result = new GeneratedAudio(file, Path.GetRelativePath(runFolder, request.OutputFilePath), seconds,
                    StudioStore.Hash(output), pronunciation is not null ? "Stored BERT profile · machine estimate" : "Model-inferred accent");
                completed = true;
                return result;
            }
            finally
            {
                if (!completed)
                {
                    Paths.RequireChild(folder, runFolder);
                    if (Directory.Exists(folder)) Directory.Delete(folder, true);
                }
            }
        }
        public ValueTask DisposeAsync() => service.DisposeAsync();
    }
}
