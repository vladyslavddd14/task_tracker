# Архітектура

Backend побудований за Clean Architecture. Залежності спрямовані лише всередину:

```text
Api ──► Application ──► Domain
Infrastructure ──► Application ──► Domain
```

`Api` також посилається на `Infrastructure`, але лише щоб зібрати залежності (composition root) у `Program.cs`. Жоден шар усередині не знає про зовнішні.

## Відповідальність шарів

| Шар | Знає про | Не знає про |
|---|---|---|
| Domain | лише .NET | EF Core, HTTP, JWT, валідатори |
| Application | Domain, FluentValidation | EF Core, HTTP, конкретні БД і токени |
| Infrastructure | Application, Domain, EF Core/Npgsql, BCrypt, JWT | HTTP і контролери |
| Api | Application, Infrastructure (для DI) | бізнес-правила (їх тут немає) |

Завдяки цьому бізнес-правила та сценарії тестуються без бази даних: репозиторії підміняються (NSubstitute), а фільтри виконуються над списком у пам'яті.

## Потік запиту: зміна статусу задачі

```text
PATCH /api/tasks/{id}/status
  └─ TasksController.ChangeStatus          витягує userId із JWT, викликає сервіс
      └─ TaskService.ChangeStatusAsync
          ├─ ChangeStatusRequestValidator   перевірка вхідних даних (400)
          ├─ ITaskRepository.GetAsync       задача шукається за (userId, id); чужа дає null → 404
          ├─ TaskItem.ChangeStatus          доменне правило: CompletedAt + запис StatusTransition
          └─ IUnitOfWork.SaveChangesAsync   одна транзакція PostgreSQL
```

Помилки з будь-якого шару перетворюються на RFC 7807 в одному місці: `ApiExceptionHandler`.

## Де приймається рішення про порожній заголовок

Рішення продубльовано свідомо, на трьох рівнях (захист «в глибину»), а джерелом істини є backend:

1. **Frontend:** `validateTitle` блокує відправку форми й показує повідомлення.
2. **Application:** `ValidTitle` у валідаторах (FluentValidation) повертає `400` із полем `title`.
3. **Domain:** `Guard.RequiredText` кидає `DomainException`, якщо правило обійшли.

## Ключові рішення

- **Прості репозиторії.** По одному інтерфейсу на агрегат (`IUserRepository`, `ITaskRepository`, `ITagRepository`, `ICustomFieldRepository`) без generic-базових класів.
- **Ізоляція даних на рівні репозиторію.** Кожен метод приймає `userId` і фільтрує за ним, тож помилка в контролері не відкриє чужих даних. Чужий ресурс виглядає як неіснуючий (`404`).
- **Фільтри як LINQ.** `TaskFilters` живе в Application, працює над `IQueryable<TaskItem>`: EF Core переводить його в SQL, а юніт-тести виконують той самий код над `List`.
- **Транзакційність через `SaveChanges`.** Кожен сценарій завершується одним збереженням, яке EF Core виконує в одній транзакції.
- **Дедлайн як `DateOnly`.** Календарна дата без часу не зсувається часовими поясами; «сьогодні» обчислюється за поясом користувача.
- **Дробові позиції в Kanban.** Картку можна вставити між двома іншими, змінивши лише її власну позицію; колонка перенумеровується, коли відстань між сусідами стає надто малою.
- **Оптимістичний контроль конкурентності.** Колонка `xmin` PostgreSQL є токеном версії задачі.
- **Єдина точка входу.** nginx віддає статику та проксує `/api`, тому в продакшені CORS не потрібен.
- **Рядки інтерфейсу в одному модулі.** `frontend/src/i18n/uk.ts`, що готує проєкт до локалізації.
