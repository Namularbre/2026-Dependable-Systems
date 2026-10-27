using MyShop.Domain.Common;

namespace MyShop.Domain.Items;

public sealed record Item(
    Guid Id,
    string Name,
    string Description,
    Money Price,
    int Stock,
    bool IsPublished) : Entity(Id)
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    public bool IsAvailable => IsPublished && !IsDeleted && Stock > 0;

    public static Result<Item> Create(string name, string description, decimal price) =>
        Validate(name, description, price)
            .Map(p => new Item(Guid.CreateVersion7(), p.Name, p.Description, p.Price, Stock: 0, IsPublished: false));

    public Result<Item> Update(string name, string description, decimal price) =>
        EnsureNotDeleted()
            .Bind(_ => Validate(name, description, price))
            .Map(p => this with { Name = p.Name, Description = p.Description, Price = p.Price });

    public Result<Item> Refill(Quantity quantity) =>
        EnsureNotDeleted().Map(i => i with { Stock = i.Stock + quantity.Value });

    public Result<Item> Publish() => EnsureNotDeleted().Map(i => i with { IsPublished = true });

    public Result<Item> Unpublish() => EnsureNotDeleted().Map(i => i with { IsPublished = false });

    public Result<Item> Withdraw(Quantity quantity) =>
        EnsureSellable()
            .Ensure(i => i.Stock >= quantity.Value,
                Error.Conflict("item.insufficient_stock", $"Only {Stock} unit(s) of '{Name}' left."))
            .Map(i => i with { Stock = i.Stock - quantity.Value });

    public Result<Item> EnsureSellable() =>
        EnsureNotDeleted()
            .Ensure(i => i.IsPublished, Error.Validation("item.not_on_sale", $"'{Name}' is not on sale."));

    public Result<Item> Delete() => EnsureNotDeleted().Map(i => i with { IsDeleted = true, IsPublished = false });

    private Result<Item> EnsureNotDeleted() =>
        IsDeleted ? Error.NotFound("item.not_found", "Item not found.") : this;

    private sealed record ValidProps(string Name, string Description, Money Price);

    private static Result<ValidProps> Validate(string? name, string? description, decimal price) =>
        (name?.Trim() ?? "", description?.Trim() ?? "") switch
        {
            ("", _) => Error.Validation("item.name_required", "Name is required."),
            ({ Length: > NameMaxLength }, _) =>
                Error.Validation("item.name_too_long", $"Name is limited to {NameMaxLength} characters."),
            (_, { Length: > DescriptionMaxLength }) =>
                Error.Validation("item.description_too_long", $"Description is limited to {DescriptionMaxLength} characters."),
            var (n, d) => Money.Create(price).Map(p => new ValidProps(n, d, p))
        };
}
