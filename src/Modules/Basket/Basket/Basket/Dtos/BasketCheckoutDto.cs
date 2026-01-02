namespace Basket.Basket.Dtos;

public record BasketCheckoutDto
(
  string UserName,
    Guid CustomerId,
    decimal TotalPrice,
    // Shipping and billing Address
    string FirstName,
    string LastName,
    string EmailAddress,
    string AddressLine,
    string Country,
    string ZipCode,
    string State,
    // Payment
    string CardName,
    string CardNumber,
    string Expiration,
    string Cvv,
    int PaymentMethod
);
