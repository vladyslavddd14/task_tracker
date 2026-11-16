# Модуль Infrastructure (`TaskTracker.Infrastructure`)

Реалізує інтерфейси Application: база даних, репозиторії, токени, хешування, годинник, сид. Залежить від Application, EF Core (Npgsql), BCrypt і `System.IdentityModel.Tokens.Jwt`.

## Структура

```text
Persistence/
  AppDbContext.cs                   DbSet-и та підключення конфігурацій
  EntityConfigurations.cs           Fluent API для всіх сутностей
  EfUnitOfWork.cs                   IUnitOfWork + переклад помилок БД
  DesignTimeDbContextFactory.cs     для `dotnet ef` (до БД не підключається)
  Migrations/                       InitialCreate та знімок моделі
Repositories/Repositories.cs        UserRepository, TaskRepository, TagRepository, CustomFieldRepository
Security/Security.cs                JwtOptions, BcryptPasswordHasher, JwtTokenService, SystemClock
Seed/DemoDataSeeder.cs              демо-користувач із задачами
DatabaseInitializer.cs              міграції при старті + сид
DependencyInjection.cs              AddInfrastructure()
```

## Модель БД

Таблиці: `Users`, `Tasks`, `Subtasks`, `Tags`, `TaskTags`, `StatusTransitions`, `CustomFieldDefinitions`, `CustomFieldValues`. Перелічення зберігаються числами, часові мітки як `timestamptz`, `Deadline` як `date`.

### Індекси
- `Users.Email` унікальний;
- `Tasks (UserId, Status)` та `Tasks (UserId, Deadline)` для списків і «Сьогодні»;
- `Tags (UserId, NormalizedName)` та `CustomFieldDefinitions (UserId, NormalizedName)` унікальні. Колонка `NormalizedName` містить назву в нижньому регістрі, тож унікальність не залежить від регістру без виразових індексів;
- `CustomFieldValues (TaskItemId, CustomFieldDefinitionId)` унікальний.

### Зв'язки та каскади
Видалення користувача каскадно прибирає всі його дані. Видалення задачі каскадно видаляє підзадачі, зв'язки з тегами, переходи та значення полів. Видалення тегу чіпає лише `TaskTags`, а видалення поля його значення. Каскадами займається сама БД, тому видаляти зв'язані дані вручну не треба.

### Ключі
Усі `Guid`-ключі налаштовані `ValueGeneratedNever()`: їх створює домен. Без цього EF Core вважав би нову сутність із заповненим ключем уже наявною і намагався б її оновити.

### Конкурентність
`Tasks` має токен `xmin` (`xid`, `IsConcurrencyToken`). Якщо рядок змінили між читанням і записом, `SaveChanges` кидає `DbUpdateConcurrencyException`.

## `EfUnitOfWork`

`SaveChangesAsync` виконує всі накопичені зміни однією транзакцією та перекладає помилки БД на винятки Application:

| Помилка БД | Результат |
|---|---|
| `DbUpdateConcurrencyException` | `ConflictException` (`409`) |
| `PostgresException` `23505` (унікальне порушення) | `ConflictException` (`409`), наприклад гонка при створенні тега |

## Репозиторії

Кожен запит до задач, тегів і полів фільтрується за `UserId`.

- `TaskRepository.GetAsync` завантажує задачу з підзадачами, тегами, переходами й значеннями полів (`AsSplitQuery`).
- `SearchAsync` застосовує `TaskFilters.Apply` та `ApplyOrder` з Application, рахує загальну кількість, за потреби робить `Skip/Take`, і підвантажує підзадачі та теги без відстеження змін.
- `GetMaxPositionAsync` дає позицію для додавання в кінець колонки; `GetColumnAsync` повертає відстежувані задачі колонки для перенумерації.

## Безпека (`Security`)

- **`BcryptPasswordHasher`:** солоний BCrypt, work factor 11. Сіль зберігається всередині хешу, тож однакові паролі мають різні хеші.
- **`JwtTokenService`:** токен HS256 із claim `sub` (id користувача) та `email`, термін життя `Jwt:ExpiresMinutes`.
- **`JwtOptions`:** `Secret`, `Issuer`, `Audience`, `ExpiresMinutes`. У `AddInfrastructure` налаштування валідуються при старті: секрет має містити щонайменше 32 символи, інакше застосунок не запуститься з чітким повідомленням. Секрету за замовчуванням немає.
- **`SystemClock`:** `DateTime.UtcNow`.

## `DatabaseInitializer`

При старті API:
1. застосовує невиконані міграції (`Database.MigrateAsync`). Повторний запуск безпечний (ідемпотентно);
2. якщо БД ще не готова, повторює спробу до 10 разів із паузою 3 с (контейнер БД може стартувати повільніше за API);
3. якщо `SEED_DEMO=true`, викликає сид. Тоді обов'язкові `SEED_DEMO_EMAIL` і `SEED_DEMO_PASSWORD`, інакше старт зупиняється із поясненням.

У журналі на першому запуску з порожньою БД можна побачити `fail` для запиту до `__EFMigrationsHistory`: це очікувана перевірка існування таблиці, одразу після якої застосовується міграція.

## `DemoDataSeeder`

Створює користувача «Олена Коваленко» (шаблон «Студент», пояс `Europe/Kyiv`) і 9 реалістичних задач: різні статуси, пріоритети, дедлайни відносно сьогодні (є прострочена, на сьогодні, майбутні й без дати), теги, підзадачі, Markdown-нотатки, значення власних полів та історія статусів. Якщо користувач з таким email уже є, нічого не змінює.

## Міграції

Створення нової міграції:

```bash
cd backend
dotnet tool restore
dotnet tool run dotnet-ef migrations add <Name> --project src/TaskTracker.Infrastructure --output-dir Persistence/Migrations
```

`dotnet-ef` встановлено як локальний інструмент (`backend/.config/dotnet-tools.json`). `DesignTimeDbContextFactory` бере рядок підключення зі змінної `ConnectionStrings__Default` або використовує порожній локальний, бо під час створення міграції до БД не підключаються.
