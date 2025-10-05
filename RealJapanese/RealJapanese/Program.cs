using DataLoaders;
using RealJapanese.Components;
using Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

//Data
builder.Services.AddSingleton<NumbersQuestionGenerator>();
builder.Services.AddSingleton<GreetingsData>();

var app = builder.Build();

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