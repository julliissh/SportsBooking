using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ApiGateway.Filters;

public class VenueDetailsOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath ?? string.Empty;
        if (!path.Contains("api/gateway/venues/{venueId}/details", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (operation.Responses.TryGetValue("200", out var response200) &&
            response200.Content.TryGetValue("application/json", out var mediaType200))
        {
            mediaType200.Examples.Clear();
            mediaType200.Examples.Add("active-booking", new OpenApiExample
            {
                Summary = "200 OK — Активне бронювання знайдено",
                Description = "Повна відповідь: знайдено майданчик та активне бронювання поточного користувача.",
                Value = new OpenApiObject
                {
                    ["venue"] = new OpenApiObject
                    {
                        ["id"] = new OpenApiInteger(1),
                        ["name"] = new OpenApiString("Olympic Arena"),
                        ["address"] = new OpenApiString("вул. Спортивна, 1"),
                        ["pricePerHour"] = new OpenApiDouble(450.00),
                        ["description"] = new OpenApiString("Сучасний спорткомплекс"),
                        ["category"] = new OpenApiString("Multipurpose")
                    },
                    ["myActiveBooking"] = new OpenApiObject
                    {
                        ["id"] = new OpenApiInteger(101),
                        ["venueId"] = new OpenApiInteger(1),
                        ["startTime"] = new OpenApiString("2027-06-01T10:00:00Z"),
                        ["hours"] = new OpenApiInteger(2),
                        ["totalPrice"] = new OpenApiDouble(900.00),
                        ["status"] = new OpenApiString("Confirmed"),
                        ["createdAt"] = new OpenApiString("2026-10-25T14:30:00Z")
                    },
                    ["bookingStatus"] = new OpenApiString("active")
                }
            });

            mediaType200.Examples.Add("unavailable-booking", new OpenApiExample
            {
                Summary = "200 OK — Часткова деградація (BookingService недоступний)",
                Description = "Вимога 3.2: якщо BookingService недоступний/таймаут, шлюз повертає 200 із bookingStatus='unavailable' та myActiveBooking=null.",
                Value = new OpenApiObject
                {
                    ["venue"] = new OpenApiObject
                    {
                        ["id"] = new OpenApiInteger(1),
                        ["name"] = new OpenApiString("Olympic Arena"),
                        ["address"] = new OpenApiString("вул. Спортивна, 1"),
                        ["pricePerHour"] = new OpenApiDouble(450.00),
                        ["description"] = new OpenApiString("Сучасний спорткомплекс"),
                        ["category"] = new OpenApiString("Multipurpose")
                    },
                    ["myActiveBooking"] = new OpenApiNull(),
                    ["bookingStatus"] = new OpenApiString("unavailable")
                }
            });

            mediaType200.Examples.Add("none-booking", new OpenApiExample
            {
                Summary = "200 OK — Бронювання відсутнє",
                Description = "BookingService успішно відповів 204 No Content (користувач не має активних бронювань на цей майданчик).",
                Value = new OpenApiObject
                {
                    ["venue"] = new OpenApiObject
                    {
                        ["id"] = new OpenApiInteger(1),
                        ["name"] = new OpenApiString("Olympic Arena"),
                        ["address"] = new OpenApiString("вул. Спортивна, 1"),
                        ["pricePerHour"] = new OpenApiDouble(450.00),
                        ["description"] = new OpenApiString("Сучасний спорткомплекс"),
                        ["category"] = new OpenApiString("Multipurpose")
                    },
                    ["myActiveBooking"] = new OpenApiNull(),
                    ["bookingStatus"] = new OpenApiString("none")
                }
            });
        }

        if (operation.Responses.TryGetValue("401", out var response401) &&
            response401.Content.TryGetValue("application/json", out var mediaType401))
        {
            mediaType401.Examples.Clear();
            mediaType401.Examples.Add("unauthorized", new OpenApiExample
            {
                Summary = "401 Unauthorized — Відсутній або невалідний токен",
                Description = "Запит надіслано без заголовка Authorization або з невалідним JWT токеном. Повертається ProblemDetails.",
                Value = new OpenApiObject
                {
                    ["type"] = new OpenApiString("https://tools.ietf.org/html/rfc9110#section-15.5.2"),
                    ["title"] = new OpenApiString("Unauthorized"),
                    ["status"] = new OpenApiInteger(401),
                    ["detail"] = new OpenApiString("Bearer token is missing or invalid."),
                    ["instance"] = new OpenApiString("/api/gateway/venues/1/details")
                }
            });
        }

        if (operation.Responses.TryGetValue("503", out var response503) &&
            response503.Content.TryGetValue("application/json", out var mediaType503))
        {
            mediaType503.Examples.Clear();
            mediaType503.Examples.Add("catalog-unavailable", new OpenApiExample
            {
                Summary = "503 Service Unavailable — CatalogService недоступний",
                Description = "Основна сутність не отримана (CatalogService лежить або timeout > 2s). Повертається ProblemDetails.",
                Value = new OpenApiObject
                {
                    ["type"] = new OpenApiString("https://tools.ietf.org/html/rfc9110#section-15.6.4"),
                    ["title"] = new OpenApiString("Catalog service unavailable"),
                    ["status"] = new OpenApiInteger(503),
                    ["detail"] = new OpenApiString("The catalog service is currently unreachable or timed out."),
                    ["instance"] = new OpenApiString("/api/gateway/venues/1/details")
                }
            });
        }

        if (operation.Responses.TryGetValue("404", out var response404) &&
            response404.Content.TryGetValue("application/json", out var mediaType404))
        {
            mediaType404.Examples.Clear();
            mediaType404.Examples.Add("venue-not-found", new OpenApiExample
            {
                Summary = "404 Not Found — Майданчик не існує",
                Description = "Майданчик із вказаним id не знайдено в CatalogService.",
                Value = new OpenApiObject
                {
                    ["type"] = new OpenApiString("https://tools.ietf.org/html/rfc9110#section-15.5.5"),
                    ["title"] = new OpenApiString("Venue not found"),
                    ["status"] = new OpenApiInteger(404),
                    ["detail"] = new OpenApiString("Venue with id 999 was not found in catalog."),
                    ["instance"] = new OpenApiString("/api/gateway/venues/999/details")
                }
            });
        }

        if (operation.Responses.TryGetValue("429", out var response429) &&
            response429.Content.TryGetValue("application/json", out var mediaType429))
        {
            mediaType429.Examples.Clear();
            mediaType429.Examples.Add("rate-limit-exceeded", new OpenApiExample
            {
                Summary = "429 Too Many Requests — Перевищено ліміт",
                Description = "Перевищено ліміт запитів у поточному хвилинному вікні. Містить заголовок Retry-After.",
                Value = new OpenApiObject
                {
                    ["type"] = new OpenApiString("https://tools.ietf.org/html/rfc6585#section-4"),
                    ["title"] = new OpenApiString("Too Many Requests"),
                    ["status"] = new OpenApiInteger(429),
                    ["detail"] = new OpenApiString("Rate limit exceeded. Please retry later."),
                    ["instance"] = new OpenApiString("/api/gateway/venues/1/details")
                }
            });
        }
    }
}

public class OcelotRoutesDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // 1. POST /api/v1/auth/login
        swaggerDoc.Paths["/api/v1/auth/login"] = new OpenApiPathItem
        {
            Operations =
            {
                [OperationType.Post] = new OpenApiOperation
                {
                    Tags = new List<OpenApiTag> { new() { Name = "Auth (Ocelot Route)" } },
                    Summary = "Вхід користувача (отримати JWT accessToken) [Ліміт: 10/хв за IP]",
                    RequestBody = new OpenApiRequestBody
                    {
                        Required = true,
                        Content =
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Example = new OpenApiObject
                                {
                                    ["email"] = new OpenApiString("admin@sportsbooking.com"),
                                    ["password"] = new OpenApiString("Admin123!")
                                }
                            }
                        }
                    },
                    Responses = new OpenApiResponses
                    {
                        ["200"] = new OpenApiResponse { Description = "Успішний вхід — повертає accessToken" },
                        ["401"] = new OpenApiResponse { Description = "Невірний логін або пароль" },
                        ["429"] = new OpenApiResponse { Description = "Перевищено ліміт (10 спроб/хв за IP) + Retry-After" }
                    }
                }
            }
        };

        // 2. GET /api/v1/catalog/items та POST /api/v1/catalog/items
        swaggerDoc.Paths["/api/v1/catalog/items"] = new OpenApiPathItem
        {
            Operations =
            {
                [OperationType.Get] = new OpenApiOperation
                {
                    Tags = new List<OpenApiTag> { new() { Name = "Catalog (Ocelot Route + Cache)" } },
                    Summary = "Отримати список спортивних майданчиків [Кеш IMemoryCache TTL 30 с, заголовок X-Cache: MISS/HIT]",
                    Responses = new OpenApiResponses
                    {
                        ["200"] = new OpenApiResponse { Description = "Список майданчиків (із заголовок X-Cache: MISS або HIT)" }
                    }
                }
            }
        };

        // 3. GET / PUT / DELETE /api/v1/catalog/items/{id}
        var idParam = new OpenApiParameter
        {
            Name = "id",
            In = ParameterLocation.Path,
            Required = true,
            Schema = new OpenApiSchema { Type = "integer", Default = new OpenApiInteger(1) }
        };

        swaggerDoc.Paths["/api/v1/catalog/items/{id}"] = new OpenApiPathItem
        {
            Operations =
            {
                [OperationType.Get] = new OpenApiOperation
                {
                    Tags = new List<OpenApiTag> { new() { Name = "Catalog (Ocelot Route + Cache)" } },
                    Summary = "Отримати майданчик за ID [Кеш TTL 30 с]",
                    Parameters = new List<OpenApiParameter> { idParam },
                    Responses = new OpenApiResponses
                    {
                        ["200"] = new OpenApiResponse { Description = "Деталі майданчика" },
                        ["404"] = new OpenApiResponse { Description = "Майданчик не знайдено" }
                    }
                },
                [OperationType.Put] = new OpenApiOperation
                {
                    Tags = new List<OpenApiTag> { new() { Name = "Catalog (Ocelot Route + Cache)" } },
                    Summary = "Оновити майданчик (миттєво скидає кеш каталогу через Invalidate!)",
                    Parameters = new List<OpenApiParameter> { idParam },
                    RequestBody = new OpenApiRequestBody
                    {
                        Required = true,
                        Content =
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Example = new OpenApiObject
                                {
                                    ["name"] = new OpenApiString("Olympic Arena"),
                                    ["address"] = new OpenApiString("вул. Спортивна, 1"),
                                    ["pricePerHour"] = new OpenApiDouble(550.00),
                                    ["description"] = new OpenApiString("Оновлена ціна майданчика"),
                                    ["category"] = new OpenApiString("Multipurpose")
                                }
                            }
                        }
                    },
                    Responses = new OpenApiResponses
                    {
                        ["200"] = new OpenApiResponse { Description = "Майданчик оновлено, кеш каталогу миттєво очищено" },
                        ["401"] = new OpenApiResponse { Description = "Потрібен JWT-токен" }
                    }
                }
            }
        };

        // 4. GET / POST /api/bookings
        swaggerDoc.Paths["/api/bookings"] = new OpenApiPathItem
        {
            Operations =
            {
                [OperationType.Get] = new OpenApiOperation
                {
                    Tags = new List<OpenApiTag> { new() { Name = "Bookings (Ocelot Route)" } },
                    Summary = "Отримати власні бронювання [НЕ кешується, вимагає JWT]",
                    Responses = new OpenApiResponses
                    {
                        ["200"] = new OpenApiResponse { Description = "Список бронювань поточного користувача" },
                        ["401"] = new OpenApiResponse { Description = "401 ProblemDetails (відсікається на шлюзі)" }
                    }
                },
                [OperationType.Post] = new OpenApiOperation
                {
                    Tags = new List<OpenApiTag> { new() { Name = "Bookings (Ocelot Route)" } },
                    Summary = "Створити бронювання [Ліміт: 10/хв за UserId]",
                    RequestBody = new OpenApiRequestBody
                    {
                        Required = true,
                        Content =
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Example = new OpenApiObject
                                {
                                    ["venueId"] = new OpenApiInteger(1),
                                    ["startTime"] = new OpenApiString("2027-07-01T14:00:00Z"),
                                    ["hours"] = new OpenApiInteger(2)
                                }
                            }
                        }
                    },
                    Responses = new OpenApiResponses
                    {
                        ["201"] = new OpenApiResponse { Description = "Бронювання створено" },
                        ["401"] = new OpenApiResponse { Description = "Потрібен JWT-токен" },
                        ["429"] = new OpenApiResponse { Description = "Перевищено ліміт (10/хв за UserId)" }
                    }
                }
            }
        };
    }
}
