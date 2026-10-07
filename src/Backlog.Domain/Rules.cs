namespace Backlog.Domain;

public static class Roles
{
    public const string Treasury = "tesouraria";

    public const string Developer = "desenvolvedor";

    public const string Manager = "gestor";

    public static readonly string[] All = [Treasury, Developer, Manager];
}

public static class TaskStatuses
{
    public const string Backlog = "backlog";

    public const string Development = "desenvolvimento";

    public const string Review = "homologação";

    public const string Deployment = "em implantação";

    public const string Finished = "finalizado";

    public static readonly string[] All = [Backlog, Development, Review, Deployment, Finished];
}

public enum ErrorKind
{
    Invalid,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    TooLarge,
    Unavailable
}

public sealed class DomainException(ErrorKind kind, string message) : Exception(message)
{
    public ErrorKind Kind { get; } = kind;
}

public sealed record Actor(Guid Id, string Role);
