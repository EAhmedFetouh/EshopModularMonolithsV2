
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Basket.Data.Configuration;

public class ShoppingCartConfiguration : IEntityTypeConfiguration<ShoppingCart>
{
    public void Configure(EntityTypeBuilder<ShoppingCart> builder)
    {
        builder.HasKey(sc => sc.Id);

        builder.Property(sc => sc.UserName).IsRequired().HasMaxLength(100);

        builder.HasIndex(sc=> sc.UserName).IsUnique();

        builder.HasMany(sc => sc.Items)
            .WithOne()
            .HasForeignKey(sci => sci.ShoppingCartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Items)
           .WithOne()
           .HasForeignKey(sc => sc.ShoppingCartId);
    }
}
