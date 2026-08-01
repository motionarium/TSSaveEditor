# TSSaveEditor

Редактор сохранений для **Euro Truck Simulator 2** и **American Truck Simulator**.

Десктопное WPF-приложение на **.NET 8**. Сборка: `TSSaveEditor.exe` (**v1.2.0**).

> Репозиторий: [motionarium/TSSaveEditor](https://github.com/motionarium/TSSaveEditor)  
> Скачать: [GitHub Releases](https://github.com/motionarium/TSSaveEditor/releases/latest)

---

## Что умеет

### Экономика
- Баланс аккаунта (быстрые пресеты 1M–100M)
- Опыт (XP) и уровень игрока (синхронизация XP ↔ уровень)
- Навыки водителя 0–6 (дальние, хрупкие, срочные, эко, дорогие грузы) + кнопка MAX
- Лицензии ADR по классам

### Карта и гаражи
- Автоопределение карты из профиля/сейва: `map_path` + `active_mods` / `dependencies`
- Поиск `.scs` в папке модов (и `base_map.scs` / `base.scs` для ванили)
- Список городов с поиском, выбором / снятием всех
- Список гаражей со статусом и уровнем (вплоть до Large)
- Опции: открыть все города карты · открыть туман (`discovered_items`)
- Устойчивое чтение модовых карт (битые/неизвестные item types не валят весь скан)
- Прогресс-бар при сканировании карты и модов

### Техника
- Списки грузовиков и прицепов из сейва
- Отображение модели, номера и **типа номера** (часть после `|` в `license_plate`, например `russia`, `germany`)
- Отметка активного в слоте (★)
- Износ узлов, точечный или полный ремонт, заправка

### Моды (просмотр)
- Скан `Documents\…\mod` (+ кэш `mods_cache.json` рядом с exe)
- Два списка: все моды в папке / активные в сейве
- Версия игры из `Documents\Euro Truck Simulator 2\game.log.txt` (или ATS)
- Фильтр «только совместимые» по `compatible_versions` из манифеста
- Нормализация «кривых» имён (`\xNN`, zero-width пробелы)
- Подписи Steam Workshop без длинных `mod_workshop_package.…`

### Профили и сейвы
- Источники: **Steam** (Cloud `userdata\…` + `steam_profiles`) и **Local** (`profiles` + свой путь)
- Выбор игры ETS2 / ATS, профиля и слота
- SteamID64 для Steam-сейвов
- Плашка «Сохранить» только при несохранённых изменениях

### Интерфейс и настройки
- Язык **RU / EN**
- Акцентные темы с плавной сменой
- Тёмный UI, фиксированное окно, разделы на всю высоту рабочей области
- Проверка обновлений с GitHub Releases

---

## Требования

| | |
|---|---|
| ОС | Windows 10 / 11 (x64) |
| Runtime | [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Игры | ETS2 и/или ATS (сейвы в Documents) |

Приложение **framework-dependent** (single-file ~5 MB): runtime на машине пользователя обязателен.

---

## Установка

1. Скачайте `TSSaveEditor.exe` из [Releases](https://github.com/motionarium/TSSaveEditor/releases/latest).
2. Положите exe в любую папку (права на запись желательны — для `settings.ini` и кэша модов).
3. Убедитесь, что установлен **.NET 8 Desktop Runtime**.
4. Запустите. Выберите игру → источник → профиль → слот.

Перед правками сделайте бэкап сейва (редактор тоже пишет `.bak`, но свой архив надёжнее).

---

## Как пользоваться (кратко)

1. **Шапка** — игра, Steam/Local, профиль, слот.
2. **Экономика / Карта / Техника / Моды** — правки в соответствующих вкладках.
3. Внизу — **Сохранить**, когда есть изменения.
4. **Настройки** — язык, тема, пути, decryptor.
5. **О программе** — версия, лицензии, проверка обновлений.

### Где редактор ищет файлы

| Что | Типичный путь |
|-----|----------------|
| Сейвы Local | `Documents\Euro Truck Simulator 2\profiles\…\save\…` |
| Сейвы Steam (Documents) | `Documents\Euro Truck Simulator 2\steam_profiles\…` |
| Steam Cloud | `…\Steam\userdata\<id>\227300\…` (ETS2) / `270880` (ATS) |
| Моды | `Documents\Euro Truck Simulator 2\mod\` |
| Версия игры | `Documents\Euro Truck Simulator 2\game.log.txt` |

Для ATS те же пути с `American Truck Simulator`. Поддерживаются зеркала OneDrive Documents.

---

## Структура репозитория

```
Ets2SaveEditor.sln
Ets2SaveEditor.App/          # WPF UI (сборка TSSaveEditor)
Ets2SaveEditor.Core/         # сейвы, профили, флот, карта, моды
deps/                        # вендор TruckLib (+ HashFs / Models / Sii / Core)
.github/workflows/release.yml
LICENSE
README.md
CHANGELOG.md
zlib.dll                     # native zlib для сжатия сейвов
```

В git **не** входят: `bin/`, `obj/`, `release/`, `SAVE/` (локальные дампы), `settings.ini`, `mods_cache.json`.

---

## Сборка из исходников

```bash
dotnet build Ets2SaveEditor.sln -c Release
```

### Single-file exe (как в релизе)

```bash
dotnet publish Ets2SaveEditor.App/Ets2SaveEditor.App.csproj ^
    -c Release -r win-x64 --self-contained false ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugType=None -p:DebugSymbols=false ^
    -o release
```

Результат: `release/TSSaveEditor.exe`.

### Релиз на GitHub

```bash
git tag v1.2.0
git push origin v1.2.0
```

Workflow по тегу `v*` соберёт exe и приложит его к GitHub Release.

---

## Безопасность сейвов

- Перед записью: `game.sii.bak` и timestamped-копии
- Атомарная запись через `.tmp`
- При апгрейде гаражей сохраняются ID техники/водителей в слотах
- Внешний decryptor работает по временной копии файла

Используйте на свой страх и риск. Всегда держите отдельный бэкап важных профилей.

---

## Зависимости и лицензии

| Компонент | Назначение | Лицензия |
|-----------|------------|----------|
| .NET 8 + WPF (Microsoft) | runtime / UI | MIT |
| zlib 1.3 | сжатие сейвов | zlib |
| SIIDecryptSharp | дешифровка SII | MIT |
| TruckLib (+ HashFs / Models / Sii / Core) | чтение `.scs` / `.mbd` | **GPL-2.0** |

Сам проект — **[MIT](LICENSE)** © 2026 motionarium.

Из‑за **TruckLib (GPL-2.0)** при распространении бинарника учитывайте требования GPL (в т.ч. доступность исходников связанных частей).

Игровые архивы `.scs` и ассеты SCS Software **не** входят в репозиторий и не должны распространяться вместе с программой.

---

## История версий

См. **[CHANGELOG.md](CHANGELOG.md)**.

---

## Участие ИИ

- **Google Gemini** — ранняя генерация и прототип
- **Cursor** (Composer / Auto) — UI, Core, карта/моды, релизная полировка

---

## Обратная связь

Issues и обсуждения: [github.com/motionarium/TSSaveEditor](https://github.com/motionarium/TSSaveEditor).
