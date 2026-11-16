# Документація Task Tracker

Технічна документація за модулями системи. Вимоги та критерії приймання описані в [специфікації](spec/SPEC.md), а швидкий старт у [README проєкту](../README.md).

| Документ | Про що |
|---|---|
| [Архітектура](architecture.md) | шари, правила залежностей, потік запиту, ключові рішення |
| [Domain](modules/domain.md) | сутності, інваріанти, доменна поведінка, шаблони профілів |
| [Application](modules/application.md) | сервіси (use cases), DTO, фільтри, валідація, абстракції |
| [Infrastructure](modules/infrastructure.md) | EF Core, міграції, репозиторії, JWT, сид демо-даних |
| [Api](modules/api.md) | контролери, REST-маршрути, обробка помилок, конфігурація |
| [Frontend](modules/frontend.md) | структура React-застосунку, дані, дошка, календар, локалізація |
| [Розгортання](modules/deployment.md) | Docker Compose, nginx, змінні середовища, локальна розробка |
| [Тестування](modules/testing.md) | рівні тестів, що саме покрито, як запускати |

## Карта репозиторію

```text
backend/
  src/
    TaskTracker.Domain/           сутності та правила, без залежностей
    TaskTracker.Application/      сценарії, DTO, валідатори, інтерфейси
    TaskTracker.Infrastructure/   EF Core, репозиторії, JWT, BCrypt, сид
    TaskTracker.Api/              контролери, Program.cs, обробка помилок
  tests/
    TaskTracker.Domain.Tests/
    TaskTracker.Application.Tests/
    TaskTracker.Api.IntegrationTests/
frontend/
  src/
    api/  components/  features/  hooks/  i18n/  lib/  pages/
docker-compose.yml   .env.example   README.md
docs/                ця документація та специфікація
```
