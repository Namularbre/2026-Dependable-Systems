namespace MyShop.Domain.Common;

/// <summary>Base for every aggregate: identity, audit timestamps and logical deletion.</summary>
public abstract record Entity(Guid Id)
{
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public bool IsDeleted { get; init; }
}
