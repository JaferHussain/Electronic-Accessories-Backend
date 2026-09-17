namespace MoeezMobile.Api.Models.Common;

/// <summary>
/// A rule violation the user can act on. Carries a <see cref="MessageKeys"/> key plus any
/// format arguments; the exception middleware turns it into text in the caller's language
/// and returns it as a 400.
/// </summary>
public class BusinessException : Exception
{
    public string Key { get; }
    public object[] Args { get; }

    public BusinessException(string key, params object[] args) : base(key)
    {
        Key = key;
        Args = args;
    }
}

/// <summary>Requested record does not exist (404).</summary>
public class NotFoundException : Exception
{
    public string Key { get; }
    public object[] Args { get; }

    public NotFoundException(string key = MessageKeys.NotFound, params object[] args) : base(key)
    {
        Key = key;
        Args = args;
    }
}
