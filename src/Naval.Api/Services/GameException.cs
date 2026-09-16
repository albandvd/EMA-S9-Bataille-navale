namespace Naval.Api.Services;

public sealed class GameException : Exception
{
    public string Code { get; }
    public bool IsConflict { get; }
    public bool IsForbidden { get; }
    public bool IsNotFound { get; }

    public GameException(string code, string message,
        bool isConflict = false, bool isForbidden = false, bool isNotFound = false)
        : base(message)
    {
        Code = code;
        IsConflict = isConflict;
        IsForbidden = isForbidden;
        IsNotFound = isNotFound || code == "GAME_NOT_FOUND";
    }
}
