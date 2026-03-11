# GitHub Portfolio (ASP.NET Core Razor Pages)

Сайт-портфолио GitHub-проектов с возможностями:
- отображение только выбранных репозиториев;
- скрытая веб-админка;
- отдельные презентационные страницы проектов;
- загрузка медиа (изображения/видео) и карусель.

## Технологии
- ASP.NET Core 9 (Razor Pages)
- C#
- локальное хранение настроек в JSON
- xUnit + Playwright (UI e2e-тесты)

## Структура проекта
- `WebApplication1/Program.cs` - запуск приложения и загрузка `.env`
- `WebApplication1/Pages/` - UI-страницы (главная, админка, детали проекта)
- `WebApplication1/Services/` - GitHub API и хранилище настроек
- `WebApplication1/Models/` - модели приложения
- `WebApplication1/wwwroot/` - статические файлы (css/js/uploads)
- `WebApplication1/Data/` - локальные runtime-настройки (игнорируются в git)
- `WebApplication1.Tests/` - unit-тесты и Playwright e2e-тесты

## Быстрый старт
1. Установите .NET SDK 9.
2. Создайте локальный env-файл из шаблона:
   - скопируйте `.env.example` -> `.env`
3. Заполните секреты в `.env`:

```env
PORTFOLIO_GITHUB_TOKEN=
PORTFOLIO_ADMIN_ACCESS_CODE=change-me
```

4. Запустите приложение из корня решения:

```bash
dotnet run --project WebApplication1/WebApplication1.csproj --urls http://localhost:5078
```

5. Откройте:
- Главная: `http://localhost:5078`
- Админка: `http://localhost:5078/__portfolio-admin-7f1d2`

## Конфигурация
- `PORTFOLIO_GITHUB_TOKEN` - опциональный токен для снижения проблем с rate limit GitHub API.
- `PORTFOLIO_ADMIN_ACCESS_CODE` - код доступа в админку (используйте сложное значение).
- `GitHub__ApiBaseUrl` - опциональный override базового URL GitHub API.
  - по умолчанию: `https://api.github.com/`
  - удобно для локальных интеграционных/e2e-тестов с фейковым API-сервером.

## Возможности админки
- настройка GitHub username/token;
- выбор отображаемых репозиториев;
- редактирование блоков About/Contacts;
- настройка подстраниц проектов;
- загрузка медиа для каждого проекта;
- управление порядком, подписями и удалением медиа.

## Rate Limit GitHub API
Если появляется предупреждение о rate limit, добавьте `PORTFOLIO_GITHUB_TOKEN` в `.env` или в поле токена в админке.

## Безопасность
- `.env` игнорируется в git;
- `WebApplication1/Data/portfolio-settings.json` игнорируется в git;
- папка `uploads` игнорируется в git;
- используйте сложный `PORTFOLIO_ADMIN_ACCESS_CODE`.

## Сборка
```bash
dotnet build WebApplication1.sln
```

## Тесты
Запуск всех тестов (unit + Playwright e2e):

```bash
dotnet test WebApplication1.sln -v minimal
```

Установка Playwright Chromium (один раз, Windows PowerShell):

```powershell
powershell -ExecutionPolicy Bypass -File .\WebApplication1.Tests\bin\Debug\net9.0\playwright.ps1 install chromium
```

## Публикация на GitHub
```bash
git remote add origin https://github.com/<USERNAME>/<REPO>.git
git branch -M main
git push -u origin main
```
