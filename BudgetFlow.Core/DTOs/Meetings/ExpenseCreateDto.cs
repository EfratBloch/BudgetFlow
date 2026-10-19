using System.ComponentModel.DataAnnotations;

namespace BudgetFlow.Core.DTOs;

// Request: no BudgetSource here - the server decides the pot from the meeting type
public class ExpenseCreateDto
{
    [Range(typeof(decimal), "0.01", "100000", ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Required, StringLength(200, MinimumLength = 2)]
    public string Description { get; set; } = string.Empty;
}
