# SportsBooking

Платформа бронювання спортивних майданчиків. Архітектура переходу від моноліту до мікросервісів з використанням API Gateway (Ocelot).

## Порти та сервіси

| Сервіс | Порт / URL | База даних (MySQL) | Призначення |
|--------|------------|---------------------|-------------|
| **ApiGateway** | `http://localhost:5000` | — | Шлюз Ocelot, єдина точка входу |
| **AuthService** | `http://localhost:5001` | `miniproject_auth` | Автентифікація, користувачі, ролі, видача JWT та Refresh токенів |
| **CatalogService** | `http://localhost:5002` | `miniproject_catalog` | Каталог спортивних майданчиків (Venue), категорій, локальна валідація JWT |
| **Monolith** | `http://localhost:5100` | `SportsBooking` | Моноліт (залишковий функціонал бронювань) |
| **MySQL Server** | `localhost:3306` | — | СУБД (принцип Database per Service) |

## Підготовка MySQL (User Secrets)

Рядки підключення зберігаються у User Secrets:

```powershell
# Monolith
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=SportsBooking;User=root;Password=YOUR_PASSWORD" --project src/Monolith

# AuthService
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=miniproject_auth;User=root;Password=YOUR_PASSWORD" --project src/AuthService

# CatalogService
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=miniproject_catalog;User=root;Password=YOUR_PASSWORD" --project src/CatalogService
```

Бази даних і таблиці створюються автоматично за допомогою EF Core міграцій при запуску сервісів.

## Запуск сервісів

Для запуску системи необхідно запустити 4 сервіси у терміналах:

```powershell
# Термінал 1 — Monolith
dotnet run --project src/Monolith --launch-profile http

# Термінал 2 — AuthService
dotnet run --project src/AuthService --launch-profile http

# Термінал 3 — CatalogService
dotnet run --project src/CatalogService --launch-profile http

# Термінал 4 — ApiGateway
dotnet run --project src/ApiGateway --launch-profile http
```

## Перевірка роботи через шлюз (:5000)

```powershell
# Health-check сервісів
Invoke-RestMethod http://localhost:5000/health
Invoke-RestMethod http://localhost:5001/health
Invoke-RestMethod http://localhost:5002/health

# Каталог майданчиків (анонімний доступ)
Invoke-RestMethod "http://localhost:5000/api/v1/catalog/items?page=1&pageSize=10"

# Вхід користувача (отримання токена)
$resp = Invoke-RestMethod http://localhost:5000/api/v1/auth/login -Method POST `
  -ContentType 'application/json' `
  -Body '{"email":"admin@sportsbooking.com","password":"Admin123!"}'
$token = $resp.accessToken

# Запит захищеного профілю
Invoke-RestMethod http://localhost:5000/api/v1/users/me -Headers @{ Authorization = "Bearer $token" }

# Створення нового майданчика (роль Admin)
Invoke-RestMethod http://localhost:5000/api/v1/catalog/items -Method POST `
  -Headers @{ Authorization = "Bearer $token" } `
  -ContentType 'application/json' `
  -Body '{"name":"Нове футбольне поле","address":"вул. Спортивна 15","pricePerHour":650,"categoryId":1}'
```

## Swagger UI

- AuthService: `http://localhost:5001/swagger`
- CatalogService: `http://localhost:5002/swagger`
- Monolith: `http://localhost:5100/swagger`

## Маршрути Gateway (`ocelot.json`)

| Upstream (клієнт → :5000) | Downstream (шлюз → сервіс) |
|---------------------------|---------------------------|
| `/api/v1/auth/**` | AuthService `:5001` |
| `/api/v1/users/**` | AuthService `:5001` |
| `/api/v1/catalog/**` | CatalogService `:5002` |
| `/api/**` | Monolith `:5100` |

## Документація

- [Інвентаризація моноліту](docs/monolith-inventory.md)
- [ADR-0001: Поділ моноліту на сервіси](docs/adr/0001-split-monolith.md)
- [ADR-0002: Шлюз Ocelot перед монолітом](docs/adr/0002-api-gateway.md)
- [Діаграми архітектури](docs/diagrams/README.md)
