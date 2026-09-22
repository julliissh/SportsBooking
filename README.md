# SportsBooking

Платформа бронювання спортивних майданчиків.

## Порти

| Сервіс | Адреса |
|--------|--------|
| ApiGateway | `http://localhost:5000` |
| AuthService | `http://localhost:5001` |
| CatalogService | `http://localhost:5002` |
| Monolith | `http://localhost:5100` |
| MySQL | `localhost:3306` |

## Підготовка MySQL

Рядки підключення зберігаються у User Secrets (без паролів у коді).

```powershell
# Monolith
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=SportsBooking;User=root;Password=YOUR_PASSWORD" --project src/Monolith

# AuthService
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=SportsBooking_Auth;User=root;Password=YOUR_PASSWORD" --project src/AuthService

# CatalogService
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=SportsBooking_Catalog;User=root;Password=YOUR_PASSWORD" --project src/CatalogService
```

Бази даних і таблиці створюються автоматично при першому запуску кожного сервісу.

## Запуск (4 термінали)

```powershell
# Термінал 1 — Моноліт
dotnet run --project src/Monolith --launch-profile http

# Термінал 2 — AuthService
dotnet run --project src/AuthService --launch-profile http

# Термінал 3 — CatalogService
dotnet run --project src/CatalogService --launch-profile http

# Термінал 4 — Gateway
dotnet run --project src/ApiGateway --launch-profile http
```

## Перевірка

```powershell
# Health-check всіх сервісів
Invoke-WebRequest http://localhost:5000/health   # Gateway
Invoke-WebRequest http://localhost:5001/health   # AuthService
Invoke-WebRequest http://localhost:5002/health   # CatalogService

# Реєстрація нового користувача
Invoke-RestMethod http://localhost:5000/api/v1/auth/register -Method POST `
  -ContentType 'application/json' `
  -Body '{"email":"user@test.com","password":"Pass123!","displayName":"Test User"}'

# Вхід (отримати токен)
$resp = Invoke-RestMethod http://localhost:5000/api/v1/auth/login -Method POST `
  -ContentType 'application/json' `
  -Body '{"email":"admin@sportsbooking.com","password":"Admin123!"}'
$token = $resp.accessToken

# Каталог майданчиків (без токена — анонімний доступ)
Invoke-RestMethod http://localhost:5000/api/v1/catalog/items

# Профіль поточного юзера (з токеном)
Invoke-RestMethod http://localhost:5000/api/v1/users/me `
  -Headers @{ Authorization = "Bearer $token" }
```

## Swagger UI

- AuthService: `http://localhost:5001/swagger`
- CatalogService: `http://localhost:5002/swagger`
- Monolith: `http://localhost:5100/swagger`

> Для перевірки через Gateway використовуйте PowerShell або Postman — Swagger кожного сервісу звертається напряму, а не через шлюз.

## Маршрути Gateway

| Upstream (клієнт → :5000) | Downstream (шлюз → сервіс) |
|---------------------------|---------------------------|
| `/api/v1/auth/**` | AuthService `:5001` |
| `/api/v1/users/**` | AuthService `:5001` |
| `/api/v1/catalog/**` | CatalogService `:5002` |
| `/api/**` | Monolith `:5100` |

## Документація

- [Інвентаризація моноліту](docs/monolith-inventory.md)
- [ADR-0001. Поділ моноліту на сервіси](docs/adr/0001-split-monolith.md)
- [ADR-0002. Шлюз Ocelot перед монолітом](docs/adr/0002-api-gateway.md)
- [Діаграми архітектури](docs/diagrams/README.md)
