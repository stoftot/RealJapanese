using AiLibrary;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GenerateAudioUsingTTS;

public sealed record Sample(Verb Verb, string Engine, string Mode, SpeechSynthesisRequest? Request,
    string? File, AudioStats? Audio, string? Error);

public sealed class Report(Options options, Settings settings, IReadOnlyList<Verb> verbs)
{
    public Options Options { get; } = options;
    public Settings Settings { get; } = settings;
    public IReadOnlyList<Verb> Verbs { get; } = verbs;
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.Now;
    public List<Sample> Samples { get; } = [];
    public List<string> Notes { get; } = ["BERT uses a fixed speaker, not voice cloning. Listening is required to assess pronunciation, pitch accent, naturalness and similarity."];
    public string Status { get; set; } = "running";
    public Dictionary<string, string> LibrarySha256 { get; } = Directory.GetFiles(AppContext.BaseDirectory, "AiLibrary.*.dll")
        .ToDictionary(p => Path.GetFileName(p)!, p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));

    public void Save()
    {
        string temporary = Path.Combine(Options.Output, "manifest.json.tmp");
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, Json.Format), Encoding.UTF8);
        File.Move(temporary, Path.Combine(Options.Output, "manifest.json"), overwrite: true);
        File.WriteAllText(Path.Combine(Options.Output, "index.html"), Html(), Encoding.UTF8);
    }

    private string Html()
    {
        static string E(string? text) => WebUtility.HtmlEncode(text) ?? "";
        var html = new StringBuilder("""
            <!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Japanese verb voice comparison</title><style>
            body{font:16px system-ui,sans-serif;background:#f6f4ef;color:#202b31;margin:0;padding:32px;line-height:1.5}
            main{max-width:1350px;margin:auto}h1{font-size:30px;margin-bottom:6px}p{max-width:900px}.notes{background:#e8eee8;padding:16px;border-radius:10px}
            table{border-collapse:collapse;width:100%;background:white}th,td{padding:16px;border-bottom:1px solid #ddd;text-align:left;vertical-align:top}
            th{background:#203d39;color:white}audio{width:240px;max-width:100%;display:block;margin-top:8px}.word{font-size:28px;font-weight:650}
            small{color:#56646b}.warning{color:#8d3b12}.scroll{overflow:auto}a{color:#176b61}footer{margin-top:24px}
            </style><main><h1>One word, three voices</h1>
            """);
        html.Append($"<p>Verb IDs {Options.FirstId}–{Options.LastId} · {E(Status)} · Separate dictionary-form recordings.</p>");
        html.Append("<div class='notes'><strong>Reference excerpt</strong><audio controls preload='none' src='reference.wav'></audio>");
        html.Append($"<p>{Settings.ReferenceStartSeconds:0.00}–{Settings.ReferenceStartSeconds + Settings.ReferenceDurationSeconds:0.00} seconds in the supplied recording.</p>");
        if (Settings.ReferenceTranscript is { } transcript) html.Append($"<p lang='ja'>{E(transcript)}</p><small>{E(Settings.ReferenceTranscriptSource)}</small>");
        foreach (string note in Notes) html.Append($"<p>{E(note)}</p>");
        html.Append("</div><div class='scroll'><table><thead><tr><th>Verb</th>");
        foreach (string engine in Options.Engines) html.Append($"<th>{E(engine.ToUpperInvariant())}</th>");
        html.Append("</tr></thead><tbody>");
        foreach (Verb verb in Verbs)
        {
            html.Append($"<tr><td><small>#{verb.Id:D3}</small><div class='word' lang='ja'>{E(verb.Japanese)}</div><div lang='ja'>{E(verb.Kana)}</div><small>{E(verb.English)}</small></td>");
            foreach (string engine in Options.Engines)
            {
                var sample = Samples.LastOrDefault(s => s.Verb.Id == verb.Id && s.Engine == engine);
                html.Append("<td>");
                if (sample is null) html.Append("Pending");
                else
                {
                    html.Append($"<small>{E(sample.Mode)}</small>");
                    if (sample.File is { } file)
                    {
                        html.Append($"<audio controls preload='none' src='{E(file)}'></audio><small>{sample.Audio!.Seconds:0.00}s · {sample.Audio.SampleRate / 1000d:0.#} kHz</small>");
                        html.Append($"<div><a href='{engine}/raw/{verb.Id:D3}.wav'>Raw recording</a></div>");
                        foreach (string warning in sample.Audio.Warnings) html.Append($"<div class='warning'>{E(warning)}</div>");
                    }
                    if (sample.Error is { } error) html.Append($"<details class='warning'><summary>Generation issue</summary>{E(error)}</details>");
                }
                html.Append("</td>");
            }
            html.Append("</tr>");
        }
        html.Append("</tbody></table></div><footer><a href='manifest.json'>Settings and generation manifest</a> · Raw audio is preserved. Trimming removes only edge silence; long speech is flagged, never cut to fit.</footer></main></html>");
        return html.ToString();
    }
}
