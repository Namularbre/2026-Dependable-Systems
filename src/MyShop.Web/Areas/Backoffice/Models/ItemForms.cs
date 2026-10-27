using System.ComponentModel.DataAnnotations;
using MyShop.Application;
using MyShop.Domain.Items;

namespace MyShop.Web.Areas.Backoffice.Models;

public sealed record ItemForm
{
    public Guid? Id { get; init; }

    [Required, StringLength(Item.NameMaxLength)]
    public string Name { get; init; } = "";

    [StringLength(Item.DescriptionMaxLength)]
    public string? Description { get; init; }

    [Range(0, 1_000_000), DataType(DataType.Currency)]
    public decimal Price { get; init; }

    [Range(0, 100_000), Display(Name = "Initial stock")]
    public int InitialStock { get; init; }

    [Display(Name = "Put on sale immediately")]
    public bool Publish { get; init; }

    public CreateItemCommand ToCreateCommand() => new(Name, Description ?? "", Price, InitialStock, Publish);

    public UpdateItemCommand ToUpdateCommand(Guid id) => new(id, Name, Description ?? "", Price);

    public static ItemForm From(ItemDto item) =>
        new() { Id = item.Id, Name = item.Name, Description = item.Description, Price = item.Price };
}

public sealed record ItemEditViewModel(ItemForm Form, ItemDto Item, IReadOnlyList<StockMovementDto> Movements);
