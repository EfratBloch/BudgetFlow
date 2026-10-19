using BudgetFlow.Core.Interfaces.Services;

namespace BudgetFlow.Service.Security;

// BCrypt: a slow one-way hash with a random salt built into every hash,
// so two users with the same password get different hashes.
public class BCryptPasswordHasher : IPasswordHasher
{
    // Each +1 doubles the hashing time; 11 is about a tenth of a second
    private const int WorkFactor = 11;

    public string Hash(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string hash, string password)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // The stored value is not a BCrypt hash - treat it as a wrong password
            return false;
        }
    }
}
