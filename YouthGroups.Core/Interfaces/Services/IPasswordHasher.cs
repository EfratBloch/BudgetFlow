namespace YouthGroups.Core.Interfaces.Services;

// Turns a password into a one-way hash and checks a password against a stored hash.
// Defined in Core so both the seeder (Data) and AuthService (Service) can use it
// without either of them depending on the other.
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}
