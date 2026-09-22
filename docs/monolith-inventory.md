# Інвентаризація моноліту

| Контролер | Сутності | Таблиці | Хто ще їх читає / пише |
| :--- | :--- | :--- | :--- |
| `AuthController` | `User` | `Users` | `AuthController` реєструє та автентифікує користувачів. `BookingsController` логічно посилається на `UserId` користувача при створенні бронювання. |
| `VenuesController` | `Venue` | `Venues` | `VenuesController` додає та читає каталог майданчиків. `BookingsController` логічно посилається на `VenueId` при створенні бронювання. |
| `BookingsController` | `Booking` | `Bookings` | `BookingsController` створює, переглядає та скасовує бронювання. Залежить від користувачів (`UserId`) та майданчиків (`VenueId`). |
