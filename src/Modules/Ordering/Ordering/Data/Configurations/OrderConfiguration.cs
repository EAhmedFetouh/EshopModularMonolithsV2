
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Orders.Models;

namespace Ordering.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {

        builder.HasKey(o => o.Id);

        builder.Property(o => o.CustomerId);

        builder.Property(o => o.OrderName)
               .IsRequired()
               .HasMaxLength(100);

        builder.HasIndex(o => o.OrderName)
               .IsUnique();

        builder.Property(o => o.TotalPrice)
               .HasColumnType("decimal(18,2)");

        builder.HasMany(s => s.Items)
               .WithOne()
               .HasForeignKey(si => si.OrderId);

        builder.ComplexProperty(o => o.ShippingAdress, sa =>
        {
            sa.Property(a => a.FirstName).HasMaxLength(50).IsRequired();
            sa.Property(a => a.LastName).HasMaxLength(50).IsRequired();
            sa.Property(a => a.EmailAddress).HasMaxLength(50);
            sa.Property(a => a.AddressLine).HasMaxLength(180).IsRequired();
            sa.Property(a => a.Country).HasMaxLength(50);
            sa.Property(a => a.State).HasMaxLength(50);
            sa.Property(a => a.ZipCode).HasMaxLength(5).IsRequired();
        });

        builder.ComplexProperty(o => o.BillingAddress, ba =>
        {
            ba.Property(a => a.FirstName).HasMaxLength(50).IsRequired();
            ba.Property(a => a.LastName).HasMaxLength(50).IsRequired();
            ba.Property(a => a.EmailAddress).HasMaxLength(50);
            ba.Property(a => a.AddressLine).HasMaxLength(180).IsRequired();
            ba.Property(a => a.Country).HasMaxLength(50);
            ba.Property(a => a.State).HasMaxLength(50);
            ba.Property(a => a.ZipCode).HasMaxLength(5).IsRequired();
        });


        builder.ComplexProperty(o => o.Payment, py =>
        {
            py.Property(p => p.CardNumber).HasMaxLength(24).IsRequired();
            py.Property(p => p.CardName).HasMaxLength(50);
            py.Property(p => p.Expiration).HasMaxLength(10);
            py.Property(p => p.CVV).HasMaxLength(3);
            py.Property(p => p.PaymentMethod);
        });


    }
}
