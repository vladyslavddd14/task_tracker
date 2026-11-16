# Модуль Api (`TaskTracker.Api`)

Тонкий HTTP-шар: маршрутизація, автентифікація, перетворення помилок на відповіді. Бізнес-логіки тут немає.

## Структура

```text
Program.cs                              складання застосунку
ClaimsExtensions.cs                     GetUserId() з claim "sub"
Middleware/ApiExceptionHandler.cs       виняток → RFC 7807
Controllers/
  AuthAndProfileControllers.cs          AuthController, MeController
  TasksController.cs                    задачі, «Сьогодні», календар, kanban
  SubtasksController.cs                 підзадачі
  TagsController.cs                     теги
  CustomFieldsController.cs             власні поля
appsettings.json, appsettings.Development.json
```

## REST-маршрути

Усі маршрути, крім `auth/*`, `/health` і `/swagger`, потребують `Authorization: Bearer <token>`. Формат JSON, перелічення передаються рядками (`"Todo"`), дедлайн як `YYYY-MM-DD`.

### Auth та профіль
| Метод | Шлях | Відповідь |
|---|---|---|
| POST | `/api/auth/register` | `201` + токен і користувач |
| POST | `/api/auth/login` | `200` + токен і користувач |
| GET / PUT | `/api/me` | профіль / зміна імені й поясу |
| PUT | `/api/me/password` | `204` |
| POST | `/api/me/apply-template` | `200` + користувач |

### Задачі
| Метод | Шлях | Опис |
|---|---|---|
| GET | `/api/tasks` | список із фільтрами, сортуванням і пагінацією (`{items,total,page,pageSize}`) |
| GET | `/api/tasks/today` | вигляд «Сьогодні» |
| GET | `/api/tasks/calendar?from&to` | задачі з дедлайном у діапазоні (ті ж фільтри) |
| GET | `/api/tasks/{id}` | деталі |
| POST | `/api/tasks` | `201` |
| PUT | `/api/tasks/{id}` | редагування всіх полів |
| PATCH | `/api/tasks/{id}/status` | `{ "status": "Done" }` |
| PATCH | `/api/tasks/{id}/move` | kanban: `{ "status", "position" }` |
| DELETE | `/api/tasks/{id}` | `204` |

### Підзадачі, теги, власні поля
| Метод | Шлях |
|---|---|
| POST | `/api/tasks/{id}/subtasks` |
| PUT / DELETE | `/api/tasks/{id}/subtasks/{subtaskId}` |
| PATCH | `/api/tasks/{id}/subtasks/reorder` (`{ "orderedIds": [...] }`) |
| GET / POST | `/api/tags`, `/api/custom-fields` |
| PUT / DELETE | `/api/tags/{id}`, `/api/custom-fields/{id}` |

### Службові
`GET /health` перевіряє зв'язок із БД (`200` або `503`). `GET /swagger` документація API, лише в середовищі Development.

Маршрути `tasks/today` і `tasks/calendar` не конфліктують із `tasks/{id}`, бо `id` має обмеження `:guid`.

## Коди відповідей і формат помилок

| Код | Коли |
|---|---|
| 200 / 201 / 204 | успіх |
| 400 | помилка валідації або порушення доменного правила (є словник `errors` по полях) |
| 401 | немає або недійсний токен, невірні дані входу |
| 404 | не знайдено, включно з чужими ресурсами |
| 409 | дублікат (email, тег, поле) або конкурентна зміна |
| 500 | внутрішня помилка (стек-трейс клієнту не передається, лише в журнал) |

Приклад відповіді `400`:

```json
{
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/tasks",
  "errors": { "title": ["'Title' must not be empty."] }
}
```

`ApiExceptionHandler` реалізує `IExceptionHandler`, а відповідь повертається з типом `application/problem+json`. Ключі `errors` мають camelCase. Помилки прив'язки моделі (наприклад, невідоме значення перелічення) повертає сам ASP.NET Core в тому ж форматі.

## Автентифікація

JWT Bearer із перевіркою підпису, видавця, аудиторії та терміну дії (запас 1 хвилина). `MapInboundClaims = false`, тому id користувача читається з claim `sub`. Параметри підпису беруться з `JwtOptions` (див. [Infrastructure](infrastructure.md)).

## Конфігурація

| Ключ | Змінна середовища | Опис |
|---|---|---|
| `ConnectionStrings:Default` | `ConnectionStrings__Default` | рядок підключення до PostgreSQL |
| `Jwt:Secret` | `Jwt__Secret` / `JWT__Secret` | ключ підпису, ≥ 32 символи (обов'язково) |
| `Jwt:ExpiresMinutes` | `Jwt__ExpiresMinutes` | час життя токена (60) |
| `Jwt:Issuer`, `Jwt:Audience` | | за замовчуванням `TaskTracker` |
| `SEED_DEMO`, `SEED_DEMO_EMAIL`, `SEED_DEMO_PASSWORD` | | демо-дані |
| `Cors:AllowedOrigins` | | дозволені походження, діє лише в Development |

Імена ключів конфігурації нечутливі до регістру, тому `JWT__Secret` і `Jwt__Secret` рівнозначні.

## Інші властивості

- **Ліміт тіла запиту:** 1 МБ (нотатки до 20 000 символів із запасом).
- **CORS:** лише в Development для dev-сервера Vite; в Docker усе йде через nginx з одного походження.
- **Логування:** вбудований `ILogger`, паролі й токени не логуються; повідомлення DataProtection приглушено.
- **Запуск:** перед `app.Run()` викликається `DatabaseInitializer`, тож міграції застосовуються до прийому запитів.
- **Тестування:** `public partial class Program` дає `WebApplicationFactory` доступ до точки входу.
