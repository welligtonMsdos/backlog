using Backlog.Domain;
using FluentValidation;

namespace Backlog.Api;

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterUserRequest(string Name, string Email, string Password, string Role, bool IsActive = true);

public sealed record UserResponse(Guid Id, string Name, string Email, string Role, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static UserResponse From(User user)
    {
        return new(user.Id, user.Name, user.Email, user.Role, user.IsActive, user.CreatedAt, user.UpdatedAt);
    }
}

public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);

        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class RegisterUserValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);

        RuleFor(x => x.Password).NotEmpty().Length(12, 128);

        RuleFor(x => x.Role).Must(role => Roles.All.Contains(role)).WithMessage("Perfil inválido.");
    }
}

public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<T>().Single();

        var validation = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.Errors.GroupBy(x => x.PropertyName).ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).ToArray()));
        }

        return await next(context);
    }
}
