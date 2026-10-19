namespace BudgetFlow.Core.Common;

// One row of the budget summary: a guide with her counts and totals, produced by a
// single aggregate query in the database (no loop over guides, no N+1).
public sealed record GuideBudgetRow(
    int UserId,
    string FullName,
    int ParticipantCount,
    decimal ActivitySpent,
    decimal BirthdaySpent,
    decimal ActivityAdded,   // budget additions from the coordinator
    decimal BirthdayAdded);
