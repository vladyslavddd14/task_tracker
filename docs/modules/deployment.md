# Розгортання та конфігурація

## Сервіси `docker-compose.yml`

| Сервіс | Образ | Призначення |
|---|---|---|
| `db` | `postgres:16-alpine` | база даних; named volume `pgdata`; healthcheck `pg_isready`; порт публікується лише на `127.0.0.1` |
| `api` | збірка `backend/Dockerfile` | API; чекає на здорову БД (`service_healthy`); застосовує міграції при старті; healthcheck `GET /health` |
| `web` | збірка `frontend/Dockerfile` | nginx: статика React та проксі `/api` на `api`; чекає на здоровий `api` |

Порядок старту: `db` → `api` → `web`. Дані зберігаються у volume `pgdata` і переживають `docker compose down` та перезапуск машини; їх видаляє лише `docker compose down -v`.

## Запуск

```bash
cp .env.example .env      # один раз; замініть усі значення change-me
docker compose up --build
```

Застосунок: `http://localhost:<WEB_PORT>` (за замовчуванням 8080). Наступні запуски: `docker compose up -d`; `--build` потрібен після змін коду або на новому пристрої.

## Змінні середовища

| Змінна | Призначення | Обов'язкова |
|---|---|---|
| `POSTGRES_DB`, `POSTGRES_USER` | БД і користувач (`tasktracker`) | ні |
| `POSTGRES_PASSWORD` | пароль БД (без `;`) | так |
| `JWT__Secret` | ключ підпису JWT, ≥ 32 символи | так |
| `JWT__ExpiresMinutes` | час життя токена, хв (60) | ні |
| `SEED_DEMO` | `true` створює демо-користувача | ні (`false`) |
| `SEED_DEMO_EMAIL`, `SEED_DEMO_PASSWORD` | демо-акаунт | якщо `SEED_DEMO=true` |
| `WEB_PORT` | порт застосунку на хості (8080) | ні |
| `DB_PORT` | порт PostgreSQL на `127.0.0.1` (5432) | ні |

Без `POSTGRES_PASSWORD` або `JWT__Secret` Compose зупиняється з повідомленням, що треба скопіювати `.env.example`. Реальних секретів у репозиторії немає: `.env` ігнорується git, а в `.env.example` лише заглушки. Рядок підключення `api` збирається в `docker-compose.yml` із цих змінних.

## Образи

**`backend/Dockerfile`** (багатоетапний): `dotnet/sdk:9.0` збирає та публікує, `dotnet/aspnet:9.0` запускає. Файли проєктів копіюються окремо до коду, тож шар `restore` кешується. Процес працює від користувача `app` (не root) на порту 8080. Контекст збірки обмежує `backend/.dockerignore`.

**`frontend/Dockerfile`** (багатоетапний): `node:22-alpine` робить `npm ci` і `npm run build`, потім статика копіюється в `nginxinc/nginx-unprivileged` (не root, порт 8080).

## nginx (`frontend/nginx.conf`)

- `/api/` проксується на `http://api:8080`, тож фронтенд і бекенд мають одне походження і CORS не потрібен;
- `/assets/` (файли із хешем) кешуються на рік;
- решта шляхів повертає `index.html` (SPA-маршрутизація), без кешування;
- ліміт тіла запиту 1 МБ;
- заголовки безпеки: `Content-Security-Policy`, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`.

## Локальна розробка

Лише БД у Docker, `api` і `web` на своїй машині:

```bash
docker compose up db -d
```

API (потрібен .NET 9 SDK):

```bash
cd backend
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=tasktracker;Username=tasktracker;Password=<POSTGRES_PASSWORD>"
export Jwt__Secret="<JWT__Secret>"
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --project src/TaskTracker.Api --urls http://localhost:8080
```

Frontend (Node 22):

```bash
cd frontend
npm install
npm run dev
```

У Development API дозволяє CORS для `http://localhost:5173` і відкриває Swagger на `/swagger`.

## CI

`.github/workflows/ci.yml` запускає на кожен push: `dotnet test` (інтеграційні тести піднімають PostgreSQL через Testcontainers), для frontend `lint`, `format:check`, тести та збірку, а також перевіряє збірку Docker-образів.

## Діагностика

| Симптом | Причина та рішення |
|---|---|
| Compose каже `required variable … is missing` | немає `.env`: `cp .env.example .env` і заповніть значення |
| `api` не стартує, у логах про `Jwt:Secret` | секрет коротший за 32 символи |
| `api` не підключається до БД після зміни пароля в `.env` | volume вже ініціалізовано старим паролем; поверніть старий пароль або видаліть volume (`down -v`, **дані зникнуть**) |
| Порт зайнятий | змініть `WEB_PORT` / `DB_PORT` у `.env` |
| `docker compose` не бачить Docker | запустіть Docker Desktop |
