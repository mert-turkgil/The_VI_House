# Contributing to The VI House

You can run the whole site on your own machine without any of the project's secrets.

## What you need

- **.NET 10 SDK**
- **Node.js 22** or later. `dotnet build` runs `npm install` and the Vite build for you.
- **SQL Server**, one of these:
  - **Windows:** SQL Server Express LocalDB. It is included with Visual Studio, or you can install it with the SQL Server Express installer.
  - **macOS / Linux:** SQL Server in Docker:
    ```sh
    docker run -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='Local-Dev-Pass-1' -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
    ```

## Run it

```sh
git clone https://github.com/<you>/The_VI_House.git
cd The_VI_House/src/VIHouse.WebUI
cd ClientApp && npm run fetch:media && cd ..   # site photography (optional; without it covers show a placeholder)
dotnet run --launch-profile http
```

On macOS / Linux, point the app at the Docker database once before `dotnet run`:

```sh
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=VIHouse_Dev;User Id=sa;Password=Local-Dev-Pass-1;TrustServerCertificate=True"
```

The first start creates the database, applies the migrations and adds demo content.

Open http://localhost:5062 and sign in with the local admin:

| Email | Password |
|---|---|
| `admin@vihouse.local` | `VIHouse-Dev-Admin-1!` |

This account only exists in your local database.

## What is switched off without secrets

The defaults live in `src/VIHouse.WebUI/Helpers/DevelopmentDefaults.cs` and apply only in Development. Each of the features below turns itself off when its keys are missing. The app runs normally without them.

| Feature | Without keys | To try it, set with `dotnet user-secrets set …` |
|---|---|---|
| Email | Sent to `localhost:25`. Run [smtp4dev](https://github.com/rnwood/smtp4dev) or Papercut to read the messages. If nothing is listening, emails are logged as failed and nothing else breaks. | `Smtp:Host`, `Smtp:Port`, … |
| Payments (Stripe) | Starting a checkout fails with a Stripe error. Everything else works. | `Stripe:SecretKey`, `Stripe:PublishableKey`, `Stripe:WebhookSecret` (your own **test-mode** keys) |
| Google / Apple / LinkedIn sign-in | The buttons are hidden. On the "Sign-in providers" page they show as "Coming soon". | `Authentication:Google:ClientId` / `ClientSecret`, and so on |
| Rich-text editor (CKEditor) | Falls back to a plain textarea. | `CkEditor:LicenseKey` |
| SMS | Off. | `Sms:*` |
| Image processing licence (ImageSharp) | Debug builds only print a warning. Only Release builds need `sixlabors.lic`. | — |

Your own keys stay in user-secrets, which live outside the repository. **Never put a key in a committed file.** `appsettings.Development.json`, `appsettings.Production.json` and `sixlabors.lic` are gitignored for that reason.

## Pull requests

- Branch from `master` in your fork, and open the pull request against `master`.
- CI builds and runs the tests on every pull request. Fork pull requests are built in Debug, because GitHub does not give secrets to forks.
- Before you push, run `dotnet build` and `dotnet test` from the repository root.
- The site is in four languages: English, German, Turkish and Estonian. Text shown to users goes in `src/VIHouse.WebUI/Resources/SharedResource*.resx`, with a value in all four files. Don't hard-code it in a view.
