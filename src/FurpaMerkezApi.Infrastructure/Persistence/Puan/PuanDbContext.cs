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
            entity.ToTable("INTERBONUS_INDIRIM_CEK");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Id).HasColumnName("ID");
            entity.Property(item => item.Cek_No).HasColumnName("CEK_NO");
            entity.Property(item => item.Cari_Kod).HasColumnName("CARI_KOD");
            entity.Property(item => item.Tutar).HasColumnName("TUTAR");
            entity.Property(item => item.Puan).HasColumnName("PUAN");
            entity.Property(item => item.Baslangic).HasColumnName("BASLANGIC");
            entity.Property(item => item.Bitis).HasColumnName("BITIS");
            entity.Property(item => item.Flag).HasColumnName("FLAG");
            entity.Property(item => item.Sube_Kodu).HasColumnName("SUBE_KODU");
            entity.Property(item => item.Kasa_No).HasColumnName("KASA_NO");
            entity.Property(item => item.Musteri_Flag).HasColumnName("MUSTERI_FLAG");
            entity.Property(item => item.Belge_Tutar).HasColumnName("BELGE_TUTAR");
            entity.Property(item => item.DATECREATED).HasColumnName("DATECREATED");
            entity.Property(item => item.DATEMODIFIED).HasColumnName("DATEMODIFIED");
            entity.Property(item => item.SESSIONIDCREATED).HasColumnName("SESSIONIDCREATED");
            entity.Property(item => item.SESSIONIDMODIFIED).HasColumnName("SESSIONIDMODIFIED");
            entity.Property(item => item.Kdv).HasColumnName("KDV");
            entity.Property(item => item.Kampanya_Kodu).HasColumnName("KAMPANYA_KODU");
            entity.Property(item => item.FATURA_GUID).HasColumnName("FATURA_GUID");
            entity.Property(item => item.KULLANICI).HasColumnName("KULLANICI");
            entity.Property(item => item.KASA_RG).HasColumnName("KASA_RG");
            entity.Property(item => item.KASA_REF_NO).HasColumnName("KASA_REF_NO");
            entity.Property(item => item.BELGE_TIPI).HasColumnName("BELGE_TIPI");
            entity.Property(item => item.EKU_NO).HasColumnName("EKU_NO");       

            entity.Property(item => item.Z_NO).HasColumnName("Z_NO");
            entity.Property(item => item.FIS_NO).HasColumnName("FIS_NO");
            entity.Property(item => item.Kart_Tipi).HasColumnName("KART_TIPI");
        });
    }
}
