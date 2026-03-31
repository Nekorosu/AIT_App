# AIT_App — Электронный журнал (Avalonia + MySQL)

Миграция электронного журнала с WPF/MSSQL на Avalonia UI / MySQL / .NET 9.

---

## Быстрый старт

### 1. База данных
Импортируйте `mysql_schema.sql` на ваш MySQL-сервер (уже выполнено).

### 2. Строка подключения
Скопируйте `config.example.json` → `config.json` и заполните:
```json
{
  "ConnectionString": "Server=192.168.1.X;Port=3306;Database=electronic_journal;User=journal_user;Password=YOURPASSWORD;"
}
```
`config.json` находится в `.gitignore` — никогда не коммитится.

### 3. Запуск
```bash
cd AIT_App
dotnet run
```

### Учётные данные по умолчанию
| Логин | Пароль |
|-------|--------|
| teacher | 2222 |

---

## Структура файлов

```
AIT_App/
  Services/
    ConnectionStringService.cs   ← читает/пишет config.json
    DataBaseCon.cs               ← MySQL ADO.NET обёртка
    ExportService.cs             ← экспорт в Excel (ClosedXML)
  Windows/
    Frontend/                    ← AXAML разметка (дизайнер)
      AuthWindow.axaml
      HomeWindow.axaml
      SettingsWindow.axaml
      UserControls/
        Journal.axaml
        SessionReport.axaml
        PlanSession.axaml
    Backend/                     ← code-behind (кодер)
      AuthWindow/AuthWindow.axaml.cs
      HomeWindow/HomeWindow.axaml.cs
      SettingsWindow/SettingsWindow.axaml.cs
      UserControls/
        Journal/Journal.axaml.cs
        SessionReport/SessionReport.axaml.cs
        PlanSession/PlanSession.axaml.cs
```

---

## Функциональность

| Модуль | Что делает |
|--------|-----------|
| **AuthWindow** | Асинхронная проверка соединения с БД при запуске, загрузка логинов из БД, аутентификация |
| **HomeWindow** | Навигационная оболочка (sidebar + ContentControl) |
| **Journal** | Журнал оценок — pivot-таблица (студенты × даты), добавление/удаление оценок, экспорт в Excel |
| **SessionReport** | Отчёт по сессии — pivot по студентам, таблица успеваемости (средний балл, качество, успеваемость %), экспорт в Excel |
| **PlanSession** | Управление плановыми сессиями (добавить/удалить), вызов `sp_UpdateGradeTypes` |
| **SettingsWindow** | Редактирование строки подключения, кнопка «Проверить соединение» |

---

## Известные задачи (технический долг)

- [ ] Хеширование паролей (сейчас plain text)
- [ ] Fade-анимация между окнами (WindowTransition)
- [ ] Иконки `check.png` / `exclamation.png` — добавить в папку `Icons/`
- [ ] Отличники / должники / кандидаты на красный диплом (фаза 8)
