# SportsBooking

Платформа бронювання спортивних майданчиків.

## Порти

- Моноліт: `http://localhost:5100`
- ApiGateway: `http://localhost:5000`
- MySQL: `localhost:3306`

## Підготовка MySQL

Моноліт використовує MySQL. Рядок підключення зберігається у User Secrets, тому пароля немає у файлах проєкту.

Якщо запускаєте проєкт на іншому комп'ютері, задайте своє підключення:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=SportsBooking;User=root;Password=YOUR_PASSWORD" --project src/Monolith
```

При першому запуску застосунок створить базу `SportsBooking` і таблиці `Users`, `Venues`, `Bookings`.

## Запуск моноліту

```powershell
dotnet run --project src/Monolith --launch-profile http
```

Swagger моноліту: `http://localhost:5100/swagger`

Відкрийте цю адресу в браузері, щоб переглянути та вручну викликати API
моноліту через кнопку **Try it out**. Це корисно для перевірки самого
моноліту, але не перевіряє проходження запиту через gateway.

## Запуск шлюзу

У другому терміналі:

```powershell
dotnet run --project src/ApiGateway --launch-profile http
```

Шлюз працює на `http://localhost:5000` і пересилає запити `/api/*` на моноліт.
Адреса моноліту задається в `src/ApiGateway/appsettings.json` (`Gateway:MonolithHost`,
`Gateway:MonolithPort`) або через змінні середовища `Gateway__MonolithHost` і
`Gateway__MonolithPort`.

## Швидка перевірка у браузері

Після запуску **обох** застосунків відкрийте:

- `http://localhost:5000/health` — відповідь gateway `{"status":"OK"}`;
- `http://localhost:5000/api/venues` — список майданчиків, отриманий через gateway;
- `http://localhost:5100/api/venues` — той самий список напряму з моноліту.

Дві останні відповіді мають збігатися. Адресу `5000` надалі використовує
клієнт; адресу моноліту `5100` клієнтові знати не потрібно.

Звичайний адресний рядок браузера надсилає лише `GET`-запити. Для перевірки
`POST`, `PUT` або `DELETE` через gateway використовуйте команди нижче або
будь-який HTTP-клієнт (Postman, Bruno тощо). Swagger за адресою `:5100/swagger`
належить моноліту, тому його **Try it out** звертається напряму до `:5100`.

## Перевірка шлюзу

```powershell
Invoke-WebRequest http://localhost:5000/health
Invoke-WebRequest http://localhost:5000/api/venues
```

Перший запит повертає `200 OK`. Другий запит через шлюз повертає список спортивних майданчиків з моноліту.

## Документація

- [Інвентаризація моноліту](docs/monolith-inventory.md)
- [ADR-0001. Поділ моноліту на сервіси](docs/adr/0001-split-monolith.md)
- [ADR-0002. Шлюз Ocelot перед монолітом](docs/adr/0002-api-gateway.md)
- [Діаграми архітектури (Mermaid)](docs/diagrams/README.md) та вихідний файл [architecture.drawio](docs/diagrams/architecture.drawio)
- [Сценарій демонстрації для захисту](docs/demo-guide.md)
- [Покрокова перевірка роботи для захисту](docs/verification-guide.md)
