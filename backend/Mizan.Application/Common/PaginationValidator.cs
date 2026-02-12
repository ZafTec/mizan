using FluentValidation;

namespace Mizan.Application.Common;

public static class PaginationValidatorExtensions
{
    public static IRuleBuilderOptions<T, int> ValidPage<T>(this IRuleBuilder<T, int> ruleBuilder)
    {
        return ruleBuilder
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be at least 1");
    }

    public static IRuleBuilderOptions<T, int> ValidPageSize<T>(this IRuleBuilder<T, int> ruleBuilder)
    {
        return ruleBuilder
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100");
    }

    public static IRuleBuilderOptions<T, string?> ValidSortOrder<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .Must(v => v is null or "asc" or "desc")
            .WithMessage("SortOrder must be 'asc' or 'desc'");
    }
}
