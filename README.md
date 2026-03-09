# GitHub Portfolio (ASP.NET Core Razor Pages)

Portfolio website for GitHub projects with:
- selected repositories only,
- hidden web admin panel,
- per-project presentation pages,
- media upload (images/videos) and carousel.

## Tech Stack
- ASP.NET Core 9 (Razor Pages)
- C#
- JSON-based local settings storage

## Project Structure
- `WebApplication1/Program.cs` - app startup and `.env` loading
- `WebApplication1/Pages/` - UI pages (home, admin, project details)
- `WebApplication1/Services/` - GitHub API and settings store
- `WebApplication1/Models/` - app models
- `WebApplication1/wwwroot/` - static files (css/js/uploads)
- `WebApplication1/Data/` - local runtime settings (ignored in git)

## Quick Start
1. Install .NET SDK 9
2. Create local env file from template:
   - copy `.env.example` -> `.env`
3. Configure secrets in `.env`:

```env
PORTFOLIO_GITHUB_TOKEN=
PORTFOLIO_ADMIN_ACCESS_CODE=change-me
```

4. Run:

```bash
cd WebApplication1
dotnet run --urls http://localhost:5078
```

5. Open:
- Home: `http://localhost:5078`
- Admin: `http://localhost:5078/__portfolio-admin-7f1d2`

## Admin Panel Features
- Set GitHub username/token
- Choose visible repositories
- Edit About/Contacts sections
- Configure project subpages
- Upload media for each project
- Set media order, captions, and remove media

## GitHub API Rate Limit
If you see rate-limit warnings, add `PORTFOLIO_GITHUB_TOKEN` in `.env` or in admin panel token field.

## Security Notes
- `.env` is ignored by git
- `WebApplication1/Data/portfolio-settings.json` is ignored by git
- uploads folder is ignored by git
- use strong `PORTFOLIO_ADMIN_ACCESS_CODE`

## Build
```bash
cd WebApplication1
dotnet build
```

## Publish to GitHub
```bash
git remote add origin https://github.com/<USERNAME>/<REPO>.git
git branch -M main
git push -u origin main
```
