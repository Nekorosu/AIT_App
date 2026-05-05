# Редизайн UI — инструкция по интеграции

## Что внутри

Полная замена визуального слоя проекта. Логика приложения и работы с БД **не тронута** — кроме двух вещей:

1. `Journal.axaml.cs` — добавлена обработка трёх pill-кнопок (`BtnTypeAll`, `BtnTypeCurrent`, `BtnTypeExam`) которые подменяют скрытый `TypeCombo`. Существующая логика загрузки журнала использует `TypeCombo` как раньше.
2. `SessionReport.axaml.cs` — pivot-колонки оценок теперь рендерятся через `DataGridTemplateColumn` с цветным бейджем (раньше были `DataGridTextColumn`). Логика построения pivot, performance-таблицы и экспорта не изменилась.

`HomeWindow.axaml.cs` тоже обновлён, так как добавились новые элементы в разметке (декоративные иконки в узком сайдбаре, аватарка пользователя, метка роли) и подсветка активного раздела через CSS-класс `active`.

## Файловая структура

```
AIT_App/
  App.axaml                                                 ← заменить (дизайн-система: цвета, стили, кисти оценок)
  Services/
    GradeColorConverter.cs                                  ← НОВЫЙ файл (конвертеры для цвета бейджей)
  Windows/Backend/
    AuthWindow/AuthWindow.axaml                             ← заменить
    HomeWindow/HomeWindow.axaml                             ← заменить
    HomeWindow/HomeWindow.axaml.cs                          ← заменить
    SettingsWindow/SettingsWindow.axaml                     ← заменить
    UserControls/
      Journal/Journal.axaml                                 ← заменить
      Journal/Journal.axaml.cs                              ← заменить
      PlanSession/PlanSession.axaml                         ← заменить
      Reports/Reports.axaml                                 ← заменить
      SessionReport/SessionReport.axaml                     ← заменить
      SessionReport/SessionReport.axaml.cs                  ← заменить
      Students/Students.axaml                               ← заменить
      Teachers/Teachers.axaml                               ← заменить
```

`*.axaml.cs` файлы для тех, кого я не трогал (`AuthWindow.axaml.cs`, `PlanSession.axaml.cs`, `Reports.axaml.cs`, `SettingsWindow.axaml.cs`, `Students.axaml.cs`, `Teachers.axaml.cs`) **не меняются** — их оставьте как есть. Все `x:Name` в новых .axaml совпадают со старыми, обработчики событий не пострадают.

## Дизайн-система

Все цвета и стили централизованы в `App.axaml`. Если захотите подкрутить — правьте только там.

**Палитра:**
- `BgAppBrush` — фон рабочей области (`#F4F5F8`)
- `BgCardBrush` — фон карточек (`#FFFFFF`)
- `BgSidebarBrush` / `BgSidebarNarrowBrush` — тёмный синий сайдбар
- `AccentPrimaryBrush` — основной тёмно-синий (`#1E2240`) для кнопок
- `AccentSoftBrush` — светло-голубой для активных pill/section кнопок (`#E0E7FF`)

**Цвета оценок (по вашему запросу):**
- `5` — зелёный (`#DCFCE7` фон + `#15803D` текст)
- `4` и `3` — жёлтый (`#FEF9C3` фон + `#A16207` текст)
- `2` — красный (`#FEE2E2` фон + `#B91C1C` текст)
- `Н` — серый (`#F3F4F6` фон + `#6B7280` текст)

**Классы кнопок:**
- `Classes="primary"` — главная тёмно-синяя
- `Classes="danger"` — красная (для удаления)
- `Classes="pill"` + `Classes="active"` — переключатели фильтра
- `Classes="section"` — кнопки списка разделов в сайдбаре
- `Classes="iconNav"` — квадратные иконки в узком сайдбаре

## Что осталось вне scope (по нашим договорённостям)

- **Логика журнала** не переделывалась под pivot — таблица остаётся плоской (одна оценка = одна строка).
- **Поиск в сайдбаре** — декоративный, не функциональный (как и было решено).
- **Иконочный сайдбар** — задел на «суперапп», все иконки кроме «Настроек» и «Выхода» пока без действий.
- Иконки нарисованы через `<Path>` с SVG-геометрией, так что зависимостей не добавилось.

## Замечания по сборке

Дополнительные NuGet-пакеты не нужны — всё работает на тех же `Avalonia 11.3.10`, `Avalonia.Controls.DataGrid`, `MySqlConnector`, `ClosedXML`.

Если Rider начнёт ругаться на `$parent[Button]` биндинги внутри `<Path>` — это кросс-элементные биндинги, они работают независимо от DataContext, ошибок быть не должно. Если всё-таки появятся варнинги об отсутствии `x:DataType` — добавьте `x:CompileBindings="False"` в корневой тег соответствующего файла.
