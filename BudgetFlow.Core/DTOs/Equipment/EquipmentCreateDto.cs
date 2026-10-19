using System.ComponentModel.DataAnnotations;

namespace BudgetFlow.Core.DTOs;

// Request: a new item always starts as Available
public class EquipmentCreateDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }
}
