namespace BudgetFlow.Service.Common;

internal static class Email
{
    // Emails are compared case-insensitively: "Sarah@X.com" and "sarah@x.com" are one account.
    // Every service that saves or looks up an email goes through here.
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
