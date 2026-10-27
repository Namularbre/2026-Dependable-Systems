namespace MyShop.Domain.Common;

public readonly record struct Money(decimal Amount)
{
    public static readonly Money Zero = new(0m);

    public static Result<Money> Create(decimal amount) =>
        amount < 0
            ? Error.Validation("money.negative", "A price cannot be negative.")
            : new Money(decimal.Round(amount, 2));

    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);
    public static Money operator *(Money a, Quantity q) => new(a.Amount * q.Value);

    public override string ToString() => Amount.ToString("0.00");
}

public readonly record struct Quantity(int Value)
{
    public static Result<Quantity> Create(int value) =>
        value <= 0
            ? Error.Validation("quantity.not_positive", "Quantity must be greater than zero.")
            : new Quantity(value);

    public static Quantity operator +(Quantity a, Quantity b) => new(a.Value + b.Value);

    public override string ToString() => Value.ToString();
}
