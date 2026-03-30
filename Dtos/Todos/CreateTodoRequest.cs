using System.ComponentModel.DataAnnotations;
namespace Todo.Api.Dtos.Todos;

public class CreateTodoRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Details { get; set; }

    [Required]
    [RegularExpression("(?i)^(low|medium|high)$")]
    public string Priority { get; set; } = "medium";
    public DateOnly? DueDate { get; set; }
    public bool IsPublic { get; set; }
}
