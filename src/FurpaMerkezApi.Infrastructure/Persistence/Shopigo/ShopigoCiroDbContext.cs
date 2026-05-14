using FurpaMerkezApi.Infrastructure.Persistence.Shopigo.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Persistence.Shopigo;

public sealed class ShopigoCiroDbContext(DbContextOptions<ShopigoCiroDbContext> options) : DbContext(options)
{
    public DbSet<ShopigoBranch> Branches => Set<ShopigoBranch>();

    public DbSet<ShopigoEmployee> Employees => Set<ShopigoEmployee>();

    public DbSet<ShopigoPayment> Payments => Set<ShopigoPayment>();

    public DbSet<ShopigoPaymentMethod> PaymentMethods => Set<ShopigoPaymentMethod>();

    public DbSet<ShopigoReceivedSale> ReceivedSales => Set<ShopigoReceivedSale>();

    public DbSet<ShopigoSaleItem> SaleItems => Set<ShopigoSaleItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShopigoBranch>(entity =>
        {
            entity.ToTable("branches");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.DepoId).HasColumnName("depo_id");
            entity.Property(item => item.MarketId).HasColumnName("market_id");
            entity.Property(item => item.Name).HasColumnName("name");
            entity.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        });

        modelBuilder.Entity<ShopigoEmployee>(entity =>
        {
            entity.ToTable("employees");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Code).HasColumnName("code");
            entity.Property(item => item.Name).HasColumnName("name");
            entity.Property(item => item.Surname).HasColumnName("surname");
            entity.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        });

        modelBuilder.Entity<ShopigoPayment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.SaleUuid).HasColumnName("sale_uuid");
            entity.Property(item => item.PaymentMethod).HasColumnName("payment_method");
            entity.Property(item => item.Amount).HasColumnName("amount");
            entity.Property(item => item.Refunded).HasColumnName("refunded");
            entity.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        });

        modelBuilder.Entity<ShopigoPaymentMethod>(entity =>
        {
            entity.ToTable("payment_methods");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Name).HasColumnName("name");
            entity.Property(item => item.PavoType).HasColumnName("pavo_type");
            entity.Property(item => item.PavoMediator).HasColumnName("pavo_mediator");
            entity.Property(item => item.Status).HasColumnName("status");
        });

        modelBuilder.Entity<ShopigoReceivedSale>(entity =>
        {
            entity.ToTable("received_sales");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Uuid).HasColumnName("uuid");
            entity.Property(item => item.Status).HasColumnName("status");
            entity.Property(item => item.InitiatedBy).HasColumnName("initiated_by");
            entity.Property(item => item.ReceiptNumber).HasColumnName("receipt_number");
            entity.Property(item => item.TotalPrice).HasColumnName("total_price");
            entity.Property(item => item.RemainingAmount).HasColumnName("remaining_amount");
            entity.Property(item => item.ReceivedAt).HasColumnName("received_at");
            entity.Property(item => item.MarketId).HasColumnName("market_id");
            entity.Property(item => item.Subeno).HasColumnName("subeno");
            entity.Property(item => item.Kasano).HasColumnName("kasano");
            entity.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        });

        modelBuilder.Entity<ShopigoSaleItem>(entity =>
        {
            entity.ToTable("sale_items");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.SaleUuid).HasColumnName("sale_uuid");
            entity.Property(item => item.Quantity).HasColumnName("quantity");
            entity.Property(item => item.TotalPrice).HasColumnName("total_price");
            entity.Property(item => item.Refunded).HasColumnName("refunded");
            entity.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        });
    }
}
