using CatalogService.Dtos;
using CatalogService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Controllers;

[ApiController]
[Route("api/v1/catalog")]
public class CatalogController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public CatalogController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    /// <summary>Get venues list with pagination, search and sort</summary>
    [HttpGet("items")]
    [ProducesResponseType<VenueListDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool desc = false)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new { error = "Invalid pagination parameters" });

        var result = await _catalogService.GetVenuesAsync(page, pageSize, search, categoryId, sortBy, desc);
        return Ok(result);
    }

    /// <summary>Get venue by id</summary>
    [HttpGet("items/{id}")]
    [ProducesResponseType<VenueDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var venue = await _catalogService.GetByIdAsync(id);
        return venue is null ? NotFound() : Ok(venue);
    }

    /// <summary>Create a new venue (Admin only)</summary>
    [HttpPost("items")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<VenueDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateVenueRequest request)
    {
        var venue = await _catalogService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = venue.Id }, venue);
    }

    /// <summary>Update a venue (Admin only)</summary>
    [HttpPut("items/{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateVenueRequest request)
    {
        var ok = await _catalogService.UpdateAsync(id, request);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Soft-delete a venue (Admin only)</summary>
    [HttpDelete("items/{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _catalogService.DeleteAsync(id);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Get all categories</summary>
    [HttpGet("categories")]
    [ProducesResponseType<List<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _catalogService.GetCategoriesAsync();
        return Ok(categories);
    }
}
