namespace MoeezMobile.Api.Helpers;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public class PasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            // A null or empty PasswordHash throws ArgumentException rather than
            // SaltParseException, so it escaped the catch above and surfaced as a 500 from
            // the login endpoint instead of a clean credential rejection. A user row with an
            // empty hash is reachable through direct SQL or a partially seeded database.
            // Fixed under FR-020; the reproducing test was written first and watched failing.
            return false;
        }
    }
}
