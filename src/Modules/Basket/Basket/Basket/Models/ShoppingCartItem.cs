
namespace Basket.Basket.Models;

public class ShoppingCartItem : Entity<Guid>
{
    public Guid ShoppingCartId { get; set; } = default!;
    public Guid ProductId { get; set; } = default!;
    public int Quantity { get; set; } = default!;
    public string Color { get; set; } = default!;

    // Will comes from Catalog Service
    public decimal Price { get; set; } = default!;
    public string ProductName { get; set; } = default!;


    public ShoppingCartItem(Guid shoppingCartId,Guid productId, int quantity, string color, decimal price, string productName)
    {
        ShoppingCartId = shoppingCartId;
        ProductId = productId;
        Quantity = quantity;
        Color = color;
        Price = price;
        ProductName = productName;
    }
}
