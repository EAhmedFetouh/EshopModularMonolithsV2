using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Orders.Models;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
       public void Configure(EntityTypeBuilder<OrderItem> builder)
       {
              builder.HasKey(oi => oi.Id);

              builder.Property(oi => oi.ProductId)
                     .IsRequired();

              builder.Property(oi => oi.OrderId)
                     .IsRequired();

              builder.Property(oi => oi.Price)
                     .HasColumnType("decimal(18,2)")
                     .IsRequired();

              builder.Property(oi => oi.Quantity)
                     .IsRequired();
       }
}