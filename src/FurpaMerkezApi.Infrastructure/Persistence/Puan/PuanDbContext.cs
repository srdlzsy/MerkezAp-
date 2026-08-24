using FurpaMerkezApi.Infrastructure.Persistence.Puan.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Persistence.Puan;

public class PuanDbContext : DbContext
{
    public PuanDbContext(DbContextOptions<PuanDbContext> options) : base(options)
    {
    }

    public DbSet<InterbonusIndirimCek> InterbonusIndirimCeks => Set<InterbonusIndirimCek>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InterbonusIndirimCek>(entity =>
        {
            entity.ToTable("INTERBONUS_INDIRIMCEK");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Id).HasColumnName("ID");
            entity.Property(item => item.Cek_No).HasColumnName("Cek_No");
            entity.Property(item => item.Cari_Kod).HasColumnName("Cari_Kod");
            entity.Property(item => item.Tutar).HasColumnName("Tutar");
            entity.Property(item => item.Puan).HasColumnName("Puan");
            entity.Property(item => item.Baslangic).HasColumnName("Baslangic");
            entity.Property(item => item.Bitis).HasColumnName("Bitis");
            entity.Property(item => item.Flag).HasColumnName("Flag");
            entity.Property(item => item.Sube_Kodu).HasColumnName("Sube_Kodu");
            entity.Property(item => item.Kasa_No).HasColumnName("Kasa_No");
            entity.Property(item => item.Musteri_Flag).HasColumnName("Musteri_Flag");
            entity.Property(item => item.Belge_Tutar).HasColumnName("Belge_Tutar");
            entity.Property(item => item.DATECREATED).HasColumnName("DATECREATED");
            entity.Property(item => item.DATEMODIFIED).HasColumnName("DATEMODIFIED");
            entity.Property(item => item.SESSIONIDCREATED).HasColumnName("SESSIONIDCREATED");
            entity.Property(item => item.SESSIONIDMODIFIED).HasColumnName("SESSIONIDMODIFIED");
            entity.Property(item => item.Kdv).HasColumnName("Kdv");
            entity.Property(item => item.Kampanya_Kodu).HasColumnName("Kampanya_Kodu");
            entity.Property(item => item.FATURA_GUID).HasColumnName("FATURA_GUID");
            entity.Property(item => item.KULLANICI).HasColumnName("KULLANICI");
            entity.Property(item => item.KASA_RG).HasColumnName("KASA_RG");
            entity.Property(item => item.KASA_REF_NO).HasColumnName("KASA_REF_NO");
            entity.Property(item => item.BELGE_TIPI).HasColumnName("BELGE_TIPI");
            entity.Property(item => item.EKU_NO).HasColumnName("EKU_NO");

            entity.Property(item => item.Z_NO).HasColumnName("Z_NO");
            entity.Property(item => item.FIS_NO).HasColumnName("FIS_NO");
            entity.Property(item => item.Kart_Tipi).HasColumnName("Kart_Tipi");
        });
    }
}
