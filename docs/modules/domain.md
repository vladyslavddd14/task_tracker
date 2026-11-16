# Модуль Domain (`TaskTracker.Domain`)

Серце системи: сутності з поведінкою та інваріантами. Проєкт не має залежностей від пакетів.

## Сутності

| Сутність | Призначення | Основні поля |
|---|---|---|
| `User` | акаунт | `Email` (нижній регістр), `PasswordHash`, `DisplayName`, `TimeZoneId` (IANA), `ProfileTemplate`, `CreatedAt` |
| `TaskItem` | задача | `Title`, `Description`, `NotesMarkdown`, `Status`, `Priority`, `Deadline` (`DateOnly?`), `Position`, `CreatedAt`, `UpdatedAt`, `CompletedAt` |
| `Subtask` | підзадача (один рівень) | `Title`, `IsDone`, `Position`, `CreatedAt` |
| `Tag` | тег користувача | `Name`, `NormalizedName`, `Color` |
| `TaskTag` | зв'язок задача ↔ тег | складений ключ `(TaskItemId, TagId)` |
| `StatusTransition` | запис про зміну статусу | `FromStatus`, `ToStatus`, `ChangedAt` |
| `CustomFieldDefinition` | власне поле | `Name`, `NormalizedName`, `Type`, `Position` |
| `CustomFieldValue` | значення поля в задачі | `CustomFieldDefinitionId`, `Value` (текст) |

Усі ключі `Guid`, створюються в домені (`Guid.NewGuid()`), усі часові мітки в UTC.

### Enum-и

`TaskItemStatus` (`Todo`, `InProgress`, `Done`), `TaskPriority` (`Low`, `Medium`, `High`), `CustomFieldType` (`Text`, `Number`, `Checkbox`, `Date`), `ProfileTemplate` (`Universal`, `Student`, `Freelancer`, `Training`). Числові значення priority зростають, тому сортування за пріоритетом працює на рівні БД.

## Поведінка `TaskItem`

| Метод | Що робить |
|---|---|
| `Create(...)` | створює задачу; назва обрізається, порожня відхиляється; для `Done` одразу ставить `CompletedAt` |
| `Update(...)` | змінює назву, опис, нотатки, пріоритет, дедлайн; порожній текст опису/нотаток стає `null` |
| `ChangeStatus(status, now)` | будь-який перехід дозволений; нічого не робить при тому ж статусі; додає `StatusTransition`; ставить або очищає `CompletedAt` |
| `SetPosition`, `Touch` | позиція в колонці та оновлення `UpdatedAt` |
| `IsOverdue(today)` | `Deadline < today` і `Status != Done` |
| `AddSubtask`, `RemoveSubtask`, `ReorderSubtasks` | керування підзадачами; не більше 50; нове впорядкування мусить містити кожну підзадачу рівно один раз |
| `SetTags(tags)` | замінює набір тегів |
| `SetCustomFieldValues(values)` | upsert значень; `null` видаляє значення |
| `SubtasksTotal`, `SubtasksDone` | прогрес підзадач |

Завершення всіх підзадач **не** змінює статус задачі автоматично (FR-T3): інтерфейс лише пропонує позначити задачу виконаною.

## Інваріанти та межі

Усі межі зібрані в `DomainLimits`, їх спільно використовують домен і валідатори:

| Правило | Значення |
|---|---|
| Назва задачі/підзадачі | 1–200 символів після обрізання |
| Опис | до 2000 |
| Нотатки | до 20 000 |
| Назва тегу | 1–30, унікальна в межах користувача без урахування регістру |
| Назва поля | 1–40, унікальна в межах користувача |
| Значення поля | до 1000 символів |
| Підзадач у задачі | до 50 |
| Email / пароль | до 254 / 8–100 |

`Guard` (внутрішній клас) містить спільні перевірки: `RequiredText` обрізає та відхиляє порожній або задовгий текст, `OptionalText` перетворює пробіли на `null`. Порушення кидають `DomainException`.

Тип власного поля (`CustomFieldDefinition.Type`) задається при створенні й далі не змінюється, щоб збережені значення не змінили сенс.

## `ProfileTemplateCatalog`

Статичний каталог шаблонів («твіст» проєкту): для кожного `ProfileTemplate` повертає теги (назва + колір) і власні поля (назва + тип).

| Шаблон | Теги | Поля |
|---|---|---|
| Universal | немає | немає |
| Student | лекція, лабораторна, екзамен | Предмет (текст), Оцінка (число) |
| Freelancer | клієнт, рахунок | Клієнт (текст), Ставка/год (число), Виставлено рахунок (прапорець) |
| Training | біг, зал | Дистанція, км (число), Тривалість, хв (число) |

Застосування шаблону до користувача виконує `ProfileTemplateService` в Application.

## Інкапсуляція

Властивості мають `private set`, колекції відкриті як `IReadOnlyCollection<T>` із приватним `List<T>`-полем (EF Core заповнює їх через поле). Змінювати стан можна лише методами сутності, тому інваріанти неможливо обійти.
