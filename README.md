# TSSaveEditor (ETS2 / ATS Save Editor)

Редактор сохранений для **Euro Truck Simulator 2** и **American Truck Simulator**.  
WPF-приложение на **.NET 8**, сборка `TSSaveEditor` (v1.1.0).

## Возможности

- **Экономика** — баланс, XP / уровень, навыки водителя, ADR
- **Карта и гаражи** — простой режим (всё сразу) и продвинутый (по городам)
- **Техника** — список грузовиков/прицепов, износ, ремонт, топливо
- **Источники профилей** — фильтр **Steam** / **Local**
  - Steam: Steam Cloud (`userdata\…`) и `Documents\…\steam_profiles`
  - Local: обычные `Documents\…\profiles` (+ custom path из настроек)
- **SteamID64** — показывается под профилем для выбранного Steam-сейва
- **Плашка сохранения** — только при несохранённых изменениях
- **Настройки** — язык (RU/EN), тема акцента с плавной сменой, пути, decryptor
- **Первый запуск** — выбор языка и темы
- **Обновления** — проверка и установка новой версии из [GitHub Releases](https://github.com/motionarium/TSSaveEditor/releases)
- Тёмный UI, динамическая иконка окна под цвет темы

## Скачать

Актуальный `TSSaveEditor.exe`: **[Releases](https://github.com/motionarium/TSSaveEditor/releases/latest)**

## Требования

- Windows 10/11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## Структура репозитория

```
Ets2SaveEditor.sln
README.md
LICENSE
.gitignore
zlib.dll
Ets2SaveEditor.App/               # WPF UI (TSSaveEditor)
Ets2SaveEditor.Core/              # профили, decrypt, парсинг/запись
.github/workflows/release.yml     # автосборка exe в GitHub Releases по тегу
```

Сборки (`bin/`, `obj/`, `publish/`, `release/`) в git не входят.

## Сборка

```bash
dotnet build Ets2SaveEditor.sln -c Release
```

### Single-file exe (без вшитого .NET)

```bash
dotnet publish Ets2SaveEditor.App/Ets2SaveEditor.App.csproj ^
    -c Release -r win-x64 --self-contained false ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugType=None -p:DebugSymbols=false ^
    -o release
```

Готовый файл: `release/TSSaveEditor.exe`. На машине пользователя нужен .NET 8 Desktop Runtime.

### Релиз на GitHub

```bash
git tag v1.2.0
git push origin v1.2.0
```

Workflow `.github/workflows/release.yml` соберёт `TSSaveEditor.exe` и приложит его к релизу.
## Безопасность сейвов

- Перед записью: `game.sii.bak` + timestamped-копии
- Атомарная запись через `.tmp`
- ID техники/водителей в слотах гаража сохраняются при апгрейде
- Внешний decryptor работает по временной копии

## Используемые библиотеки

| Компонент | Назначение | Лицензия |
|-----------|------------|----------|
| **.NET 8.0 + WPF** (Microsoft) | Фреймворк и UI | MIT |
| **zlib 1.3** (Jean-loup Gailly & Mark Adler) | Сжатие / распаковка сейвов | zlib |
| **SIIDecryptSharp** 1.0.1 | Дешифровка SII | MIT |

## Участие ИИ

- **Google Gemini** — первоначальная генерация и разработка
- **Cursor** (Composer / Auto) — UI, Core, релизная полировка

## Лицензия

Проект распространяется под **[MIT License](LICENSE)** © 2026 motionarium.

Используйте на свой страх и риск. Делайте бэкапы сохранений игры.
