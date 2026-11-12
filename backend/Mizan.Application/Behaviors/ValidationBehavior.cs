using FluentValidation;
using MediatR;

namespace Mizan.Application.Behaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);

            // One at a time: a validator may read through the request's DbContext, which allows one
            // operation at a time.
            var failures = new List<FluentValidation.Results.ValidationFailure>();
            foreach (var validator in _validators)
            {
                var result = await validator.ValidateAsync(context, cancellationToken);
                failures.AddRange(result.Errors.Where(f => f != null));
            }

            if (failures.Count != 0)
            {
                throw new ValidationException(failures);
            }
        }

        return await next();
    }
}
