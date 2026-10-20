# SportsBooking

Платформа бронювання спортивних майданчиків. Поетапна міграція від моноліту до мікросервісів (патерн *Strangler Fig*) з використанням API Gateway (Ocelot).

## Порти та сервіси

| Сервіс | Порт / URL | База даних (MySQL) | Призначення |
|--------|------------|---------------------|-------------|
| **ApiGateway** | `http://localhost:5000` | — | Шлюз Ocelot, єдина точка входу, наскрізний `X-Correlation-Id` |
| **AuthService** | `http://localhost:5001` | `miniproject_auth` | Автентифікація, користувачі, ролі, видача JWT та Refresh токенів |
| **CatalogService** | `http://localhost:5002` | `miniproject_catalog` | Каталог спортивних майданчиків (`Venue`), категорій |
| **BookingService** *(ЛР2)* | `http://localhost:5003` | `miniproject_booking` | Операційний сервіс бронювань (`Booking`, `ItemSnapshot`, `Idempotency-Key`) |
| **Monolith** | `http://localhost:5100` | `SportsBooking` | Старий моноліт (залишений для порівняння на час перевірки) |
| **Інтеграційні тести** | В пам'яті (`WebApplicationFactory`) | `miniproject_booking_test` | Ізольована тестова БД для `BookingService.IntegrationTests` |

## Підготовка MySQL (User Secrets)

Рядки підключення з паролями не зберігаються в репозиторії — налаштуйте їх через `dotnet user-secrets`:

```powershell
# AuthService
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=miniproject_auth;User=root;Password=YOUR_PASSWORD" --project src/AuthService

# CatalogService
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=miniproject_catalog;User=root;Password=YOUR_PASSWORD" --project src/CatalogService

# BookingService (ЛР2)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=miniproject_booking;User=root;Password=YOUR_PASSWORD" --project src/BookingService

# Monolith
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=SportsBooking;User=root;Password=YOUR_PASSWORD" --project src/Monolith
```

Бази даних, таблиці та перенесені історичні записи (`Id = 1, 3, 4` із підняттям `AUTO_INCREMENT = 100`) створюються автоматично за допомогою міграцій EF Core під час запуску сервісів або командою:

```powershell
dotnet ef database update --project src/BookingService
```

Окремий SQL-скрипт перенесення даних зі старої БД також доступний у `scripts/migrate-bookings.sql`.

## Запуск через Docker Compose (Бонус +3 бали)

Усі мікросервіси, шлюз Ocelot та MySQL піднімаються однією командою:

```powershell
docker compose up --build
```

Після запуску доступні:
- Шлюз: `http://localhost:5000`
- AuthService: `http://localhost:5001/swagger`
- CatalogService: `http://localhost:5002/swagger`
- BookingService: `http://localhost:5003/swagger`

## Запуск сервісів вручну

```powershell
# Термінал 1 — AuthService (:5001)
dotnet run --project src/AuthService --launch-profile http

# Термінал 2 — CatalogService (:5002)
dotnet run --project src/CatalogService --launch-profile http

# Термінал 3 — BookingService (:5003)
dotnet run --project src/BookingService --launch-profile http

# Термінал 4 — ApiGateway (:5000)
dotnet run --project src/ApiGateway --launch-profile http
```

## Запуск інтеграційних тестів (ЛР2)

Проєкт `tests/BookingService.IntegrationTests` використовує `WebApplicationFactory<Program>`, підміняє `ICatalogClient` стабом (`FakeCatalogClient`) і працює з окремою тестовою базою `miniproject_booking_test`:

```powershell
dotnet test
```

## Перевірка роботи через шлюз (`:5000`)

```powershell
# 1. Health-check сервісів
Invoke-RestMethod http://localhost:5000/health
Invoke-RestMethod http://localhost:5003/health

# 2. Вхід користувача (отримання JWT токена)
$resp = Invoke-RestMethod http://localhost:5000/api/v1/auth/login -Method POST `
  -ContentType 'application/json' `
  -Body '{"email":"admin@sportsbooking.com","password":"Admin123!"}'
$token = $resp.accessToken

# 3. Створення бронювання з Idempotency-Key через шлюз
Invoke-RestMethod http://localhost:5000/api/bookings -Method POST `
  -Headers @{ Authorization = "Bearer $token"; "Idempotency-Key" = "demo-key-001" } `
  -ContentType 'application/json' `
  -Body '{"venueId":1,"startTime":"2027-05-10T14:00:00Z","hours":2}'

# 4. Отримання списку своїх бронювань з пагінацією та фільтром за статусом
Invoke-RestMethod "http://localhost:5000/api/bookings?page=1&pageSize=10&status=Confirmed" `
  -Headers @{ Authorization = "Bearer $token" }
```

## Swagger UI

- AuthService: `http://localhost:5001/swagger`
- CatalogService: `http://localhost:5002/swagger`
- BookingService: `http://localhost:5003/swagger`
- Monolith: `http://localhost:5100/swagger`

## Маршрути Gateway (`ocelot.json`)

| Upstream (клієнт → `:5000`) | Downstream (шлюз → сервіс) |
|---------------------------|---------------------------|
| `/api/v1/auth/**` | AuthService `:5001` |
| `/api/v1/users/**` | AuthService `:5001` |
| `/api/v1/catalog/**` | CatalogService `:5002` |
| `/api/bookings`, `/api/bookings/**` | BookingService `:5003` |
| `/api/**` | Monolith `:5100` |

## Документація

- [Інвентаризація моноліту](docs/monolith-inventory.md)
- [ADR-0001: Поділ моноліту на сервіси](docs/adr/0001-split-monolith.md)
- [ADR-0002: Шлюз Ocelot перед монолітом](docs/adr/0002-api-gateway.md)
- [ADR-0003: Винесення операційного сервісу (BookingService)](docs/adr/0003-operations-extraction.md)
- [Порівняння базової поведінки (baseline-diff)](docs/baseline-diff.md)
- [Діаграми архітектури](docs/diagrams/README.md)
