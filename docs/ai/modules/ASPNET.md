# ASP.NET project facts

## Host and launch configuration

| Fact | Value |
| --- | --- |
| Entry project | `RealJapanese/RealJapanese/RealJapanese.csproj`: Web SDK, `net10.0` |
| Startup | `RealJapanese/RealJapanese/Program.cs` |
| Shared UI | `RealJapanese/RealJapanese.UI/`: Razor class library, `net10.0` |
| Rendering | Blazor Interactive Server |
| Profiles | `http`, `https`; Development; no automatic browser |
| HTTP URL | `http://localhost:5287` |
| HTTPS URLs | `https://localhost:7234` and `http://localhost:5287` |

From the repository root after building:

```powershell
dotnet run --project RealJapanese/RealJapanese/RealJapanese.csproj --no-build --launch-profile http
```

The browser needs this local ASP.NET process while the application is in use.
Study features do not require internet access or a remote backend.

## UI and route ownership

The web project owns the HTML document in `Components/App.razor` and the web error
page. Routes, layout, study pages, reusable components, CSS, JavaScript and Bootstrap
assets live in the RealJapanese.UI Razor class library. The host maps that assembly
and exposes its static assets through `_content/RealJapanese.UI/`.

Vocabulary practice routes require `?category=known|rehearsing|training`, normally
supplied by selectors. Some selector targets remain unimplemented; source inspection
is required before assuming a route exists.

## Services, data and environment

`Program.cs` resolves:

- `StudyData:CatalogRoot`, defaulting to the sibling `RealJapanese/Data/` directory.
- `StudyData:ProgressRoot`, defaulting to the resolved catalog root.

Use appsettings or environment variables such as
`StudyData__ProgressRoot` to separate roots. Each root must preserve the normal
dataset subdirectories. Catalog inputs must exist; an absent progress file starts
empty and is created when progress is first saved.

Repository construction no longer rewrites catalogs. Web repositories remain
singletons inside the server process, so concurrent browser sessions share the
configured web progress files. No database, remote API, identity provider or
authentication middleware is configured.

Middleware includes HTTPS redirection, antiforgery and mapped static assets;
non-Development adds HSTS and exception handling. HTTPS requires an appropriate
development certificate.

Use the [ASP.NET skill](../../../.agents/skills/aspnet-web/SKILL.md) for real-browser
validation. The current Debug build passed; this does not itself establish browser
behavior.
