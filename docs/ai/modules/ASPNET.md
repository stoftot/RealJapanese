# ASP.NET project facts

## Host and launch configuration

| Fact | Value |
| --- | --- |
| Entry project | `RealJapanese/RealJapanese/RealJapanese.csproj`: Web SDK, net9.0 |
| Startup | `RealJapanese/RealJapanese/Program.cs` |
| UI | Blazor Razor components; practice/selector pages opt into Interactive Server |
| Profiles | `http`, `https` in `Properties/launchSettings.json`; both Development, no automatic browser |
| HTTP URL | `http://localhost:5287` |
| HTTPS profile URLs | `https://localhost:7234` and `http://localhost:5287` |
| Test projects | None found |

From the repository root, after building and with .NET 9 runtimes installed:

```powershell
dotnet run --project RealJapanese/RealJapanese/RealJapanese.csproj --no-build --launch-profile http
```

Data paths such as `../Data/Words` are relative to the process working directory.
The expected directory is `RealJapanese/RealJapanese/`; project-profile launch
uses it. Set it explicitly for a direct DLL launch. Data is outside `wwwroot` and
is not configured for copying into publish output.

## Browser workflows

| Route | Implemented purpose / setup |
| --- | --- |
| `/` | Home and navigation |
| `/words`, `/verbs`, `/adjectives`, `/kanji` | Select vocabulary/progress category; controls write files |
| `/words/spelling`, `/words/flashcards` | Spelling or reveal/recall practice |
| `/verbs/spelling`, `/verbs/categories`, `/verbs/ConjugationsAndForms` | Verb spelling, classification and conjugation/forms |
| `/adjectives/spelling`, `/adjectives/categories`, `/adjectives/conjugateBase` | Adjective spelling, classification and conjugation |
| `/kanji/single/meaning`, `/kanji/combined/meaning` | Meaning practice from the corresponding vocabulary |
| `/numbers` | Generated number questions |
| `/Error` | Error display; non-Development exception-handler target |

Vocabulary practice requires `?category=known|rehearsing|training`, normally
supplied by selectors. `WordPracticeCategoryExtensions.ParseQueryValue` throws
for missing or unrecognized values. Use a populated category for interaction checks.

Selectors also advertise `/verbs/conjugation`, `/verbs/fillIn` and
`/adjectives/conjugation`, but no matching route pages exist. These are current
source-level limitations, not implemented workflows.

## Services, data and environment

- No database, remote API, identity provider or authentication middleware is
  configured for the web host. Singleton services supply data/questions. Local
  model inference belongs to the separate extraction utility.
- Required inputs are vocabulary datasets and `SavedData.json` files under
  `RealJapanese/Data/`. File IO does not silently create missing input files.
- Repository construction invokes `UpdateIDs()` and saves vocabulary. Visiting a
  data-backed page can write data before a progress button is clicked. Use
  disposable data for browser validation that must preserve originals.
- Appsettings files configure logging; the base file also sets allowed hosts.
  Launch settings own development ports and environment.
- Middleware includes HTTPS redirection, antiforgery and mapped static assets;
  non-Development adds HSTS and exception handling. HTTPS requires an appropriate
  development certificate. Confirm the actual listening URL at launch.
- No deployment profile or per-user state isolation is established. Hosting needs
  deliberate data layout and file permissions.

Use the [ASP.NET skill](../../../.agents/skills/aspnet-web/SKILL.md) for real-browser
validation; source inspection and compilation do not establish UI behavior.
