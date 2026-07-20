using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Ets2SaveEditor.App
{
    /// <summary>RU/EN string table. Mark controls with Uid="key"; ToolTip="@key".</summary>
    public static class Loc
    {
        public static string Language { get; private set; } = "ru";

        private static readonly Dictionary<string, string> Ru = BuildRu();
        private static readonly Dictionary<string, string> En = BuildEn();
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, TipKeyHolder> TipKeys = new();

        private sealed class TipKeyHolder
        {
            public string Key;
        }

        public static void SetLanguage(string lang)
        {
            Language = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ru";
        }

        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            var table = Language == "en" ? En : Ru;
            if (table.TryGetValue(key, out string value)) return value;
            if (Ru.TryGetValue(key, out value)) return value;
            return key;
        }

        public static string Tf(string key, params object[] args)
        {
            try { return string.Format(T(key), args); }
            catch { return T(key); }
        }

        /// <summary>
        /// Applies Uid→Text/Content and ToolTip "@key" across the logical tree.
        /// </summary>
        public static void ApplyTree(DependencyObject root)
        {
            if (root == null) return;
            Walk(root, new HashSet<DependencyObject>());
        }

        private static void Walk(DependencyObject node, HashSet<DependencyObject> seen)
        {
            if (node == null || !seen.Add(node)) return;

            // Only FrameworkElements carry Uid / ToolTip we care about.
            // Never call VisualTreeHelper on non-Visuals (e.g. ColumnDefinition).
            if (node is FrameworkElement fe)
            {
                if (!string.IsNullOrEmpty(fe.Uid))
                {
                    string val = T(fe.Uid);
                    switch (fe)
                    {
                        case TextBlock tb:
                            tb.Text = val;
                            break;
                        case TextBox:
                            break;
                        case ContentControl cc when cc.Content is null || cc.Content is string:
                            cc.Content = val;
                            break;
                    }
                }

                if (fe.ToolTip is string tip)
                {
                    string tipKey = null;
                    if (tip.StartsWith("@", StringComparison.Ordinal))
                    {
                        tipKey = tip.Substring(1);
                        TipKeys.GetOrCreateValue(fe).Key = tipKey;
                    }
                    else if (TipKeys.TryGetValue(fe, out TipKeyHolder holder))
                    {
                        tipKey = holder.Key;
                    }

                    if (!string.IsNullOrEmpty(tipKey))
                        fe.ToolTip = T(tipKey);
                }

                if (fe is ContentPresenter cp && cp.Content is FrameworkElement cpFe)
                    Walk(cpFe, seen);
                else if (fe is ContentControl { Content: FrameworkElement ccFe })
                    Walk(ccFe, seen);

                if (fe is ItemsControl items)
                {
                    foreach (object item in items.Items)
                    {
                        if (item is FrameworkElement itemFe)
                            Walk(itemFe, seen);
                    }
                }
            }

            foreach (object child in LogicalTreeHelper.GetChildren(node))
            {
                if (child is FrameworkElement childFe)
                    Walk(childFe, seen);
            }
        }

        private static Dictionary<string, string> BuildRu() => new(StringComparer.OrdinalIgnoreCase)
        {
            ["nav.economy"] = "Экономика",
            ["nav.map"] = "Карта и Гаражи",
            ["nav.vehicle"] = "Техника",
            ["nav.settings"] = "Настройки",
            ["nav.about"] = "О программе",
            ["nav.economy.tip"] = "Баланс, опыт, навыки водителя и ADR",
            ["nav.map.tip"] = "Разблокировать города и улучшить гаражи",
            ["nav.vehicle.tip"] = "Ремонт грузовика и прицепа",
            ["nav.settings.tip"] = "Язык, тема, пути и дешифратор",
            ["nav.about.tip"] = "Версия, авторы и лицензии",
            ["overlay.close"] = "Закрыть",

            ["hdr.game"] = "ИГРА",
            ["hdr.source"] = "ИСТОЧНИК",
            ["hdr.profile"] = "ПРОФИЛЬ",
            ["hdr.save"] = "СОХРАНЕНИЕ",
            ["hdr.game.tip"] = "Выберите игру: Euro Truck Simulator 2 или American Truck Simulator",
            ["hdr.source.tip"] = "Steam Cloud / steam_profiles или локальные профили из Documents",
            ["hdr.source.steam"] = "Steam",
            ["hdr.source.local"] = "Local",
            ["hdr.profile.tip"] = "Выберите профиль игрока",
            ["hdr.save.tip"] = "Выберите слот сохранения для редактирования",
            ["hdr.refresh.tip"] = "Обновить список профилей и сохранений",
            ["hdr.subtitle"] = "ETS2 / ATS Editor",
            ["hdr.steamid"] = "SteamID64: {0}",
            ["hdr.steamid.tip"] = "SteamID64 аккаунта выбранного Steam-профиля",

            ["savebar.title"] = "Общее сохранение",
            ["savebar.hint"] = "Экономика · навыки · ADR · карта · гаражи · техника",
            ["savebar.btn"] = "СОХРАНИТЬ",
            ["savebar.tip"] = "Записать все изменения в выбранное сохранение",

            ["econ.money.title"] = "Баланс аккаунта",
            ["econ.money.hint"] = "€ для ETS2 · $ для ATS",
            ["econ.money.tip"] = "Текущий баланс. Нажмите «Сохранить» внизу для применения.",
            ["econ.money.set"] = "Установить {0}",
            ["econ.level.title"] = "Уровень и опыт",
            ["econ.level.hint"] = "Уровень считается по XP. Выберите уровень — очки подставятся сами.",
            ["econ.xp.title"] = "Очки опыта (XP)",
            ["econ.xp.tip"] = "Ур.10 = 11 100 XP  ·  Ур.50 = 235 700 XP  ·  Ур.100 = 575 700 XP  ·  Ур.150 = 915 700 XP",
            ["econ.xp.lvl10"] = "Ур. 10",
            ["econ.xp.lvl25"] = "Ур. 25",
            ["econ.xp.lvl50"] = "Ур. 50",
            ["econ.xp.lvl100"] = "Ур. 100",
            ["econ.xp.lvl150"] = "Ур. 150",
            ["econ.level"] = "Уровень",
            ["econ.level.tip"] = "Выберите уровень — XP подставится автоматически",
            ["econ.adr.title"] = "Лицензии ADR",
            ["econ.adr.hint"] = "Допуск к перевозке опасных грузов по классам",
            ["econ.adr.0"] = "Класс 1 — Взрывчатые",
            ["econ.adr.0.tip"] = "Взрывчатка, боеприпасы, фейерверки",
            ["econ.adr.1"] = "Класс 2 — Газы",
            ["econ.adr.1.tip"] = "Сжатые и сжиженные газы",
            ["econ.adr.2"] = "Класс 3 — Легковоспламеняющиеся жидкости",
            ["econ.adr.2.tip"] = "Бензин, спирт, растворители",
            ["econ.adr.3"] = "Класс 4 — Легковоспламеняющиеся твёрдые",
            ["econ.adr.3.tip"] = "Спички, металлические порошки",
            ["econ.adr.4"] = "Класс 6 — Токсичные",
            ["econ.adr.4.tip"] = "Яды, инфекционные вещества",
            ["econ.adr.5"] = "Класс 8 — Коррозионные",
            ["econ.adr.5.tip"] = "Кислоты, щёлочи",
            ["econ.skills.title"] = "Навыки водителя",
            ["econ.skills.max.tip"] = "Поставить все навыки на уровень 6 (максимум)",
            ["econ.skills.hint"] = "Уровень 0–6. Каждый уровень открывает бонус к соответствующим заказам.",
            ["econ.skill.dist"] = "Дальние дистанции",
            ["econ.skill.dist.tip"] = "Бонус к длинным маршрутам и заработку за пробег",
            ["econ.skill.fragile"] = "Хрупкие грузы",
            ["econ.skill.fragile.tip"] = "Бонус к перевозке хрупких и деликатных грузов",
            ["econ.skill.urgent"] = "Срочные заказы",
            ["econ.skill.urgent.tip"] = "Бонус к срочным и экспресс-доставкам",
            ["econ.skill.eco"] = "Эко-вождение",
            ["econ.skill.eco.tip"] = "Бонус за экономичный расход топлива",
            ["econ.skill.valuable"] = "Дорогие грузы",
            ["econ.skill.valuable.tip"] = "Навык High Value / heavy в сохранении (0 – 6)",
            ["econ.skill.slider"] = "Уровень навыка (0 – 6)",

            ["map.mode.simple"] = "Простой режим",
            ["map.mode.advanced"] = "Продвинутый режим",
            ["map.mode.hint"] = "Простой — всё сразу · Продвинутый — по городам",
            ["map.unlock.cities"] = "Разблокировать все города",
            ["map.unlock.cities.hint"] = "Отметить все города с гаражами как посещённые. Применяется общей кнопкой «Сохранить».",
            ["map.unlock.garages"] = "Купить и улучшить все гаражи",
            ["map.unlock.garages.hint"] = "Купить все гаражи и расширить до Large (5 слотов). Применяется общей кнопкой «Сохранить».",
            ["map.apply.tip"] = "Применится при нажатии «Сохранить» внизу",
            ["map.cities.title"] = "Список городов",
            ["map.cities.search.tip"] = "Поиск городов",
            ["map.cities.search.ph"] = "Поиск города...",
            ["map.cities.select"] = "Выбрать все",
            ["map.cities.clear"] = "Снять все",
            ["map.cities.empty"] = "Загрузите сохранение,\nчтобы увидеть города",
            ["map.cities.none"] = "Нет городов в этом сохранении",
            ["map.cities.nofilter"] = "Ничего не найдено",
            ["map.garages.title"] = "Список гаражей",
            ["map.garages.search.tip"] = "Поиск гаражей",
            ["map.garages.search.ph"] = "Поиск гаража...",
            ["map.garages.large"] = "Все → Large",
            ["map.garages.sell"] = "Распродать все",
            ["map.garages.empty"] = "Загрузите сохранение,\nчтобы увидеть гаражи",
            ["map.garages.none"] = "Нет гаражей в этом сохранении",
            ["map.garages.nofilter"] = "Ничего не найдено",
            ["map.footer"] = "Изменения списка городов и гаражей применятся общей кнопкой «Сохранить» внизу.",

            ["veh.hint"] = "Выберите грузовик или прицеп из списка, отметьте что починить. Изменения — кнопкой «Сохранить» внизу.",
            ["veh.list.trucks"] = "Грузовики",
            ["veh.list.trailers"] = "Прицепы",
            ["veh.truck"] = "Грузовик",
            ["veh.truck.all"] = "Починить всё + заправить",
            ["veh.truck.all.tip"] = "Отметить все элементы грузовика и топливо",
            ["veh.trailer"] = "Прицеп",
            ["veh.trailer.all"] = "Починить всё",
            ["veh.trailer.all.tip"] = "Отметить все элементы прицепа",
            ["veh.cabin"] = "Кабина",
            ["veh.chassis"] = "Шасси",
            ["veh.engine"] = "Двигатель",
            ["veh.transmission"] = "Трансмиссия",
            ["veh.wheels"] = "Колёса",
            ["veh.fuel"] = "Топливо",
            ["veh.body"] = "Кузов",
            ["veh.repair.cabin"] = "Починить кабину",
            ["veh.repair.chassis"] = "Починить шасси",
            ["veh.repair.engine"] = "Починить двигатель",
            ["veh.repair.transmission"] = "Починить трансмиссию",
            ["veh.repair.wheels"] = "Починить колёса",
            ["veh.repair.body"] = "Починить кузов",
            ["veh.refuel"] = "Заправить",

            ["settings.path.title"] = "Папка с сохранениями",
            ["settings.path.hint"] = "По умолчанию программа автоматически находит сохранения в «Мои документы» и Steam Cloud. Если игра использует нестандартную папку — укажите путь вручную.",
            ["settings.path.tip"] = "Путь к папке, содержащей папку profiles/ вашей игры",
            ["settings.decrypt.title"] = "Внешний дешифратор",
            ["settings.decrypt.badge"] = "Необязательно",
            ["settings.decrypt.hint"] = "Если встроенный дешифратор выдаёт ошибку после обновления игры, скачайте SII_Decrypt.exe и укажите путь к нему.",
            ["settings.decrypt.tip"] = "Путь к SII_Decrypt.exe (опционально)",
            ["settings.browse"] = "Обзор...",
            ["settings.lang.title"] = "Язык интерфейса",
            ["settings.lang.hint"] = "Язык меняется сразу. Чтобы запомнить выбор — нажмите «Сохранить настройки».",
            ["settings.theme.title"] = "Цветовая схема программы",
            ["settings.theme.hint"] = "Цвет меняется сразу. Чтобы запомнить выбор — нажмите «Сохранить настройки».",
            ["settings.theme.tip"] = "Выберите основной цвет оформления",
            ["settings.dirty.hint"] = "Изменения не приняты",
            ["settings.dirty.save"] = "Сохранить настройки",
            ["settings.dirty.save.tip"] = "Записать настройки в settings.ini и применить",
            ["theme.cyan"] = "Бирюзовый (Cyber Cyan)",
            ["theme.purple"] = "Фиолетовый (Neon Purple)",
            ["theme.green"] = "Зеленый (Cyber Green)",
            ["theme.blue"] = "Синий (Electric Blue)",
            ["theme.orange"] = "Оранжевый (Sunset Orange)",
            ["theme.red"] = "Красный (Imperial Red)",

            ["about.desc"] = "Редактор сейвов для Euro Truck Simulator 2 и American Truck Simulator. Позволяет изменять баланс, опыт, навыки, разблокировать города и управлять гаражами без ручного редактирования файлов.",
            ["about.authors"] = "Авторы",
            ["about.lead"] = "Основной разработчик",
            ["about.libs"] = "Используемые библиотеки",
            ["about.net"] = "Microsoft — фреймворк приложения и UI",
            ["about.zlib"] = "Jean-loup Gailly & Mark Adler — сжатие/распаковка сейвов",
            ["about.sii.title"] = "SII Decrypt (концепт)",
            ["about.sii"] = "Open-source сообщество — алгоритм декодирования SII",
            ["about.update.title"] = "Актуальная версия",
            ["about.update.hint"] = "Нажмите, чтобы проверить обновления на GitHub",
            ["about.update.btn"] = "Проверить обновление",
            ["about.update.tip"] = "Сверить версию с последним релизом на GitHub",
            ["about.update.checking"] = "Проверка обновлений…",
            ["about.update.latest"] = "У вас актуальная версия ({0})",
            ["about.update.available"] = "Доступна версия {0}",
            ["about.update.none"] = "На GitHub пока нет опубликованных релизов",
            ["about.update.error"] = "Не удалось проверить обновления: {0}",
            ["about.update.download"] = "Скачать и установить",
            ["about.update.downloading"] = "Скачивание… {0:0}%",
            ["about.update.installing"] = "Установка и перезапуск…",
            ["about.update.noasset"] = "В релизе нет файла .exe. Откройте страницу релиза в браузере.",
            ["about.update.open"] = "Открыть релиз",

            ["splash.tagline"] = "Save Editor",
            ["firstrun.welcome"] = "Добро пожаловать",
            ["firstrun.subtitle"] = "Выберите язык и цветовую тему — это займёт пару секунд.",
            ["firstrun.lang"] = "Язык",
            ["firstrun.theme"] = "Тема",
            ["firstrun.continue"] = "Продолжить",
            ["firstrun.ru"] = "Русский",
            ["firstrun.en"] = "English",

            ["nosave.title"] = "У вас нет сохранения",
            ["nosave.hint"] = "Выберите игру, профиль и слот в верхней панели — после этого раздел станет доступен.",
            ["nosave.cta"] = "Выбрать сохранение",
            ["nosave.tip"] = "Открыть список сохранений в шапке",

            ["dialog.ok"] = "OK",
            ["dialog.success"] = "Успех",
            ["dialog.error"] = "Ошибка",
            ["dialog.warning"] = "Предупреждение",
            ["dialog.info"] = "Информация",
            ["dialog.need.save"] = "Сначала выберите сохранение!",
            ["dialog.bad.money"] = "Некорректное значение баланса!",
            ["dialog.bad.xp"] = "Некорректное значение опыта!",
            ["dialog.profiles.err"] = "Ошибка при поиске профилей:\n{0}",
            ["dialog.read.err"] = "Ошибка при чтении файла сохранения:\n{0}",
            ["dialog.write.err"] = "Ошибка при записи в файл сохранения:\n{0}",
            ["dialog.saved"] = "Изменения записаны в game.sii",
            ["dialog.repair.done"] = "Ремонт выполнен",

            ["status.scan"] = "Сканирование профилей...",
            ["status.ready"] = "Готов к работе",
            ["status.saves"] = "Поиск сохранений...",
            ["status.loading"] = "Загрузка файла...",
            ["status.decrypt.err"] = "Ошибка дешифрования",
            ["status.loaded"] = "Загружено",
            ["status.parse.err"] = "Ошибка разбора",
            ["status.writing"] = "Запись...",
            ["status.saved"] = "Сохранено",
            ["status.write.err"] = "Ошибка записи",

            ["browse.folder"] = "Выберите папку с профилями сохранений",
            ["browse.exe.filter"] = "Исполняемые файлы (*.exe)|*.exe",
            ["browse.decrypt"] = "Выберите файл дешифратора",

            ["garage.size.large"] = "Большой (5 мест)",
            ["garage.size.medium"] = "Средний (3 места)",
            ["garage.size.small"] = "Малый (1 место)",
            ["garage.size.none"] = "Не куплен",
            ["garage.level"] = "Уровень {0:F0}",
        };

        private static Dictionary<string, string> BuildEn() => new(StringComparer.OrdinalIgnoreCase)
        {
            ["nav.economy"] = "Economy",
            ["nav.map"] = "Map & Garages",
            ["nav.vehicle"] = "Vehicle",
            ["nav.settings"] = "Settings",
            ["nav.about"] = "About",
            ["nav.economy.tip"] = "Balance, XP, driver skills and ADR",
            ["nav.map.tip"] = "Unlock cities and upgrade garages",
            ["nav.vehicle.tip"] = "Repair truck and trailer",
            ["nav.settings.tip"] = "Language, theme, paths and decryptor",
            ["nav.about.tip"] = "Version, authors and licenses",
            ["overlay.close"] = "Close",

            ["hdr.game"] = "GAME",
            ["hdr.source"] = "SOURCE",
            ["hdr.profile"] = "PROFILE",
            ["hdr.save"] = "SAVE",
            ["hdr.game.tip"] = "Choose Euro Truck Simulator 2 or American Truck Simulator",
            ["hdr.source.tip"] = "Steam Cloud / steam_profiles or local Documents profiles",
            ["hdr.source.steam"] = "Steam",
            ["hdr.source.local"] = "Local",
            ["hdr.profile.tip"] = "Select a player profile",
            ["hdr.save.tip"] = "Select a save slot to edit",
            ["hdr.refresh.tip"] = "Refresh profiles and saves",
            ["hdr.subtitle"] = "ETS2 / ATS Editor",
            ["hdr.steamid"] = "SteamID64: {0}",
            ["hdr.steamid.tip"] = "SteamID64 of the selected Steam profile",

            ["savebar.title"] = "Save all changes",
            ["savebar.hint"] = "Economy · skills · ADR · map · garages · vehicle",
            ["savebar.btn"] = "SAVE",
            ["savebar.tip"] = "Write all changes into the selected save",

            ["econ.money.title"] = "Account balance",
            ["econ.money.hint"] = "€ for ETS2 · $ for ATS",
            ["econ.money.tip"] = "Current balance. Press Save at the bottom to apply.",
            ["econ.money.set"] = "Set to {0}",
            ["econ.level.title"] = "Level and experience",
            ["econ.level.hint"] = "Level is derived from XP. Pick a level — XP is filled automatically.",
            ["econ.xp.title"] = "Experience points (XP)",
            ["econ.xp.tip"] = "Lv.10 = 11,100 XP  ·  Lv.50 = 235,700 XP  ·  Lv.100 = 575,700 XP  ·  Lv.150 = 915,700 XP",
            ["econ.xp.lvl10"] = "Lv. 10",
            ["econ.xp.lvl25"] = "Lv. 25",
            ["econ.xp.lvl50"] = "Lv. 50",
            ["econ.xp.lvl100"] = "Lv. 100",
            ["econ.xp.lvl150"] = "Lv. 150",
            ["econ.level"] = "Level",
            ["econ.level.tip"] = "Pick a level — XP is filled automatically",
            ["econ.adr.title"] = "ADR licenses",
            ["econ.adr.hint"] = "Permit for dangerous goods by class",
            ["econ.adr.0"] = "Class 1 — Explosives",
            ["econ.adr.0.tip"] = "Explosives, ammunition, fireworks",
            ["econ.adr.1"] = "Class 2 — Gases",
            ["econ.adr.1.tip"] = "Compressed and liquefied gases",
            ["econ.adr.2"] = "Class 3 — Flammable liquids",
            ["econ.adr.2.tip"] = "Petrol, alcohol, solvents",
            ["econ.adr.3"] = "Class 4 — Flammable solids",
            ["econ.adr.3.tip"] = "Matches, metal powders",
            ["econ.adr.4"] = "Class 6 — Toxic",
            ["econ.adr.4.tip"] = "Poisons, infectious substances",
            ["econ.adr.5"] = "Class 8 — Corrosive",
            ["econ.adr.5.tip"] = "Acids, alkalis",
            ["econ.skills.title"] = "Driver skills",
            ["econ.skills.max.tip"] = "Set all skills to level 6 (max)",
            ["econ.skills.hint"] = "Level 0–6. Each level unlocks bonuses for matching jobs.",
            ["econ.skill.dist"] = "Long distance",
            ["econ.skill.dist.tip"] = "Bonus for long routes and mileage pay",
            ["econ.skill.fragile"] = "Fragile cargo",
            ["econ.skill.fragile.tip"] = "Bonus for fragile and delicate cargo",
            ["econ.skill.urgent"] = "Just-in-time",
            ["econ.skill.urgent.tip"] = "Bonus for urgent and express deliveries",
            ["econ.skill.eco"] = "Eco driving",
            ["econ.skill.eco.tip"] = "Bonus for fuel-efficient driving",
            ["econ.skill.valuable"] = "High value",
            ["econ.skill.valuable.tip"] = "High Value / heavy skill in the save (0 – 6)",
            ["econ.skill.slider"] = "Skill level (0 – 6)",

            ["map.mode.simple"] = "Simple mode",
            ["map.mode.advanced"] = "Advanced mode",
            ["map.mode.hint"] = "Simple — everything at once · Advanced — per city",
            ["map.unlock.cities"] = "Unlock all cities",
            ["map.unlock.cities.hint"] = "Mark all garage cities as visited. Applied with the main Save button.",
            ["map.unlock.garages"] = "Buy & upgrade all garages",
            ["map.unlock.garages.hint"] = "Buy all garages and expand to Large (5 slots). Applied with the main Save button.",
            ["map.apply.tip"] = "Applied when you press Save at the bottom",
            ["map.cities.title"] = "Cities",
            ["map.cities.search.tip"] = "Search cities",
            ["map.cities.search.ph"] = "Search city...",
            ["map.cities.select"] = "Select all",
            ["map.cities.clear"] = "Clear all",
            ["map.cities.empty"] = "Load a save\nto see cities",
            ["map.cities.none"] = "No cities in this save",
            ["map.cities.nofilter"] = "Nothing found",
            ["map.garages.title"] = "Garages",
            ["map.garages.search.tip"] = "Search garages",
            ["map.garages.search.ph"] = "Search garage...",
            ["map.garages.large"] = "All → Large",
            ["map.garages.sell"] = "Sell all",
            ["map.garages.empty"] = "Load a save\nto see garages",
            ["map.garages.none"] = "No garages in this save",
            ["map.garages.nofilter"] = "Nothing found",
            ["map.footer"] = "City and garage list changes are applied with the main Save button at the bottom.",

            ["veh.hint"] = "Select a truck or trailer from the list, tick what to repair. Changes apply with Save at the bottom.",
            ["veh.list.trucks"] = "Trucks",
            ["veh.list.trailers"] = "Trailers",
            ["veh.truck"] = "Truck",
            ["veh.truck.all"] = "Repair all + refuel",
            ["veh.truck.all.tip"] = "Select all truck parts and fuel",
            ["veh.trailer"] = "Trailer",
            ["veh.trailer.all"] = "Repair all",
            ["veh.trailer.all.tip"] = "Select all trailer parts",
            ["veh.cabin"] = "Cabin",
            ["veh.chassis"] = "Chassis",
            ["veh.engine"] = "Engine",
            ["veh.transmission"] = "Transmission",
            ["veh.wheels"] = "Wheels",
            ["veh.fuel"] = "Fuel",
            ["veh.body"] = "Body",
            ["veh.repair.cabin"] = "Repair cabin",
            ["veh.repair.chassis"] = "Repair chassis",
            ["veh.repair.engine"] = "Repair engine",
            ["veh.repair.transmission"] = "Repair transmission",
            ["veh.repair.wheels"] = "Repair wheels",
            ["veh.repair.body"] = "Repair body",
            ["veh.refuel"] = "Refuel",

            ["settings.path.title"] = "Custom saves folder",
            ["settings.path.hint"] = "By default the app finds saves in Documents and Steam Cloud. If the game uses a custom folder, set the path manually.",
            ["settings.path.tip"] = "Path to the folder that contains your game profiles/ directory",
            ["settings.decrypt.title"] = "External decryptor",
            ["settings.decrypt.badge"] = "Optional",
            ["settings.decrypt.hint"] = "If the built-in decryptor fails after a game update, download SII_Decrypt.exe and point to it here.",
            ["settings.decrypt.tip"] = "Path to SII_Decrypt.exe (optional)",
            ["settings.browse"] = "Browse...",
            ["settings.lang.title"] = "Interface language",
            ["settings.lang.hint"] = "Language changes immediately. Press Save settings to keep it.",
            ["settings.theme.title"] = "Accent color theme",
            ["settings.theme.hint"] = "Color changes immediately. Press Save settings to keep it.",
            ["settings.theme.tip"] = "Choose the main accent color",
            ["settings.dirty.hint"] = "Changes are not applied",
            ["settings.dirty.save"] = "Save settings",
            ["settings.dirty.save.tip"] = "Write settings to settings.ini and apply",
            ["theme.cyan"] = "Cyan (Cyber Cyan)",
            ["theme.purple"] = "Purple (Neon Purple)",
            ["theme.green"] = "Green (Cyber Green)",
            ["theme.blue"] = "Blue (Electric Blue)",
            ["theme.orange"] = "Orange (Sunset Orange)",
            ["theme.red"] = "Red (Imperial Red)",

            ["about.desc"] = "Save editor for Euro Truck Simulator 2 and American Truck Simulator. Change balance, XP, skills, unlock cities and manage garages without hand-editing files.",
            ["about.authors"] = "Authors",
            ["about.lead"] = "Lead developer",
            ["about.libs"] = "Libraries used",
            ["about.net"] = "Microsoft — application framework and UI",
            ["about.zlib"] = "Jean-loup Gailly & Mark Adler — save compression",
            ["about.sii.title"] = "SII Decrypt (concept)",
            ["about.sii"] = "Open-source community — SII decode algorithm",
            ["about.update.title"] = "Latest version",
            ["about.update.hint"] = "Click to check for updates on GitHub",
            ["about.update.btn"] = "Check for updates",
            ["about.update.tip"] = "Compare with the latest GitHub release",
            ["about.update.checking"] = "Checking for updates…",
            ["about.update.latest"] = "You have the latest version ({0})",
            ["about.update.available"] = "Version {0} is available",
            ["about.update.none"] = "No published releases on GitHub yet",
            ["about.update.error"] = "Could not check for updates: {0}",
            ["about.update.download"] = "Download and install",
            ["about.update.downloading"] = "Downloading… {0:0}%",
            ["about.update.installing"] = "Installing and restarting…",
            ["about.update.noasset"] = "No .exe in the release. Open the release page in your browser.",
            ["about.update.open"] = "Open release",

            ["splash.tagline"] = "Save Editor",
            ["firstrun.welcome"] = "Welcome",
            ["firstrun.subtitle"] = "Pick a language and accent theme — it only takes a moment.",
            ["firstrun.lang"] = "Language",
            ["firstrun.theme"] = "Theme",
            ["firstrun.continue"] = "Continue",
            ["firstrun.ru"] = "Русский",
            ["firstrun.en"] = "English",

            ["nosave.title"] = "No save selected",
            ["nosave.hint"] = "Pick a game, profile and save slot in the top bar to unlock this section.",
            ["nosave.cta"] = "Choose a save",
            ["nosave.tip"] = "Open the save list in the header",

            ["dialog.ok"] = "OK",
            ["dialog.success"] = "Success",
            ["dialog.error"] = "Error",
            ["dialog.warning"] = "Warning",
            ["dialog.info"] = "Info",
            ["dialog.need.save"] = "Select a save first!",
            ["dialog.bad.money"] = "Invalid balance value!",
            ["dialog.bad.xp"] = "Invalid experience value!",
            ["dialog.profiles.err"] = "Failed to scan profiles:\n{0}",
            ["dialog.read.err"] = "Failed to read the save file:\n{0}",
            ["dialog.write.err"] = "Failed to write the save file:\n{0}",
            ["dialog.saved"] = "Changes written to game.sii",
            ["dialog.repair.done"] = "Repair applied",

            ["status.scan"] = "Scanning profiles...",
            ["status.ready"] = "Ready",
            ["status.saves"] = "Looking for saves...",
            ["status.loading"] = "Loading file...",
            ["status.decrypt.err"] = "Decrypt error",
            ["status.loaded"] = "Loaded",
            ["status.parse.err"] = "Parse error",
            ["status.writing"] = "Writing...",
            ["status.saved"] = "Saved",
            ["status.write.err"] = "Write error",

            ["browse.folder"] = "Select the folder with save profiles",
            ["browse.exe.filter"] = "Executable files (*.exe)|*.exe",
            ["browse.decrypt"] = "Select decryptor executable",

            ["garage.size.large"] = "Large (5 slots)",
            ["garage.size.medium"] = "Medium (3 slots)",
            ["garage.size.small"] = "Small (1 slot)",
            ["garage.size.none"] = "Not owned",
            ["garage.level"] = "Level {0:F0}",
        };
    }
}
