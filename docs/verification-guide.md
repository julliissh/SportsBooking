# Покрокова перевірка ЛР0

Цей сценарій можна використовувати на захисті. Відкрийте три термінали в корені
репозиторію: перший — для моноліту, другий — для gateway, третій — для команд
перевірки. Браузер зручно використовувати для `GET`; для створення та видалення
даних використовуйте PowerShell-команди нижче.

## 0. Запуск застосунків

У терміналі №1 запустіть моноліт:

```powershell
dotnet run --project src/Monolith --launch-profile http
```

Дочекайтеся повідомлення `Now listening on: http://localhost:5100`.

У терміналі №2 запустіть gateway:

```powershell
dotnet run --project src/ApiGateway --launch-profile http
```

Дочекайтеся повідомлення `Now listening on: http://localhost:5000`.

## 1. Health-check gateway

Відкрийте в браузері [http://localhost:5000/health](http://localhost:5000/health).

Очікуваний результат: статус `200 OK` і тіло:

```json
{"status":"OK"}
```

## 2. Однакова відповідь напряму і через gateway

Відкрийте в двох вкладках браузера:

- [http://localhost:5100/api/venues](http://localhost:5100/api/venues) — прямий запит до моноліту;
- [http://localhost:5000/api/venues](http://localhost:5000/api/venues) — такий самий запит через gateway.

Очікуваний результат: однаковий JSON зі списком майданчиків. Друга адреса
доводить, що gateway перенаправив `GET /api/venues` на моноліт.

## 3. Лог перенаправлення

Подивіться на термінал №2 після запиту через gateway. Там має з’явитися рядок
приблизно такого вигляду:

```text
GET /api/venues -> http://localhost:5100; 200; 18 ms [CorrelationId: ...]
```

Він містить метод, вхідний шлях, отримувача, HTTP-код відповіді, тривалість і
наскрізний ідентифікатор запиту.

## 4. Три різні запити через gateway

Адресний рядок браузера підтримує лише `GET`, тому для `POST` і `DELETE`
використайте третій термінал. Усі запити нижче проходять через `:5000`.

```powershell
$venue = Invoke-RestMethod -Method Post -Uri "http://localhost:5000/api/venues" -ContentType "application/json" -Body '{"name":"Demo Arena","address":"Kyiv","pricePerHour":500}'
```

```powershell
$username = "student$(Get-Random -Minimum 10000 -Maximum 99999)"
$user = Invoke-RestMethod -Method Post -Uri "http://localhost:5000/api/auth/register" -ContentType "application/json" -Body ('{"username":"' + $username + '","password":"12345"}')
```

```powershell
$booking = Invoke-RestMethod -Method Post -Uri "http://localhost:5000/api/bookings" -ContentType "application/json" -Body ('{"userId":' + $user.id + ',"venueId":' + $venue.id + ',"startTime":"2026-09-15T18:00:00","hours":2}')
```

```powershell
Invoke-WebRequest -UseBasicParsing -Method Delete -Uri ("http://localhost:5000/api/bookings/" + $booking.id)
```

Покажіть щонайменше три типи: `GET /api/venues`, `POST /api/venues` і
`DELETE /api/bookings/{id}`. У консолі gateway для кожного буде окремий лог.

## 5. Пріоритет явного маршруту

У `src/ApiGateway/Program.cs` тимчасово змініть тільки цей рядок:

```csharp
["Routes:0:DownstreamHostAndPorts:0:Port"] = monolithPort.ToString(),
```

на:

```csharp
["Routes:0:DownstreamHostAndPorts:0:Port"] = "5999",
```

Перезапустіть gateway у терміналі №2 і виконайте:

```powershell
Invoke-WebRequest -UseBasicParsing -Method Post -Uri "http://localhost:5000/api/auth/login" -ContentType "application/json" -Body '{"username":"test","password":"test"}'
```

Очікуваний результат: `502 Bad Gateway`, бо `5999` не існує. Це доводить, що
`/api/auth/{everything}` (перший явний маршрут) спрацював раніше, ніж загальний
`/api/{everything}`. Поверніть `monolithPort.ToString()` і ще раз перезапустіть
gateway.

## 6. Поведінка, коли моноліт вимкнено

Зупиніть моноліт у терміналі №1 (`Ctrl+C`), але gateway не зупиняйте. У браузері
відкрийте [http://localhost:5000/api/venues](http://localhost:5000/api/venues).

Очікуваний результат: `502 Bad Gateway`. Це зрозуміла помилка недоступності
моноліту; gateway сам продовжує працювати. Після демонстрації знову запустіть
моноліт.

## 7. Документація та пояснення рішень

На захисті відкрийте:

- `docs/adr/0001-split-monolith.md` — чому виділено `Auth`, `Venues`, `Bookings`;
- `docs/adr/0002-api-gateway.md` — чому обрано Ocelot;
- `docs/diagrams/architecture.drawio` — три вкладки: композиція, сценарій зараз,
  сценарій після поділу.

Поясніть, що зараз gateway тільки перенаправляє весь `/api/*` трафік до
моноліту, а після поділу в конфігурації маршруту буде змінюватися адреса
відповідного сервісу без зміни клієнта.
