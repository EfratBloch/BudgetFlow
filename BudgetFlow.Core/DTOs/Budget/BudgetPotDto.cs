using BudgetFlow.Core.Enums;

namespace BudgetFlow.Core.DTOs;

// Response: one pot. Every number is computed on each request - nothing is stored.
public class BudgetPotDto
{
    public BudgetSource Source { get; set; }
    public string Label { get; set; } = string.Empty;   // e.g. "Activities"
    public string Formula { get; set; } = string.Empty; // e.g. "4 x 12 participants"
    public decimal Budget { get; set; }                 // the ceiling
    public decimal Spent { get; set; }
    public decimal Remaining { get; set; }              // Budget - Spent
}
