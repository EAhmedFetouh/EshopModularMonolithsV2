namespace Basket.Basket.Dtos;

public record ShoppingCartItemDto(
    Guid Id,
    Guid ShoppingCartId,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal Price,
    string Color
    );
