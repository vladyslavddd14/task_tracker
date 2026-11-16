using Microsoft.EntityFrameworkCore;
using TaskTracker.Application.Abstractions;
using TaskTracker.Application.Common;
using TaskTracker.Domain;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;
using TaskTracker.Infrastructure.Persistence;

namespace TaskTracker.Infrastructure.Seed;

/// <summary>Creates a demo user with realistic student tasks. Safe to run repeatedly.</summary>
public class DemoDataSeeder(AppDbContext db, IPasswordHasher hasher, IClock clock)
{
    private const string TimeZoneId = "Europe/Kyiv";

    public async Task SeedAsync(string email, string password, CancellationToken ct = default)
    {
        var normalizedEmail = User.NormalizeEmail(email);
        if (await db.Users.AnyAsync(u => u.Email == normalizedEmail, ct))
        {
            return;
        }

        var now = clock.UtcNow;
        var today = UserTime.Today(TimeZoneId, now);
        var user = User.Create(normalizedEmail, hasher.Hash(password), "Олена Коваленко", TimeZoneId,
            ProfileTemplate.Student, now);
        db.Users.Add(user);

        var template = ProfileTemplateCatalog.Get(ProfileTemplate.Student);
        var tags = template.Tags.ToDictionary(t => t.Name, t => Tag.Create(user.Id, t.Name, t.Color));
        tags.Add("проєкт", Tag.Create(user.Id, "проєкт", "#14b8a6"));
        db.Tags.AddRange(tags.Values);

        var fields = template.Fields
            .Select((f, i) => CustomFieldDefinition.Create(user.Id, f.Name, f.Type, i))
            .ToDictionary(f => f.Name);
        db.CustomFieldDefinitions.AddRange(fields.Values);

        var positions = new Dictionary<TaskItemStatus, double>();

        TaskItem AddTask(string title, TaskItemStatus status, TaskPriority priority, int? deadlineInDays,
            string? description = null, string? notes = null, string[]? tagNames = null, string? subject = null,
            string? grade = null, int createdDaysAgo = 7, int changedDaysAgo = 1)
        {
            positions[status] = positions.GetValueOrDefault(status) + 1000;
            var created = now.AddDays(-createdDaysAgo);
            var task = TaskItem.Create(user.Id, title, TaskItemStatus.Todo, priority, positions[status], created);
            task.Update(title, description, notes, priority, deadlineInDays is null ? null : today.AddDays(deadlineInDays.Value),
                created);
            if (status == TaskItemStatus.Done)
            {
                task.ChangeStatus(TaskItemStatus.InProgress, now.AddDays(-changedDaysAgo - 1));
            }

            task.ChangeStatus(status, now.AddDays(-changedDaysAgo));
            task.SetTags(tagNames?.Select(n => tags[n]).ToList() ?? []);

            var values = new List<(Guid, string?)>();
            if (subject is not null) values.Add((fields["Предмет"].Id, subject));
            if (grade is not null) values.Add((fields["Оцінка"].Id, grade));
            task.SetCustomFieldValues(values);

            db.Tasks.Add(task);
            return task;
        }

        var exam = AddTask("Підготуватися до іспиту з баз даних", TaskItemStatus.InProgress, TaskPriority.High, 3,
            "Іспит у четвер о 10:00, аудиторія 214.",
            "## План підготовки\n\n- [x] Нормалізація до 3НФ\n- [x] JOIN-и та підзапити\n- [ ] Транзакції та рівні ізоляції\n- [ ] Індекси та план виконання запиту\n\n> Головне: вміти пояснити, чому індекс може сповільнити запис.\n\n| Тема | Складність |\n|---|---|\n| Транзакції | висока |\n| Індекси | середня |\n",
            ["екзамен"], "Бази даних", createdDaysAgo: 10, changedDaysAgo: 2);
        exam.AddSubtask("Повторити нормалізацію", now.AddDays(-5)).Update("Повторити нормалізацію", true);
        exam.AddSubtask("Розв'язати 10 задач на SQL", now.AddDays(-4)).Update("Розв'язати 10 задач на SQL", true);
        exam.AddSubtask("Прочитати про рівні ізоляції транзакцій", now.AddDays(-3));
        exam.AddSubtask("Подивитися EXPLAIN ANALYZE на прикладах", now.AddDays(-3));

        var lab5 = AddTask("Здати лабораторну №5: REST API", TaskItemStatus.Todo, TaskPriority.High, -1,
            "Потрібно захистити до п'ятниці, інакше мінус бал.",
            "Вимоги:\n\n1. CRUD для сутності `Book`\n2. Валідація вхідних даних\n3. Swagger-документація\n",
            ["лабораторна"], "Веб-технології", createdDaysAgo: 9, changedDaysAgo: 9);
        lab5.AddSubtask("Дописати валідацію", now.AddDays(-2));
        lab5.AddSubtask("Додати Swagger", now.AddDays(-2));

        AddTask("Конспект лекції з алгоритмів", TaskItemStatus.Done, TaskPriority.Medium, -2,
            "Сортування злиттям та швидке сортування.", null, ["лекція"], "Алгоритми", "5",
            createdDaysAgo: 8, changedDaysAgo: 2);

        AddTask("Лабораторна №6: Docker Compose", TaskItemStatus.Todo, TaskPriority.Medium, 0,
            "Підняти застосунок і базу даних однією командою.",
            "```bash\ndocker compose up --build\n```\n\nПеревірити, що дані зберігаються після `docker compose down`.\n",
            ["лабораторна", "проєкт"], "Контейнеризація", createdDaysAgo: 3, changedDaysAgo: 3);

        AddTask("Прочитати розділ про Clean Architecture", TaskItemStatus.InProgress, TaskPriority.Low, 7,
            null, "Зробити нотатки про правило залежностей: усередину, до домену.", ["проєкт"], null,
            createdDaysAgo: 5, changedDaysAgo: 1);

        AddTask("Підготувати презентацію курсової", TaskItemStatus.Todo, TaskPriority.High, 10,
            "10 слайдів, захист 15 хвилин.", null, ["проєкт"], "Курсова робота", createdDaysAgo: 2, changedDaysAgo: 2);

        AddTask("Записатися на консультацію до викладача", TaskItemStatus.Todo, TaskPriority.Low, null,
            createdDaysAgo: 4, changedDaysAgo: 4);

        AddTask("Оплатити гуртожиток", TaskItemStatus.Todo, TaskPriority.Medium, 1,
            "Через застосунок банку, чек зберегти.", createdDaysAgo: 1, changedDaysAgo: 1);

        AddTask("Здати звіт з англійської (Unit 7)", TaskItemStatus.Done, TaskPriority.Low, -4,
            null, null, ["лекція"], "Англійська мова", "4", createdDaysAgo: 12, changedDaysAgo: 4);

        await db.SaveChangesAsync(ct);
    }
}
