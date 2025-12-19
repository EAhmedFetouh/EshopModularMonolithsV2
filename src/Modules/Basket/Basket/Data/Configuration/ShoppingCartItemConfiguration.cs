
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Basket.Data.Configuration;

public class ShoppingCartItemConfiguration : IEntityTypeConfiguration<ShoppingCartItem>
{
    public void Configure(EntityTypeBuilder<ShoppingCartItem> builder)
    {
        builder.HasKey(sc => sc.Id);


        builder.Property(sc => sc.Quantity)
            .IsRequired();

        builder.Property(sc => sc.ProductId)
            .IsRequired();

        builder.Property(sc => sc.Price)
            .IsRequired();

        builder.Property(sc => sc.ProductName)
            .IsRequired();

        builder.Property(sc => sc.Color);
        builder.Property(sc => sc.Price);

       


    }
}
