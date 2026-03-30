using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Todo.Api.Data;
using Todo.Api.Dtos.Todos;
using Todo.Api.Entities;

namespace Todo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TodosController : ControllerBase
{
    private readonly AppDbContext _db;

    public TodosController(AppDbContext db)
    {
        _db = db;
    }

    // -----------------------------------------------------------------------------
    // GET /api/todos/public
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResponse<TodoResponse>>> GetPublic(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string status = "all",
        [FromQuery] string? priority = null,
        [FromQuery] string? dueFrom = null,
        [FromQuery] string? dueTo = null,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDir = "desc",
        [FromQuery] string? search = null)
    {
        var validationError = ValidateListParams(page, pageSize, status, priority, dueFrom, dueTo, sortBy, sortDir, search);
        if (validationError is not null) return validationError;

        var query = _db.TodoItems.AsNoTracking().Where(t => t.IsPublic);

        query = ApplyFilters(query, status, priority, dueFrom, dueTo, search);
        query = ApplySorting(query, sortBy, sortDir);

        var totalItems = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => ToResponse(t))
            .ToListAsync();

        return Ok(new PagedResponse<TodoResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        });
    }

    // -----------------------------------------------------------------------------
    // auth required
    // GET /api/todos
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PagedResponse<TodoResponse>>> GetMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string status = "all",
        [FromQuery] string? priority = null,
        [FromQuery] string? dueFrom = null,
        [FromQuery] string? dueTo = null,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDir = "desc",
        [FromQuery] string? search = null)
    {
        var validationError = ValidateListParams(page, pageSize, status, priority, dueFrom, dueTo, sortBy, sortDir, search);
        if (validationError is not null) return validationError;

        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var query = _db.TodoItems.AsNoTracking().Where(t => t.UserId == userId);

        query = ApplyFilters(query, status, priority, dueFrom, dueTo, search);
        query = ApplySorting(query, sortBy, sortDir);

        var totalItems = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => ToResponse(t))
            .ToListAsync();

        return Ok(new PagedResponse<TodoResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        });
    }

    // -----------------------------------------------------------------------------
    // GET /api/todos/{id}
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<TodoResponse>> GetById(Guid id)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var todo = await _db.TodoItems.FirstOrDefaultAsync(t => t.Id == id);
        if (todo is null) return NotFound();
        if (todo.UserId != userId) return Forbid();

        return Ok(ToResponse(todo));
    }

    // -----------------------------------------------------------------------------
    // POST /api/todos
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<TodoResponse>> Create(CreateTodoRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        if (!Enum.TryParse<TodoPriority>(request.Priority, true, out var prio))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["priority"] = new[] { "priority must be low, medium, or high" }
            }));

        var todo = new TodoItem
        {
            UserId = userId.Value,
            Title = request.Title,
            Details = request.Details,
            Priority = prio,
            DueDate = request.DueDate,
            IsPublic = request.IsPublic,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.TodoItems.Add(todo);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = todo.Id }, ToResponse(todo));
    }

    // -----------------------------------------------------------------------------
    // PUT /api/todos/{id}
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<TodoResponse>> Update(Guid id, UpdateTodoRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var todo = await _db.TodoItems.FirstOrDefaultAsync(t => t.Id == id);
        if (todo is null) return NotFound();
        if (todo.UserId != userId) return Forbid();

        if (!Enum.TryParse<TodoPriority>(request.Priority, true, out var prio))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["priority"] = new[] { "priority must be low, medium, or high" }
            }));

        todo.Title = request.Title;
        todo.Details = request.Details;
        todo.Priority = prio;
        todo.DueDate = request.DueDate;
        todo.IsPublic = request.IsPublic;
        todo.IsCompleted = request.IsCompleted;
        todo.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ToResponse(todo));
    }

    // -----------------------------------------------------------------------------
    // PATCH /api/todos/{id}/completion
    [HttpPatch("{id:guid}/completion")]
    [Authorize]
    public async Task<ActionResult<TodoResponse>> SetCompletion(Guid id, SetCompletionRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var todo = await _db.TodoItems.FirstOrDefaultAsync(t => t.Id == id);
        if (todo is null) return NotFound();
        if (todo.UserId != userId) return Forbid();

        todo.IsCompleted = request.IsCompleted;
        todo.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ToResponse(todo));
    }

    // -----------------------------------------------------------------------------
    // DELETE /api/todos/{id}
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var todo = await _db.TodoItems.FirstOrDefaultAsync(t => t.Id == id);
        if (todo is null) return NotFound();
        if (todo.UserId != userId) return Forbid();

        _db.TodoItems.Remove(todo);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // -----------------------------------------------------------------------------
    // Validate/map  
    private Guid? GetUserId()
    {
        var claim = User.FindFirst("uid")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private static TodoResponse ToResponse(TodoItem t) => new()
    {
        Id = t.Id,
        Title = t.Title,
        Details = t.Details,
        Priority = t.Priority.ToString().ToLowerInvariant(),
        DueDate = t.DueDate,
        IsCompleted = t.IsCompleted,
        IsPublic = t.IsPublic,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };

    private ActionResult? ValidateListParams(
        int page,
        int pageSize,
        string status,
        string? priority,
        string? dueFrom,
        string? dueTo,
        string sortBy,
        string sortDir,
        string? search)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1)
            errors["page"] = new[] { "page must be >= 1" };

        if (pageSize < 1 || pageSize > 50)
            errors["pageSize"] = new[] { "pageSize must be between 1 and 50" };

        if (status is not ("all" or "active" or "completed"))
            errors["status"] = new[] { "status must be all, active, or completed" };

        if (!string.IsNullOrWhiteSpace(priority) &&
            !Enum.TryParse<TodoPriority>(priority, true, out _))
            errors["priority"] = new[] { "priority must be low, medium, or high" };

        if (!string.IsNullOrWhiteSpace(dueFrom) && !DateOnly.TryParse(dueFrom, out _))
            errors["dueFrom"] = new[] { "dueFrom must be YYYY-MM-DD" };

        if (!string.IsNullOrWhiteSpace(dueTo) && !DateOnly.TryParse(dueTo, out _))
            errors["dueTo"] = new[] { "dueTo must be YYYY-MM-DD" };

        if (sortBy is not ("createdAt" or "dueDate" or "priority" or "title"))
            errors["sortBy"] = new[] { "sortBy must be createdAt, dueDate, priority, or title" };

        if (sortDir is not ("asc" or "desc"))
            errors["sortDir"] = new[] { "sortDir must be asc or desc" };

        if (!string.IsNullOrWhiteSpace(search) && search.Length > 100)
            errors["search"] = new[] { "search must be <= 100 chars" };

        if (errors.Count > 0)
            return BadRequest(new ValidationProblemDetails(errors));

        return null;
    }

    private static IQueryable<TodoItem> ApplyFilters(
        IQueryable<TodoItem> query,
        string status,
        string? priority,
        string? dueFrom,
        string? dueTo,
        string? search)
    {
        if (status == "active") query = query.Where(t => !t.IsCompleted);
        if (status == "completed") query = query.Where(t => t.IsCompleted);

        if (!string.IsNullOrWhiteSpace(priority) &&
            Enum.TryParse<TodoPriority>(priority, true, out var prio))
        {
            query = query.Where(t => t.Priority == prio);
        }

        if (!string.IsNullOrWhiteSpace(dueFrom) && DateOnly.TryParse(dueFrom, out var df))
            query = query.Where(t => t.DueDate >= df);

        if (!string.IsNullOrWhiteSpace(dueTo) && DateOnly.TryParse(dueTo, out var dt))
            query = query.Where(t => t.DueDate <= dt);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(t =>
                t.Title.ToLower().Contains(term) ||
                (t.Details != null && t.Details.ToLower().Contains(term)));
        }

        return query;
    }

    private static IQueryable<TodoItem> ApplySorting(
        IQueryable<TodoItem> query,
        string sortBy,
        string sortDir)
    {
        var desc = sortDir == "desc";

        return sortBy switch
        {
            "dueDate" => desc ? query.OrderByDescending(t => t.DueDate) : query.OrderBy(t => t.DueDate),
            "priority" => desc ? query.OrderByDescending(t => t.Priority) : query.OrderBy(t => t.Priority),
            "title" => desc ? query.OrderByDescending(t => t.Title) : query.OrderBy(t => t.Title),
            _ => desc ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt)
        };
    }
}
