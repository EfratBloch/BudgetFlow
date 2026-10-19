using System.ComponentModel.DataAnnotations;
using BudgetFlow.Core.Enums;

namespace BudgetFlow.Core.DTOs;

// Request: POST /api/users/{guideId}/budget-additions - the guide comes from the route.
// Which pots are allowed for TargetPot and FromPot is checked in the Service layer.
public class BudgetAdditionCreateDto
{
    // Activity or Birthday
    [Required]
    public BudgetSource? TargetPot { get; set; }

    [Range(typeof(decimal), "0.01", "100000", ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Required, StringLength(200, MinimumLength = 2)]
    public string Reason { get; set; } = string.Empty;

    // The two buttons: Extra (new money) or Transfer (from one of the coordinator's pots)
    [Required]
    public BudgetAdditionKind? Kind { get; set; }

    // Required for Transfer (GuideMeetings or Annual), must be empty for Extra
    public BudgetSource? FromPot { get; set; }
}
