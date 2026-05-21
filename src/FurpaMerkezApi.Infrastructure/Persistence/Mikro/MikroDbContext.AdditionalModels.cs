using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Persistence.Mikro;

public partial class MikroDbContext
{
    public virtual DbSet<CARI_HESAP_YETKILILERI> CARI_HESAP_YETKILILERIs { get; set; }

    public virtual DbSet<CARI_PERSONEL_TANIMLARI> CARI_PERSONEL_TANIMLARIs { get; set; }

    public virtual DbSet<EVRAK_ACIKLAMALARI> EVRAK_ACIKLAMALARIs { get; set; }

    public virtual DbSet<STOK_DEPO_DETAYLARI> STOK_DEPO_DETAYLARIs { get; set; }

    public virtual DbSet<STOK_SATIS_FIYAT_LISTE_TANIMLARI> STOK_SATIS_FIYAT_LISTE_TANIMLARIs { get; set; }

    public virtual DbSet<SummaryEntity> Summaries { get; set; }

    public virtual DbSet<BanknoteMovementEntity> BanknoteMovements { get; set; }

    public virtual DbSet<GiftCheckMovementEntity> GiftCheckMovements { get; set; }

    public virtual DbSet<BanknoteTrackEntity> BanknoteTracks { get; set; }

    public virtual DbSet<PaymentTypeEntity> PaymentTypes { get; set; }

    public virtual DbSet<BanknoteTypeEntity> BanknoteTypes { get; set; }

    public virtual DbSet<GiftCheckTypeEntity> GiftCheckTypes { get; set; }

    public virtual DbSet<CashRegisterDetailEntity> CashRegisterDetails { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CARI_HESAP_YETKILILERI>(entity =>
        {
            entity.HasKey(e => e.mye_Guid).HasName("NDX_CARI_HESAP_YETKILILERI_00");

            entity.ToTable("CARI_HESAP_YETKILILERI");

            entity.HasIndex(e => new { e.mye_cari_kod, e.mye_adres_no }, "NDX_CARI_HESAP_YETKILILERI_02");

            entity.HasIndex(e => new { e.mye_isim, e.mye_soyisim, e.mye_cari_kod, e.mye_adres_no }, "NDX_CARI_HESAP_YETKILILERI_03");

            entity.HasIndex(e => new { e.mye_soyisim, e.mye_isim, e.mye_cari_kod, e.mye_adres_no }, "NDX_CARI_HESAP_YETKILILERI_04");

            entity.Property(e => e.mye_Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.mye_KEP_adresi).HasMaxLength(80);
            entity.Property(e => e.mye_arac_plaka).HasMaxLength(15);
            entity.Property(e => e.mye_cari_kod).HasMaxLength(25);
            entity.Property(e => e.mye_cep_telno).HasMaxLength(17);
            entity.Property(e => e.mye_create_date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.mye_dahili_telno).HasMaxLength(5);
            entity.Property(e => e.mye_dogum_tarihi).HasColumnType("datetime");
            entity.Property(e => e.mye_dogum_yeri).HasMaxLength(30);
            entity.Property(e => e.mye_email_adres).HasMaxLength(127);
            entity.Property(e => e.mye_es_dogum_tarihi).HasColumnType("datetime");
            entity.Property(e => e.mye_es_isim).HasMaxLength(30);
            entity.Property(e => e.mye_ev_Apt_No).HasMaxLength(10);
            entity.Property(e => e.mye_ev_Daire_No).HasMaxLength(10);
            entity.Property(e => e.mye_ev_Semt).HasMaxLength(25);
            entity.Property(e => e.mye_ev_adres_kodu).HasMaxLength(10);
            entity.Property(e => e.mye_ev_cadde).HasMaxLength(50);
            entity.Property(e => e.mye_ev_il).HasMaxLength(50);
            entity.Property(e => e.mye_ev_ilce).HasMaxLength(50);
            entity.Property(e => e.mye_ev_mahalle).HasMaxLength(50);
            entity.Property(e => e.mye_ev_posta_kodu).HasMaxLength(8);
            entity.Property(e => e.mye_ev_sokak).HasMaxLength(50);
            entity.Property(e => e.mye_ev_telno).HasMaxLength(17);
            entity.Property(e => e.mye_ev_ulke).HasMaxLength(50);
            entity.Property(e => e.mye_evlilik_tarihi).HasColumnType("datetime");
            entity.Property(e => e.mye_is_telno).HasMaxLength(17);
            entity.Property(e => e.mye_isim).HasMaxLength(30);
            entity.Property(e => e.mye_lastup_date).HasColumnType("datetime");
            entity.Property(e => e.mye_sosyal_facebook).HasMaxLength(50);
            entity.Property(e => e.mye_sosyal_google).HasMaxLength(50);
            entity.Property(e => e.mye_sosyal_instagram).HasMaxLength(50);
            entity.Property(e => e.mye_sosyal_linkedin).HasMaxLength(50);
            entity.Property(e => e.mye_sosyal_pasaportno).HasMaxLength(20);
            entity.Property(e => e.mye_sosyal_pinterest).HasMaxLength(50);
            entity.Property(e => e.mye_sosyal_snapchat).HasMaxLength(50);
            entity.Property(e => e.mye_sosyal_twitter).HasMaxLength(50);
            entity.Property(e => e.mye_sosyal_webadresi).HasMaxLength(50);
            entity.Property(e => e.mye_sosyal_youtube).HasMaxLength(50);
            entity.Property(e => e.mye_soyisim).HasMaxLength(30);
            entity.Property(e => e.mye_special1).HasMaxLength(4);
            entity.Property(e => e.mye_special2).HasMaxLength(4);
            entity.Property(e => e.mye_special3).HasMaxLength(4);
            entity.Property(e => e.mye_tc_kimlikno).HasMaxLength(20);
            entity.Property(e => e.mye_vergi_dairesi).HasMaxLength(20);
            entity.Property(e => e.mye_vergi_kimlikno).HasMaxLength(20);
        });

        modelBuilder.Entity<CARI_PERSONEL_TANIMLARI>(entity =>
        {
            entity.HasKey(e => e.cari_per_Guid).HasName("NDX_CARI_PERSONEL_TANIMLARI_00");

            entity.ToTable("CARI_PERSONEL_TANIMLARI");

            entity.HasIndex(e => e.cari_per_kod, "NDX_CARI_PERSONEL_TANIMLARI_02").IsUnique();

            entity.HasIndex(e => new { e.cari_per_adi, e.cari_per_soyadi }, "NDX_CARI_PERSONEL_TANIMLARI_03");

            entity.HasIndex(e => new { e.cari_per_soyadi, e.cari_per_adi }, "NDX_CARI_PERSONEL_TANIMLARI_04");

            entity.Property(e => e.cari_per_Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.cari_per_KEP_adresi).HasMaxLength(80);
            entity.Property(e => e.cari_per_PasaportNo).HasMaxLength(20);
            entity.Property(e => e.cari_per_TcKimlikNo).HasMaxLength(11);
            entity.Property(e => e.cari_per_adi).HasMaxLength(50);
            entity.Property(e => e.cari_per_banka_hesapno).HasMaxLength(40);
            entity.Property(e => e.cari_per_banka_swiftkodu).HasMaxLength(25);
            entity.Property(e => e.cari_per_banka_tcmb_ilkod).HasMaxLength(3);
            entity.Property(e => e.cari_per_banka_tcmb_kod).HasMaxLength(4);
            entity.Property(e => e.cari_per_banka_tcmb_subekod).HasMaxLength(8);
            entity.Property(e => e.cari_per_cepno).HasMaxLength(15);
            entity.Property(e => e.cari_per_create_date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.cari_per_kasiyerAmiri).HasMaxLength(25);
            entity.Property(e => e.cari_per_kasiyerfirmaid).HasMaxLength(15);
            entity.Property(e => e.cari_per_kasiyerkodu).HasMaxLength(25);
            entity.Property(e => e.cari_per_kasiyersifresi).HasMaxLength(127);
            entity.Property(e => e.cari_per_kod).HasMaxLength(25);
            entity.Property(e => e.cari_per_lastup_date).HasColumnType("datetime");
            entity.Property(e => e.cari_per_mail).HasMaxLength(50);
            entity.Property(e => e.cari_per_muhkod0).HasMaxLength(40);
            entity.Property(e => e.cari_per_muhkod1).HasMaxLength(40);
            entity.Property(e => e.cari_per_muhkod2).HasMaxLength(40);
            entity.Property(e => e.cari_per_muhkod3).HasMaxLength(40);
            entity.Property(e => e.cari_per_muhkod4).HasMaxLength(40);
            entity.Property(e => e.cari_per_soyadi).HasMaxLength(50);
            entity.Property(e => e.cari_per_special1).HasMaxLength(4);
            entity.Property(e => e.cari_per_special2).HasMaxLength(4);
            entity.Property(e => e.cari_per_special3).HasMaxLength(4);
            entity.Property(e => e.cari_takvim_kodu).HasMaxLength(4);
        });

        modelBuilder.Entity<EVRAK_ACIKLAMALARI>(entity =>
        {
            entity.HasKey(e => e.egk_Guid).HasName("NDX_EVRAK_ACIKLAMALARI_00");

            entity.ToTable("EVRAK_ACIKLAMALARI");

            entity.HasIndex(e => new { e.egk_dosyano, e.egk_hareket_tip, e.egk_evr_tip, e.egk_evr_seri, e.egk_evr_sira, e.egk_evr_ustkod }, "NDX_EVRAK_ACIKLAMALARI_02").IsUnique();

            entity.Property(e => e.egk_Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.egk_create_date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.egk_evr_seri).HasMaxLength(20);
            entity.Property(e => e.egk_evr_ustkod).HasMaxLength(25);
            entity.Property(e => e.egk_evracik1).HasMaxLength(127);
            entity.Property(e => e.egk_evracik10).HasMaxLength(127);
            entity.Property(e => e.egk_evracik2).HasMaxLength(127);
            entity.Property(e => e.egk_evracik3).HasMaxLength(127);
            entity.Property(e => e.egk_evracik4).HasMaxLength(127);
            entity.Property(e => e.egk_evracik5).HasMaxLength(127);
            entity.Property(e => e.egk_evracik6).HasMaxLength(127);
            entity.Property(e => e.egk_evracik7).HasMaxLength(127);
            entity.Property(e => e.egk_evracik8).HasMaxLength(127);
            entity.Property(e => e.egk_evracik9).HasMaxLength(127);
            entity.Property(e => e.egk_kargokodu).HasMaxLength(25);
            entity.Property(e => e.egk_kargono).HasMaxLength(15);
            entity.Property(e => e.egk_lastup_date).HasColumnType("datetime");
            entity.Property(e => e.egk_special1).HasMaxLength(4);
            entity.Property(e => e.egk_special2).HasMaxLength(4);
            entity.Property(e => e.egk_special3).HasMaxLength(4);
            entity.Property(e => e.egk_tesalkisi).HasMaxLength(50);
            entity.Property(e => e.egk_tesaltarihi).HasColumnType("datetime");
        });

        modelBuilder.Entity<STOK_DEPO_DETAYLARI>(entity =>
        {
            entity.HasKey(e => e.sdp_Guid).HasName("NDX_STOK_DEPO_DETAYLARI_00");

            entity.ToTable("STOK_DEPO_DETAYLARI");

            entity.HasIndex(e => new { e.sdp_depo_kod, e.sdp_depo_no }, "NDX_STOK_DEPO_DETAYLARI_02").IsUnique();

            entity.HasIndex(e => new { e.sdp_sat_cari_kod, e.sdp_depo_kod }, "NDX_STOK_DEPO_DETAYLARI_03");

            entity.Property(e => e.sdp_Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.sdp_UrunSorumlusuKodu).HasMaxLength(25);
            entity.Property(e => e.sdp_create_date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.sdp_depo_kod).HasMaxLength(25);
            entity.Property(e => e.sdp_lastup_date).HasColumnType("datetime");
            entity.Property(e => e.sdp_muhkod_artikeli).HasMaxLength(10);
            entity.Property(e => e.sdp_pozisyonbayrak_kodu).HasMaxLength(25);
            entity.Property(e => e.sdp_sat_cari_kod).HasMaxLength(25);
            entity.Property(e => e.sdp_special1).HasMaxLength(4);
            entity.Property(e => e.sdp_special2).HasMaxLength(4);
            entity.Property(e => e.sdp_special3).HasMaxLength(4);
            entity.Property(e => e.sdp_yerkodu).HasMaxLength(10);
        });

        modelBuilder.Entity<STOK_SATIS_FIYAT_LISTE_TANIMLARI>(entity =>
        {
            entity.HasKey(e => e.sfl_Guid).HasName("NDX_STOK_SATIS_FIYAT_LISTE_TANIMLARI_00");

            entity.ToTable("STOK_SATIS_FIYAT_LISTE_TANIMLARI");

            entity.HasIndex(e => e.sfl_sirano, "NDX_STOK_SATIS_FIYAT_LISTE_TANIMLARI_02").IsUnique();

            entity.HasIndex(e => e.sfl_aciklama, "NDX_STOK_SATIS_FIYAT_LISTE_TANIMLARI_03");

            entity.Property(e => e.sfl_Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.sfl_aciklama).HasMaxLength(50);
            entity.Property(e => e.sfl_create_date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.sfl_fiyatformul).HasMaxLength(50);
            entity.Property(e => e.sfl_ilktarih).HasColumnType("datetime");
            entity.Property(e => e.sfl_lastup_date).HasColumnType("datetime");
            entity.Property(e => e.sfl_odeplformul).HasMaxLength(50);
            entity.Property(e => e.sfl_sabit_iskonto).HasMaxLength(4);
            entity.Property(e => e.sfl_sabit_kampanya).HasMaxLength(4);
            entity.Property(e => e.sfl_sontarih).HasColumnType("datetime");
            entity.Property(e => e.sfl_special1).HasMaxLength(4);
            entity.Property(e => e.sfl_special2).HasMaxLength(4);
            entity.Property(e => e.sfl_special3).HasMaxLength(4);
        });

        modelBuilder.Entity<SummaryEntity>(entity =>
        {
            entity.ToTable("Summaries");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
            entity.Property(item => item.DocumentSerie).HasMaxLength(20);
            entity.Property(item => item.TypeName).HasMaxLength(50);
            entity.Property(item => item.AccountCode).HasMaxLength(40);
            entity.Property(item => item.TerminalId).HasMaxLength(40);
            entity.Property(item => item.Description).HasMaxLength(250);
            entity.Property(item => item.SummaryDate).HasColumnType("datetime");
            entity.Property(item => item.CreateDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<BanknoteMovementEntity>(entity =>
        {
            entity.ToTable("BanknoteMovements");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
            entity.Property(item => item.DocumentSerie).HasMaxLength(20);
            entity.Property(item => item.SummaryDate).HasColumnType("datetime");
            entity.Property(item => item.CreateDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<GiftCheckMovementEntity>(entity =>
        {
            entity.ToTable("GiftCheckMovements");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
            entity.Property(item => item.DocumentSerie).HasMaxLength(20);
            entity.Property(item => item.SummaryDate).HasColumnType("datetime");
            entity.Property(item => item.CreateDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<BanknoteTrackEntity>(entity =>
        {
            entity.ToTable("BanknoteTracks");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.BanknoteTrackDate).HasColumnType("datetime");
            entity.Property(item => item.CreateDate).HasColumnType("datetime");
            entity.Property(item => item.Deliverer).HasMaxLength(100);
            entity.Property(item => item.Receiver).HasMaxLength(100);
        });

        modelBuilder.Entity<PaymentTypeEntity>(entity =>
        {
            entity.ToTable("PaymentTypes");
            entity.HasKey(item => item.PaymentTypeNo);
            entity.Property(item => item.PaymentTypeNo).ValueGeneratedNever();
            entity.Property(item => item.PaymentName).HasMaxLength(100);
        });

        modelBuilder.Entity<BanknoteTypeEntity>(entity =>
        {
            entity.ToTable("BanknoteTypes");
            entity.HasKey(item => item.BanknoteType);
            entity.Property(item => item.BanknoteType).ValueGeneratedNever();
        });

        modelBuilder.Entity<GiftCheckTypeEntity>(entity =>
        {
            entity.ToTable("GiftCheckTypes");
            entity.HasKey(item => item.GiftCheckType);
            entity.Property(item => item.GiftCheckType).ValueGeneratedNever();
        });

        modelBuilder.Entity<CashRegisterDetailEntity>(entity =>
        {
            entity.ToTable("CashRegisterDetails");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
            entity.Property(item => item.CashRegisterNo).HasMaxLength(40);
            entity.Property(item => item.Bank).HasMaxLength(100);
            entity.Property(item => item.TerminalId).HasMaxLength(40);
            entity.Property(item => item.MerchantNo).HasMaxLength(40);
        });
    }
}
