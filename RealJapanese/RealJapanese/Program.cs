using DataLoaders;
using RealJapanese.Components;
using Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

//Data
builder.Services.AddSingleton<NumbersQuestionGenerator>();
builder.Services.AddSingleton<VerbData>();
builder.Services.AddSingleton<WordData>();
builder.Services.AddSingleton<KanjiData>();

Console.OutputEncoding = System.Text.Encoding.UTF8;

var app = builder.Build();

// var sentence = new SentenceLoader("jp_kana_reading_dataset_v3.jsonl").Load();
// var sentence = new SentenceLoader("Real sentences.json").Load();
// var particals = new ParticalsLoader("Particals.json").Load();
// Console.WriteLine(particals.Count(p => p.MissingParticles.Length == 3));
// var Numbers = new NumbersQuestionGenerator();
//
// foreach (var s in Numbers.QuestionAnswers)
// {
//     Console.WriteLine($"{s.Question} => {s.Answer}");
// }

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();