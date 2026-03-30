using System.ComponentModel.DataAnnotations;

namespace Todo.Api.Dtos.Todos;

public class SetCompletionRequest
{
    [Required]
    public bool IsCompleted { get; set; }
}
