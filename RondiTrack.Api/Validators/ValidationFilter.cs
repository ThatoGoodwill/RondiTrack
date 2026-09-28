using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using RondiTrack.Api.Exceptions;

namespace RondiTrack.Api.Validators;

// ---------------------------------------------------------------------------------
// RUNS AUTOMATICALLY BEFORE EVERY CONTROLLER ACTION (registered once in Program.cs).
// No controller ever calls a validator by hand.
//
// It does two jobs, in order:
//  1. If the request body could not even be READ (bad JSON, unknown enum text such as
//     "Daily", missing body) ASP.NET marks ModelState invalid -> we throw a
//     ValidationFailedException so the handler answers with our standard 400 shape.
//  2. For each action argument (the request DTO), find the matching IValidator<T>
//     and run it. If it fails -> throw FluentValidation's ValidationException, which
//     the handler maps to 400 (with per-field messages).
// If no validator exists for an argument (a Guid route value, a CancellationToken),
// it is simply skipped.
// ---------------------------------------------------------------------------------
public sealed class ValidationFilter(IServiceProvider serviceProvider) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // ---- Job 1: malformed / unreadable request -----------------------------
        if (!context.ModelState.IsValid)
        {
            var fields = context.ModelState
                .Where(e => e.Value is not null && e.Value.Errors.Count > 0 && !string.IsNullOrWhiteSpace(e.Key))
                .Select(e => e.Key)
                .Distinct()
                .ToList();

            var detail = "The request body is missing or malformed.";
            if (fields.Count > 0) detail += " Check: " + string.Join(", ", fields) + ".";

            throw new ValidationFailedException("request.malformed", detail);
        }

        // ---- Job 2: run the FluentValidation validator for each DTO argument ----
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            // Build IValidator<TheDtoType> at runtime, e.g. IValidator<UserRequest>.
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (serviceProvider.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument));
            if (!result.IsValid)
                throw new ValidationException(result.Errors);   // -> RondiTrackExceptionHandler -> 400
        }

        await next();   // everything is well-formed: run the actual controller action
    }
}
