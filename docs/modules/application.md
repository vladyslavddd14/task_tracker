# Модуль Application (`TaskTracker.Application`)

Сценарії використання. Залежить лише від Domain та FluentValidation. З «зовнішнім світом» працює через інтерфейси.

## Структура

```text
Abstractions/   IClock, IPasswordHasher, ITokenService, IUnitOfWork, інтерфейси репозиторіїв
Common/         винятки NotFound/Conflict/Unauthorized, PagedResult, Fail, UserTime
Dtos/           запити, відповіді та Mapper (сутність → DTO)
Filtering/      TaskQuery, CalendarQuery, TaskFilters (фільтри та сортування)
Validators/     правила FluentValidation
Services/       AuthService, ProfileService, ProfileTemplateService, TaskService,
                SubtaskService, TagService, CustomFieldService, CustomFieldValueRules
DependencyInjection.cs   AddApplication()
```

## Абстракції

| Інтерфейс | Роль |
|---|---|
| `IClock` | поточний UTC-час (підміняється в тестах) |
| `IPasswordHasher` | `Hash` / `Verify` пароля |
| `ITokenService` | випуск access-токена для користувача |
| `IUnitOfWork` | `SaveChangesAsync`: усі зміни однією транзакцією |
| `IUserRepository`, `ITagRepository`, `ICustomFieldRepository` | прості CRUD-інтерфейси з `userId` |
| `ITaskRepository` | `GetAsync`, `SearchAsync`, `GetMaxPositionAsync`, `GetColumnAsync`, `Add`, `Remove` |

## Сервіси

Контролери лише викликають ці класи. Кожен метод приймає `userId` першим аргументом.

### `AuthService`
- `RegisterAsync`: валідує запит, перевіряє унікальність email (`409`), хешує пароль, створює користувача та застосовує шаблон профілю, зберігає, повертає токен.
- `LoginAsync`: невірний email чи пароль дають `UnauthorizedException` (`401`) без розкриття, що саме не так.

### `ProfileService` і `ProfileTemplateService`
- Профіль: читання, зміна імені та часового поясу, зміна пароля (потрібен поточний).
- `ProfileTemplateService.ApplyAsync` додає відсутні теги й поля шаблону, не видаляє наявні та пропускає дублікати за нормалізованою назвою; нові поля отримують наступні позиції. Використовується і при реєстрації, і в `POST /api/me/apply-template`.

### `TaskService`
| Метод | Деталі |
|---|---|
| `ListAsync` | валідація `TaskQuery`, фільтри + сортування + пагінація (`page`, `pageSize` 1–200, за замовчуванням 50) |
| `TodayAsync` | незавершені задачі з дедлайном `<= сьогодні` (у поясі користувача), за пріоритетом |
| `CalendarAsync` | задачі з дедлайном у діапазоні `from..to` (до 120 днів) з тими ж фільтрами |
| `GetAsync` | деталі: підзадачі, теги, власні поля, історія статусів |
| `CreateAsync` | новий запис іде в кінець колонки; теги й значення полів перевіряються |
| `UpdateAsync` | повна заміна полів; зміна статусу фіксується як перехід |
| `ChangeStatusAsync` | зміна статусу, картка стає останньою в новій колонці |
| `MoveAsync` | kanban: новий статус і позиція одним збереженням; якщо сусідні позиції ближчі за `1e-6`, колонку перенумеровують кроком 1000 |
| `DeleteAsync` | видалення; дочірні дані прибирає каскад БД |

Значення власних полів перевіряє `CustomFieldValueRules.TryNormalize`: число у форматі invariant, прапорець `true/false`, дата `yyyy-MM-dd`, текст до 1000 символів. Порожнє значення очищає поле. У `PUT` змінюються лише ті поля, що передані.

### `SubtaskService`
Додавання, редагування (назва та `IsDone`), видалення, перевпорядкування. Ліміт у 50 підзадач кидає `DomainException`.

### `TagService`, `CustomFieldService`
CRUD з перевіркою унікальності назви без урахування регістру (`409`). Власні поля: тип змінити не можна (`400`); видалення поля видаляє його значення.

## Фільтри (`Filtering`)

`TaskQuery` описує параметри `GET /api/tasks`. Усі фільтри комбінуються через AND:

| Параметр | Поведінка |
|---|---|
| `status` (повторюваний) | будь-який із зазначених статусів |
| `priority` | точний збіг |
| `tagId` (повторюваний) | задача має хоча б один із тегів |
| `deadline` | `overdue` (прострочені й не виконані), `today`, `upcoming`, `none` |
| `deadlineFrom` / `deadlineTo` | включний діапазон дат |
| `q` | підрядок без урахування регістру в назві, описі та нотатках |
| `customFieldId` + `customFieldValue` | підрядок у значенні власного поля |
| `sort` / `dir` | `priority` (за замовчуванням, від високого), `deadline` (без дедлайну в кінці), `createdAt` (нові першими), `title` |

`TaskFilters.Apply` і `ApplyOrder` є звичайним LINQ над `IQueryable<TaskItem>`: EF Core перетворює їх на SQL, а юніт-тести виконують ті самі вирази над списком у пам'яті. Рівні за ключем задачі впорядковуються за дедлайном, датою створення й `Id`, тож порядок завжди детермінований.

## Валідатори

Усі правила зібрані у `Validators/Validators.cs` (FluentValidation): `RegisterRequestValidator`, `CreateTaskRequestValidator`, `UpdateTaskRequestValidator`, `MoveTaskRequestValidator`, `TaskQueryValidator`, `CalendarQueryValidator`, `TagRequestValidator`, `CustomFieldRequestValidator` та ін. Помилка валідації кидає `FluentValidation.ValidationException`, яку `Api` перетворює на `400` зі словником `errors`.

## Помилки

| Виняток | HTTP |
|---|---|
| `ValidationException` (FluentValidation), `DomainException` | 400 |
| `UnauthorizedException` | 401 |
| `NotFoundException` | 404 (також для чужих ресурсів) |
| `ConflictException` | 409 |

`Fail.Validation(field, message)` створює помилку валідації для одного поля.

## Час і часові пояси

`UserTime.Today(timeZoneId, utcNow)` повертає календарну дату за поясом користувача (запасний варіант: UTC). Для `Europe/Kyiv` є запасний псевдонім `Europe/Kiev`, бо старіші бази часових поясів (наприклад, на Windows) знають лише його.
