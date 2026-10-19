namespace FurpaMerkezApi.Application.Common.Validation;

public static class CreateQuantityLimits
{
    public const double Maximum = 100_000d;
    public const decimal MaximumDecimal = 100_000m;
    public const int MaximumInteger = 100_000;

    public static void EnsureWithinMaximum(double quantity, string parameterName)
    {
        if (!double.IsFinite(quantity) || quantity > Maximum)
        {
            throw new ArgumentException("Line quantity can not exceed 100000.", parameterName);
        }
    }

    public static void EnsureWithinMaximum(decimal quantity, string parameterName)
    {
        if (quantity > MaximumDecimal)
        {
            throw new ArgumentException("Line quantity can not exceed 100000.", parameterName);
        }
    }

    public static void EnsureWithinMaximum(int quantity, string parameterName)
    {
        if (quantity > MaximumInteger)
        {
            throw new ArgumentException("Line quantity can not exceed 100000.", parameterName);
        }
    }
}
