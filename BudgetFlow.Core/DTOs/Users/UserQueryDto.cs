using System.ComponentModel.DataAnnotations;

namespace BudgetFlow.Core.DTOs;

// Request (query string) for GET /api/users
public class UserQueryDto
{
    // name_asc (default), name_desc, created_asc, created_desc
    [RegularExpression("^(name|created)_(asc|desc)$", ErrorMessage = "Sort must be name_asc, name_desc, created_asc or created_desc.")]
    public string? Sort { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
