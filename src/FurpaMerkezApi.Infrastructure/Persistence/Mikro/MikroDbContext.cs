using System;
using System.Collections.Generic;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Persistence.Mikro;

public partial class MikroDbContext : DbContext
{
    public MikroDbContext(DbContextOptions<MikroDbContext> options)
        : base(options)
    {
    }

    protected MikroDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public virtual DbSet<BARKOD_TANIMLARI> BARKOD_TANIMLARIs { get; set; }

    public virtual DbSet<CARI_HESAPLAR> CARI_HESAPLARs { get; set; }

    public virtual DbSet<CARI_HESAP_ADRESLERI> CARI_HESAP_ADRESLERIs { get; set; }

    public virtual DbSet<CARI_HESAP_HAREKETLERI> CARI_HESAP_HAREKETLERIs { get; set; }

    public virtual DbSet<DEPOLAR> DEPOLARs { get; set; }

    public virtual DbSet<DEPOLAR_ARASI_SIPARISLER> DEPOLAR_ARASI_SIPARISLERs { get; set; }

    public virtual DbSet<SIPARISLER> SIPARISLERs { get; set; }

    public virtual DbSet<STOKLAR> STOKLARs { get; set; }

    public virtual DbSet<STOK_FIYAT_DEGISIKLIKLERI> STOK_FIYAT_DEGISIKLIKLERIs { get; set; }

    public virtual DbSet<STOK_HAREKETLERI> STOK_HAREKETLERIs { get; set; }

    public virtual DbSet<STOK_HAREKETLERI_EK> STOK_HAREKETLERI_EKs { get; set; }

    public virtual DbSet<STOK_HAREKETLERI_OZET> STOK_HAREKETLERI_OZETs { get; set; }

    public virtual DbSet<SAYIM_SONUCLARI> SAYIM_SONUCLARIs { get; set; }

    public virtual DbSet<STOK_SATIS_FIYAT_LISTELERI> STOK_SATIS_FIYAT_LISTELERIs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BARKOD_TANIMLARI>(entity =>
        {
            entity.HasKey(e => e.bar_Guid).HasName("NDX_BARKOD_TANIMLARI_00");

            entity.ToTable("BARKOD_TANIMLARI", tb => tb.HasTrigger("TRG_BARKOD_SILINEN_LOG"));

            entity.HasIndex(e => e.bar_kodu, "NDX_BARKOD_TANIMLARI_02").IsUnique();

            entity.HasIndex(e => e.bar_stokkodu, "NDX_BARKOD_TANIMLARI_03");

            entity.Property(e => e.bar_Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.bar_asortitanimkodu).HasMaxLength(25);
            entity.Property(e => e.bar_create_date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.bar_kodu).HasMaxLength(50);
            entity.Property(e => e.bar_lastup_date).HasColumnType("datetime");
            entity.Property(e => e.bar_partikodu).HasMaxLength(25);
            entity.Property(e => e.bar_serino_veya_bagkodu).HasMaxLength(50);
            entity.Property(e => e.bar_special1).HasMaxLength(4);
            entity.Property(e => e.bar_special2).HasMaxLength(4);
            entity.Property(e => e.bar_special3).HasMaxLength(4);
            entity.Property(e => e.bar_stokkodu).HasMaxLength(25);
        });

        modelBuilder.Entity<CARI_HESAPLAR>(entity =>
        {
            entity.HasKey(e => e.cari_Guid)
                .HasName("NDX_CARI_HESAPLAR_00")
                .HasFillFactor(30);

            entity.ToTable("CARI_HESAPLAR");

            entity.HasIndex(e => e.cari_kod, "NDX_CARI_HESAPLAR_02")
                .IsUnique()
                .HasFillFactor(30);

            entity.HasIndex(e => e.cari_unvan1, "NDX_CARI_HESAPLAR_03").HasFillFactor(30);

            entity.HasIndex(e => new { e.cari_sektor_kodu, e.cari_kod }, "NDX_CARI_HESAPLAR_04")
                .IsUnique()
                .HasFillFactor(30);

            entity.HasIndex(e => new { e.cari_grup_kodu, e.cari_kod }, "NDX_CARI_HESAPLAR_05")
                .IsUnique()
                .HasFillFactor(30);

            entity.HasIndex(e => new { e.cari_temsilci_kodu, e.cari_kod }, "NDX_CARI_HESAPLAR_06")
                .IsUnique()
                .HasFillFactor(30);

            entity.HasIndex(e => new { e.cari_bolge_kodu, e.cari_kod }, "NDX_CARI_HESAPLAR_07")
                .IsUnique()
                .HasFillFactor(30);

            entity.HasIndex(e => new { e.cari_kaydagiristarihi, e.cari_kod }, "NDX_CARI_HESAPLAR_08")
                .IsUnique()
                .HasFillFactor(30);

            entity.HasIndex(e => e.cari_Ana_cari_kodu, "NDX_CARI_HESAPLAR_09").HasFillFactor(30);

            entity.HasIndex(e => e.cari_vdaire_no, "NDX_CARI_HESAPLAR_10").HasFillFactor(30);

            entity.HasIndex(e => e.cari_VergiKimlikNo, "NDX_CARI_HESAPLAR_11").HasFillFactor(30);

            entity.HasIndex(e => new { e.cari_unvan2, e.cari_unvan1 }, "NDX_CARI_HESAPLAR_12").HasFillFactor(30);

            entity.Property(e => e.cari_Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.cari_AlinanDepozitoTeminatMuhKodu).HasMaxLength(40);
            entity.Property(e => e.cari_AlinanDepozitoTeminatMuhKodu1).HasMaxLength(40);
            entity.Property(e => e.cari_AlinanDepozitoTeminatMuhKodu2).HasMaxLength(40);
            entity.Property(e => e.cari_Ana_cari_kodu).HasMaxLength(25);
            entity.Property(e => e.cari_CepTel).HasMaxLength(20);
            entity.Property(e => e.cari_EMail).HasMaxLength(127);
            entity.Property(e => e.cari_KEP_adresi).HasMaxLength(80);
            entity.Property(e => e.cari_Portal_PW).HasMaxLength(127);
            entity.Property(e => e.cari_TeminatMekAlacakMuhKodu).HasMaxLength(40);
            entity.Property(e => e.cari_TeminatMekAlacakMuhKodu1).HasMaxLength(40);
            entity.Property(e => e.cari_TeminatMekAlacakMuhKodu2).HasMaxLength(40);
            entity.Property(e => e.cari_TeminatMekBorcMuhKodu).HasMaxLength(40);
            entity.Property(e => e.cari_TeminatMekBorcMuhKodu1).HasMaxLength(40);
            entity.Property(e => e.cari_TeminatMekBorcMuhKodu2).HasMaxLength(40);
            entity.Property(e => e.cari_VergiKimlikNo).HasMaxLength(10);
            entity.Property(e => e.cari_VerilenDepozitoTeminatMuhKodu).HasMaxLength(40);
            entity.Property(e => e.cari_VerilenDepozitoTeminatMuhKodu1).HasMaxLength(40);
            entity.Property(e => e.cari_VerilenDepozitoTeminatMuhKodu2).HasMaxLength(40);
            entity.Property(e => e.cari_baba_adi).HasMaxLength(50);
            entity.Property(e => e.cari_banka_hesapno1).HasMaxLength(40);
            entity.Property(e => e.cari_banka_hesapno10).HasMaxLength(40);
            entity.Property(e => e.cari_banka_hesapno2).HasMaxLength(40);
            entity.Property(e => e.cari_banka_hesapno3).HasMaxLength(40);
            entity.Property(e => e.cari_banka_hesapno4).HasMaxLength(40);
            entity.Property(e => e.cari_banka_hesapno5).HasMaxLength(40);
            entity.Property(e => e.cari_banka_hesapno6).HasMaxLength(40);
            entity.Property(e => e.cari_banka_hesapno7).HasMaxLength(40);
            entity.Property(e => e.cari_banka_hesapno8).HasMaxLength(40);
            entity.Property(e => e.cari_banka_hesapno9).HasMaxLength(40);
            entity.Property(e => e.cari_banka_swiftkodu1).HasMaxLength(25);
            entity.Property(e => e.cari_banka_swiftkodu10).HasMaxLength(25);
            entity.Property(e => e.cari_banka_swiftkodu2).HasMaxLength(25);
            entity.Property(e => e.cari_banka_swiftkodu3).HasMaxLength(25);
            entity.Property(e => e.cari_banka_swiftkodu4).HasMaxLength(25);
            entity.Property(e => e.cari_banka_swiftkodu5).HasMaxLength(25);
            entity.Property(e => e.cari_banka_swiftkodu6).HasMaxLength(25);
            entity.Property(e => e.cari_banka_swiftkodu7).HasMaxLength(25);
            entity.Property(e => e.cari_banka_swiftkodu8).HasMaxLength(25);
            entity.Property(e => e.cari_banka_swiftkodu9).HasMaxLength(25);
            entity.Property(e => e.cari_banka_tcmb_ilkod1).HasMaxLength(3);
            entity.Property(e => e.cari_banka_tcmb_ilkod10).HasMaxLength(3);
            entity.Property(e => e.cari_banka_tcmb_ilkod2).HasMaxLength(3);
            entity.Property(e => e.cari_banka_tcmb_ilkod3).HasMaxLength(3);
            entity.Property(e => e.cari_banka_tcmb_ilkod4).HasMaxLength(3);
            entity.Property(e => e.cari_banka_tcmb_ilkod5).HasMaxLength(3);
            entity.Property(e => e.cari_banka_tcmb_ilkod6).HasMaxLength(3);
            entity.Property(e => e.cari_banka_tcmb_ilkod7).HasMaxLength(3);
            entity.Property(e => e.cari_banka_tcmb_ilkod8).HasMaxLength(3);
            entity.Property(e => e.cari_banka_tcmb_ilkod9).HasMaxLength(3);
            entity.Property(e => e.cari_banka_tcmb_kod1).HasMaxLength(4);
            entity.Property(e => e.cari_banka_tcmb_kod10).HasMaxLength(4);
            entity.Property(e => e.cari_banka_tcmb_kod2).HasMaxLength(4);
            entity.Property(e => e.cari_banka_tcmb_kod3).HasMaxLength(4);
            entity.Property(e => e.cari_banka_tcmb_kod4).HasMaxLength(4);
            entity.Property(e => e.cari_banka_tcmb_kod5).HasMaxLength(4);
            entity.Property(e => e.cari_banka_tcmb_kod6).HasMaxLength(4);
            entity.Property(e => e.cari_banka_tcmb_kod7).HasMaxLength(4);
            entity.Property(e => e.cari_banka_tcmb_kod8).HasMaxLength(4);
            entity.Property(e => e.cari_banka_tcmb_kod9).HasMaxLength(4);
            entity.Property(e => e.cari_banka_tcmb_subekod1).HasMaxLength(8);
            entity.Property(e => e.cari_banka_tcmb_subekod10).HasMaxLength(8);
            entity.Property(e => e.cari_banka_tcmb_subekod2).HasMaxLength(8);
            entity.Property(e => e.cari_banka_tcmb_subekod3).HasMaxLength(8);
            entity.Property(e => e.cari_banka_tcmb_subekod4).HasMaxLength(8);
            entity.Property(e => e.cari_banka_tcmb_subekod5).HasMaxLength(8);
            entity.Property(e => e.cari_banka_tcmb_subekod6).HasMaxLength(8);
            entity.Property(e => e.cari_banka_tcmb_subekod7).HasMaxLength(8);
            entity.Property(e => e.cari_banka_tcmb_subekod8).HasMaxLength(8);
            entity.Property(e => e.cari_banka_tcmb_subekod9).HasMaxLength(8);
            entity.Property(e => e.cari_bolge_kodu).HasMaxLength(25);
            entity.Property(e => e.cari_create_date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.cari_earsiv_xslt_dosya).HasMaxLength(127);
            entity.Property(e => e.cari_efatura_baslangic_tarihi).HasColumnType("datetime");
            entity.Property(e => e.cari_efatura_xslt_dosya).HasMaxLength(127);
            entity.Property(e => e.cari_eirsaliye_baslangic_tarihi).HasColumnType("datetime");
            entity.Property(e => e.cari_eirsaliye_xslt_dosya).HasMaxLength(127);
            entity.Property(e => e.cari_grup_kodu).HasMaxLength(25);
            entity.Property(e => e.cari_istasyon_cari_kodu).HasMaxLength(25);
            entity.Property(e => e.cari_kampanyakodu).HasMaxLength(4);
            entity.Property(e => e.cari_kaydagiristarihi).HasColumnType("datetime");
            entity.Property(e => e.cari_kisi_kimlik_bilgisi_diger_aciklama).HasMaxLength(50);
            entity.Property(e => e.cari_kod).HasMaxLength(25);
            entity.Property(e => e.cari_lastup_date).HasColumnType("datetime");
            entity.Property(e => e.cari_mersis_no).HasMaxLength(25);
            entity.Property(e => e.cari_muh_kod).HasMaxLength(40);
            entity.Property(e => e.cari_muh_kod1).HasMaxLength(40);
            entity.Property(e => e.cari_muh_kod2).HasMaxLength(40);
            entity.Property(e => e.cari_muhartikeli).HasMaxLength(10);
            entity.Property(e => e.cari_mutabakat_mail_adresi).HasMaxLength(80);
            entity.Property(e => e.cari_nacekodu_1).HasMaxLength(25);
            entity.Property(e => e.cari_nacekodu_2).HasMaxLength(25);
            entity.Property(e => e.cari_nacekodu_3).HasMaxLength(25);
            entity.Property(e => e.cari_ozel_butceli_kurum_carisi).HasMaxLength(25);
            entity.Property(e => e.cari_pasaport_no).HasMaxLength(20);
            entity.Property(e => e.cari_satis_isk_kod).HasMaxLength(4);
            entity.Property(e => e.cari_sektor_kodu).HasMaxLength(25);
            entity.Property(e => e.cari_sicil_no).HasMaxLength(15);
            entity.Property(e => e.cari_special1).HasMaxLength(4);
            entity.Property(e => e.cari_special2).HasMaxLength(4);
            entity.Property(e => e.cari_special3).HasMaxLength(4);
            entity.Property(e => e.cari_tasiyicifirma_cari_kodu).HasMaxLength(25);
            entity.Property(e => e.cari_temsilci_kodu).HasMaxLength(25);
            entity.Property(e => e.cari_ufrs_fark_muh_kod).HasMaxLength(40);
            entity.Property(e => e.cari_ufrs_fark_muh_kod1).HasMaxLength(40);
            entity.Property(e => e.cari_ufrs_fark_muh_kod2).HasMaxLength(40);
            entity.Property(e => e.cari_unvan1).HasMaxLength(127);
            entity.Property(e => e.cari_unvan2).HasMaxLength(127);
            entity.Property(e => e.cari_uts_kurum_no).HasMaxLength(15);
            entity.Property(e => e.cari_vdaire_adi).HasMaxLength(50);
            entity.Property(e => e.cari_vdaire_no).HasMaxLength(15);
            entity.Property(e => e.cari_vergidairekodu).HasMaxLength(10);
            entity.Property(e => e.cari_wwwadresi).HasMaxLength(30);
        });

        modelBuilder.Entity<CARI_HESAP_ADRESLERI>(entity =>
        {
            entity.HasKey(e => e.adr_Guid).HasName("NDX_CARI_HESAP_ADRESLERI_00");

            entity.ToTable("CARI_HESAP_ADRESLERI");

            entity.HasIndex(e => new { e.adr_cari_kod, e.adr_adres_no }, "NDX_CARI_HESAP_ADRESLERI_02").IsUnique();

            entity.Property(e => e.adr_Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.adr_Adres_kodu).HasMaxLength(10);
            entity.Property(e => e.adr_Apt_No).HasMaxLength(10);
            entity.Property(e => e.adr_Daire_No).HasMaxLength(10);
            entity.Property(e => e.adr_Semt).HasMaxLength(25);
            entity.Property(e => e.adr_cadde).HasMaxLength(50);
            entity.Property(e => e.adr_cari_kod).HasMaxLength(25);
            entity.Property(e => e.adr_create_date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.adr_efatura_alias).HasMaxLength(120);
            entity.Property(e => e.adr_eirsaliye_alias).HasMaxLength(120);
            entity.Property(e => e.adr_il).HasMaxLength(50);
            entity.Property(e => e.adr_ilce).HasMaxLength(50);
            entity.Property(e => e.adr_lastup_date).HasColumnType("datetime");
            entity.Property(e => e.adr_mahalle).HasMaxLength(50);
            entity.Property(e => e.adr_ozel_not).HasMaxLength(127);
            entity.Property(e => e.adr_posta_kodu).HasMaxLength(8);
            entity.Property(e => e.adr_sokak).HasMaxLength(50);
            entity.Property(e => e.adr_special1).HasMaxLength(4);
            entity.Property(e => e.adr_special2).HasMaxLength(4);
            entity.Property(e => e.adr_special3).HasMaxLength(4);
            entity.Property(e => e.adr_tel_bolge_kodu).HasMaxLength(5);
            entity.Property(e => e.adr_tel_faxno).HasMaxLength(10);
            entity.Property(e => e.adr_tel_modem).HasMaxLength(10);
            entity.Property(e => e.adr_tel_no1).HasMaxLength(10);
            entity.Property(e => e.adr_tel_no2).HasMaxLength(10);
            entity.Property(e => e.adr_tel_ulke_kodu).HasMaxLength(5);
            entity.Property(e => e.adr_temsilci_kodu).HasMaxLength(25);
            entity.Property(e => e.adr_ulke).HasMaxLength(50);
            entity.Property(e => e.adr_yon_kodu).HasMaxLength(4);
        });

        modelBuilder.Entity<CARI_HESAP_HAREKETLERI>(entity =>
        {
            entity.HasKey(e => e.cha_Guid)
                .HasName("NDX_CARI_HESAP_HAREKETLERI_00")
                .HasFillFactor(98);

            entity.ToTable("CARI_HESAP_HAREKETLERI", tb =>
                {
                    tb.HasTrigger("CARI_HESAP_HAREKETLERI_Belge_No_Ekle");
                    tb.HasTrigger("Trg_EBelge_Tablosuna_Belge_Numarası_Ekle");
                    tb.HasTrigger("mye_CARI_HESAP_HAREKETLERI_Trigger");
                });

            entity.HasIndex(e => e.cha_Guid, "IX_CARI_HESAP_HAREKETLERI_cha_Guid").HasFillFactor(97);

            entity.HasIndex(e => e.cha_tarihi, "NDX_CARI_HESAP_HAREKETLERI_02").HasFillFactor(98);

            entity.HasIndex(e => new { e.cha_cari_cins, e.cha_kod, e.cha_tarihi }, "NDX_CARI_HESAP_HAREKETLERI_03").HasFillFactor(98);

            entity.HasIndex(e => new { e.cha_evrak_tip, e.cha_evrakno_seri, e.cha_evrakno_sira, e.cha_satir_no }, "NDX_CARI_HESAP_HAREKETLERI_04")
                .IsUnique()
                .HasFillFactor(98);

            entity.HasIndex(e => new { e.cha_kasa_hizmet, e.cha_kasa_hizkod, e.cha_tarihi }, "NDX_CARI_HESAP_HAREKETLERI_05").HasFillFactor(98);

            entity.HasIndex(e => new { e.cha_vardiya_evrak_ti, e.cha_vardiya_tarihi, e.cha_vardiya_no, e.cha_satici_kodu }, "NDX_CARI_HESAP_HAREKETLERI_06").HasFillFactor(98);

            entity.HasIndex(e => new { e.cha_evrak_tip, e.cha_cari_cins, e.cha_kod, e.cha_belge_no }, "NDX_CARI_HESAP_HAREKETLERI_07").HasFillFactor(98);

            entity.HasIndex(e => e.cha_EXIMkodu, "NDX_CARI_HESAP_HAREKETLERI_08").HasFillFactor(98);

            entity.HasIndex(e => e.cha_ciro_cari_kodu, "NDX_CARI_HESAP_HAREKETLERI_09").HasFillFactor(98);

            entity.HasIndex(e => e.cha_trefno, "NDX_CARI_HESAP_HAREKETLERI_10").HasFillFactor(98);

            entity.HasIndex(e => e.cha_sip_uid, "NDX_CARI_HESAP_HAREKETLERI_11").HasFillFactor(98);

            entity.Property(e => e.cha_Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.cha_EXIMkodu).HasMaxLength(25);
            entity.Property(e => e.cha_HareketGrupKodu1).HasMaxLength(25);
            entity.Property(e => e.cha_HareketGrupKodu2).HasMaxLength(25);
            entity.Property(e => e.cha_HareketGrupKodu3).HasMaxLength(25);
            entity.Property(e => e.cha_aciklama).HasMaxLength(40);
            entity.Property(e => e.cha_belge_no).HasMaxLength(50);
            entity.Property(e => e.cha_belge_tarih).HasColumnType("datetime");
            entity.Property(e => e.cha_ciro_cari_kodu).HasMaxLength(25);
            entity.Property(e => e.cha_create_date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.cha_diger_belge_adi).HasMaxLength(50);
            entity.Property(e => e.cha_disyazilimid).HasMaxLength(50);
            entity.Property(e => e.cha_ebelgeno_seri).HasMaxLength(20);
            entity.Property(e => e.cha_eticaret_kanal_kodu).HasMaxLength(25);
            entity.Property(e => e.cha_evrakno_seri).HasMaxLength(20);
            entity.Property(e => e.cha_fis_tarih).HasColumnType("datetime");
            entity.Property(e => e.cha_hubglbid).HasMaxLength(50);
            entity.Property(e => e.cha_hubid).HasMaxLength(50);
            entity.Property(e => e.cha_ilk_belge_tarihi).HasColumnType("datetime");
            entity.Property(e => e.cha_karsisrmrkkodu).HasMaxLength(25);
            entity.Property(e => e.cha_kasa_hizkod).HasMaxLength(25);
            entity.Property(e => e.cha_kod).HasMaxLength(25);
            entity.Property(e => e.cha_lastup_date).HasColumnType("datetime");
            entity.Property(e => e.cha_projekodu).HasMaxLength(25);
            entity.Property(e => e.cha_reftarihi).HasColumnType("datetime");
            entity.Property(e => e.cha_satici_kodu).HasMaxLength(25);
            entity.Property(e => e.cha_special1).HasMaxLength(4);
            entity.Property(e => e.cha_special2).HasMaxLength(4);
            entity.Property(e => e.cha_special3).HasMaxLength(4);
            entity.Property(e => e.cha_srmrkkodu).HasMaxLength(25);
            entity.Property(e => e.cha_tarihi).HasColumnType("datetime");
            entity.Property(e => e.cha_trefno).HasMaxLength(25);
            entity.Property(e => e.cha_uuid).HasMaxLength(40);
            entity.Property(e => e.cha_vardiya_tarihi).HasColumnType("datetime");
            entity.Property(e => e.cha_yat_tes_kodu).HasMaxLength(25);
        });

        modelBuilder.Entity<DEPOLAR>(entity =>
        {
            entity.HasKey(e => e.dep_Guid).HasName("NDX_DEPOLAR_00");

            entity.ToTable("DEPOLAR");

            entity.HasIndex(e => e.dep_no, "NDX_DEPOLAR_02").IsUnique();

            entity.HasIndex(e => e.dep_adi, "NDX_DEPOLAR_03");

            entity.HasIndex(e => new { e.dep_firmano, e.dep_subeno }, "NDX_DEPOLAR_04");

            entity.HasIndex(e => e.dep_sor_mer_kodu, "NDX_DEPOLAR_05");

            entity.HasIndex(e => e.dep_grup_kodu, "NDX_DEPOLAR_06");

            entity.Property(e => e.dep_Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.dep_Adres_kodu).HasMaxLength(10);
            entity.Property(e => e.dep_Apt_No).HasMaxLength(10);
            entity.Property(e => e.dep_Daire_No).HasMaxLength(10);
            entity.Property(e => e.dep_Il).HasMaxLength(50);
            entity.Property(e => e.dep_Ilce).HasMaxLength(50);
            entity.Property(e => e.dep_KilitTarihi).HasColumnType("datetime");
            entity.Property(e => e.dep_Semt).HasMaxLength(25);
            entity.Property(e => e.dep_Ulke).HasMaxLength(50);
            entity.Property(e => e.dep_adi).HasMaxLength(50);
            entity.Property(e => e.dep_barkod_yazici_yolu).HasMaxLength(50);
            entity.Property(e => e.dep_bolge_kodu).HasMaxLength(25);
            entity.Property(e => e.dep_cadde).HasMaxLength(50);
            entity.Property(e => e.dep_create_date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.dep_dizin_adi).HasMaxLength(50);
            entity.Property(e => e.dep_fason_sor_mer_kodu).HasMaxLength(25);
            entity.Property(e => e.dep_grup_kodu).HasMaxLength(25);
            entity.Property(e => e.dep_lastup_date).HasColumnType("datetime");
            entity.Property(e => e.dep_mahalle).HasMaxLength(50);
            entity.Property(e => e.dep_muh_kodu).HasMaxLength(10);
            entity.Property(e => e.dep_posta_Kodu).HasMaxLength(8);
            entity.Property(e => e.dep_proje_kodu).HasMaxLength(25);
            entity.Property(e => e.dep_sokak).HasMaxLength(50);
            entity.Property(e => e.dep_sor_mer_kodu).HasMaxLength(25);
            entity.Property(e => e.dep_special1).HasMaxLength(4);
            entity.Property(e => e.dep_special2).HasMaxLength(4);
            entity.Property(e => e.dep_special3).HasMaxLength(4);
            entity.Property(e => e.dep_tel_bolge_kodu).HasMaxLength(5);
            entity.Property(e => e.dep_tel_faxno).HasMaxLength(10);
            entity.Property(e => e.dep_tel_modem).HasMaxLength(10);
            entity.Property(e => e.dep_tel_no1).HasMaxLength(10);
            entity.Property(e => e.dep_tel_no2).HasMaxLength(10);
            entity.Property(e => e.dep_tel_ulke_kodu).HasMaxLength(5);
            entity.Property(e => e.dep_yetkili_email).HasMaxLength(50);
        });

        modelBuilder.Entity<DEPOLAR_ARASI_SIPARISLER>(entity =>
        {
            entity.HasKey(e => e.ssip_Guid).HasName("NDX_DEPOLAR_ARASI_SIPARISLER_00");

            entity.ToTable("DEPOLAR_ARASI_SIPARISLER", tb =>
                {
                    tb.HasTrigger("TRG_DEPOLAR_ARASI_SIPARIS_SILINEN_LOG");
                    tb.HasTrigger("mye_DEPOLAR_ARASI_SIPARISLER_Trigger");
                });

            entity.HasIndex(e => e.ssip_teslim_tarih, "NDX_DEPOLAR_ARASI_SIPARISLER_02");

            entity.HasIndex(e => e.ssip_tarih, "NDX_DEPOLAR_ARASI_SIPARISLER_03");

            entity.HasIndex(e => new { e.ssip_stok_kod, e.ssip_teslim_tarih }, "NDX_DEPOLAR_ARASI_SIPARISLER_04");

            entity.HasIndex(e => new { e.ssip_evrakno_seri, e.ssip_evrakno_sira, e.ssip_satirno }, "NDX_DEPOLAR_ARASI_SIPARISLER_05").IsUnique();

            entity.Property(e => e.ssip_Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.ssip_aciklama).HasMaxLength(50);
            entity.Property(e => e.ssip_belge_tarih).HasColumnType("datetime");
            entity.Property(e => e.ssip_belgeno).HasMaxLength(50);
            entity.Property(e => e.ssip_create_date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ssip_evrakno_seri).HasMaxLength(20);
            entity.Property(e => e.ssip_gecerlilik_tarihi).HasColumnType("datetime");
            entity.Property(e => e.ssip_kapatmanedenkod).HasMaxLength(25);
            entity.Property(e => e.ssip_lastup_date).HasColumnType("datetime");
            entity.Property(e => e.ssip_paket_kod).HasMaxLength(25);
            entity.Property(e => e.ssip_projekodu).HasMaxLength(25);
            entity.Property(e => e.ssip_sormerkezi).HasMaxLength(25);
            entity.Property(e => e.ssip_special1).HasMaxLength(4);
            entity.Property(e => e.ssip_special2).HasMaxLength(4);
            entity.Property(e => e.ssip_special3).HasMaxLength(4);
            entity.Property(e => e.ssip_stok_kod).HasMaxLength(25);
            entity.Property(e => e.ssip_tarih).HasColumnType("datetime");
            entity.Property(e => e.ssip_teslim_tarih).HasColumnType("datetime");
        });

        modelBuilder.Entity<SIPARISLER>(entity =>
        {
            entity.HasKey(e => e.sip_Guid)
                .HasName("NDX_SIPARISLER_00")
                .HasFillFactor(98);

            entity.ToTable("SIPARISLER", tb => tb.HasTrigger("mye_SIPARISLER_Trigger"));

            entity.HasIndex(e => e.sip_teslim_tarih, "NDX_SIPARISLER_02").HasFillFactor(98);

            entity.HasIndex(e => e.sip_tarih, "NDX_SIPARISLER_03").HasFillFactor(98);

            entity.HasIndex(e => new { e.sip_tip, e.sip_stok_kod, e.sip_teslim_tarih }, "NDX_SIPARISLER_04").HasFillFactor(98);

            entity.HasIndex(e => new { e.sip_tip, e.sip_musteri_kod, e.sip_teslim_tarih }, "NDX_SIPARISLER_05").HasFillFactor(98);

            entity.HasIndex(e => new { e.sip_tip, e.sip_cins, e.sip_evrakno_seri, e.sip_evrakno_sira, e.sip_satirno }, "NDX_SIPARISLER_06")
                .IsUnique()
                .HasFillFactor(98);

            entity.HasIndex(e => new { e.sip_tip, e.sip_stok_kod, e.sip_tarih }, "NDX_SIPARISLER_07").HasFillFactor(98);

            entity.HasIndex(e => new { e.sip_tip, e.sip_Exp_Imp_Kodu }, "NDX_SIPARISLER_08").HasFillFactor(98);

            entity.Property(e => e.sip_Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.sip_Exp_Imp_Kodu).HasMaxLength(25);
            entity.Property(e => e.sip_HareketGrupKodu1).HasMaxLength(25);
            entity.Property(e => e.sip_HareketGrupKodu2).HasMaxLength(25);
            entity.Property(e => e.sip_HareketGrupKodu3).HasMaxLength(25);
            entity.Property(e => e.sip_aciklama).HasMaxLength(50);
            entity.Property(e => e.sip_aciklama2).HasMaxLength(50);
            entity.Property(e => e.sip_belge_tarih).HasColumnType("datetime");
            entity.Property(e => e.sip_belgeno).HasMaxLength(50);
            entity.Property(e => e.sip_cari_sormerk).HasMaxLength(25);
            entity.Property(e => e.sip_create_date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.sip_eticaret_kanal_kodu).HasMaxLength(25);
            entity.Property(e => e.sip_evrakno_seri).HasMaxLength(20);
            entity.Property(e => e.sip_gecerlilik_tarihi).HasColumnType("datetime");
            entity.Property(e => e.sip_kapatmanedenkod).HasMaxLength(25);
            entity.Property(e => e.sip_lastup_date).HasColumnType("datetime");
            entity.Property(e => e.sip_musteri_kod).HasMaxLength(25);
            entity.Property(e => e.sip_onodeme_evrak_seri).HasMaxLength(20);
            entity.Property(e => e.sip_paket_kod).HasMaxLength(25);
            entity.Property(e => e.sip_parti_kodu).HasMaxLength(25);
            entity.Property(e => e.sip_projekodu).HasMaxLength(25);
            entity.Property(e => e.sip_satici_kod).HasMaxLength(25);
            entity.Property(e => e.sip_special1).HasMaxLength(4);
            entity.Property(e => e.sip_special2).HasMaxLength(4);
            entity.Property(e => e.sip_special3).HasMaxLength(4);
            entity.Property(e => e.sip_stok_kod).HasMaxLength(25);
            entity.Property(e => e.sip_stok_sormerk).HasMaxLength(25);
            entity.Property(e => e.sip_tarih).HasColumnType("datetime");
            entity.Property(e => e.sip_teslim_tarih).HasColumnType("datetime");
            entity.Property(e => e.sip_teslimturu).HasMaxLength(4);
        });

        modelBuilder.Entity<STOKLAR>(entity =>
        {
            entity.HasKey(e => e.sto_kod)
                .HasName("NDX_STOKLAR_02")
                .HasFillFactor(30);

            entity.ToTable("STOKLAR", tb => tb.HasTrigger("TRG_STOK_SILINEN_LOG"));

            entity.HasIndex(e => e.sto_Guid, "NDX_STOKLAR_00")
                .IsUnique()
                .HasFillFactor(30);

            entity.HasIndex(e => e.sto_isim, "NDX_STOKLAR_03").HasFillFactor(30);

            entity.HasIndex(e => new { e.sto_anagrup_kod, e.sto_altgrup_kod, e.sto_sat_cari_kod, e.sto_kod }, "NDX_STOKLAR_04").HasFillFactor(30);

            entity.HasIndex(e => new { e.sto_urun_sorkod, e.sto_kod }, "NDX_STOKLAR_05").HasFillFactor(30);

            entity.HasIndex(e => new { e.sto_uretici_kodu, e.sto_kod }, "NDX_STOKLAR_06").HasFillFactor(30);

            entity.HasIndex(e => new { e.sto_reyon_kodu, e.sto_kod }, "NDX_STOKLAR_07").HasFillFactor(30);

            entity.HasIndex(e => new { e.sto_sektor_kodu, e.sto_kod }, "NDX_STOKLAR_08").HasFillFactor(30);

            entity.HasIndex(e => new { e.sto_marka_kodu, e.sto_kod }, "NDX_STOKLAR_09").HasFillFactor(30);

            entity.HasIndex(e => new { e.sto_muhgrup_kodu, e.sto_kod }, "NDX_STOKLAR_10").HasFillFactor(30);

            entity.HasIndex(e => new { e.sto_ambalaj_kodu, e.sto_kod }, "NDX_STOKLAR_11").HasFillFactor(30);

            entity.HasIndex(e => new { e.sto_kalkon_kodu, e.sto_kod }, "NDX_STOKLAR_12").HasFillFactor(30);

            entity.HasIndex(e => new { e.sto_sat_cari_kod, e.sto_kod }, "NDX_STOKLAR_13").HasFillFactor(30);

            entity.HasIndex(e => new { e.sto_renk_kodu, e.sto_kod }, "NDX_STOKLAR_14").HasFillFactor(30);

            entity.HasIndex(e => new { e.sto_model_kodu, e.sto_kod }, "NDX_STOKLAR_15").HasFillFactor(30);

            entity.HasIndex(e => new { e.sto_sezon_kodu, e.sto_kod }, "NDX_STOKLAR_16").HasFillFactor(30);

            entity.HasIndex(e => new { e.sto_hammadde_kodu, e.sto_kod }, "NDX_STOKLAR_17").HasFillFactor(30);

            entity.HasIndex(e => new { e.sto_beden_kodu, e.sto_kod }, "NDX_STOKLAR_18").HasFillFactor(30);

            entity.HasIndex(e => new { e.sto_kategori_kodu, e.sto_kod }, "NDX_STOKLAR_19").HasFillFactor(30);

            entity.HasIndex(e => new { e.sto_prim_kodu, e.sto_kod }, "NDX_STOKLAR_20").HasFillFactor(30);

            entity.HasIndex(e => e.sto_plu_no, "NDX_STOKLAR_21").HasFillFactor(30);

            entity.Property(e => e.sto_kod).HasMaxLength(25);
            entity.Property(e => e.sto_GEKAP).HasMaxLength(5);
            entity.Property(e => e.sto_GEKAP_depozitoonaykodu).HasMaxLength(10);
            entity.Property(e => e.sto_GtipNo).HasMaxLength(25);
            entity.Property(e => e.sto_Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.sto_alisk_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_altgrup_kod).HasMaxLength(25);
            entity.Property(e => e.sto_ambalaj_kodu).HasMaxLength(25);
            entity.Property(e => e.sto_anagrup_kod).HasMaxLength(25);
            entity.Property(e => e.sto_bagortsatIadmuhkod).HasMaxLength(40);
            entity.Property(e => e.sto_bagortsatIskmuhkod).HasMaxLength(40);
            entity.Property(e => e.sto_bagortsat_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_bagortsatiade_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_bagortsatisk_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_bagortsatmal_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_bagortsatmalmuhkod).HasMaxLength(40);
            entity.Property(e => e.sto_bagortsatmuhkod).HasMaxLength(40);
            entity.Property(e => e.sto_beden_kodu).HasMaxLength(25);
            entity.Property(e => e.sto_birim1_ad).HasMaxLength(10);
            entity.Property(e => e.sto_birim2_ad).HasMaxLength(10);
            entity.Property(e => e.sto_birim3_ad).HasMaxLength(10);
            entity.Property(e => e.sto_birim4_ad).HasMaxLength(10);
            entity.Property(e => e.sto_create_date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.sto_degerdusuklugu_ufrs_kod).HasMaxLength(40);
            entity.Property(e => e.sto_depsat_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_depsatmal_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_depsatmalmuhkod).HasMaxLength(40);
            entity.Property(e => e.sto_depsatmuhkod).HasMaxLength(40);
            entity.Property(e => e.sto_efat_sinif_kodu).HasMaxLength(20);
            entity.Property(e => e.sto_efat_sinif_listesi).HasMaxLength(15);
            entity.Property(e => e.sto_efat_sinif_versiyonu).HasMaxLength(15);
            entity.Property(e => e.sto_giderkodu).HasMaxLength(25);
            entity.Property(e => e.sto_hammadde_kodu).HasMaxLength(25);
            entity.Property(e => e.sto_iade_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_ihrackayitlisatismaliyetimuhkod).HasMaxLength(40);
            entity.Property(e => e.sto_ihrackayitlisatismuhkod).HasMaxLength(40);
            entity.Property(e => e.sto_ilavemas_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_ilavemasmuhkod).HasMaxLength(40);
            entity.Property(e => e.sto_isim).HasMaxLength(127);
            entity.Property(e => e.sto_kalkon_kodu).HasMaxLength(25);
            entity.Property(e => e.sto_karsi_program_kodu).HasMaxLength(127);
            entity.Property(e => e.sto_kategori_kodu).HasMaxLength(25);
            entity.Property(e => e.sto_kisa_ismi).HasMaxLength(50);
            entity.Property(e => e.sto_komisyon_hzmkodu).HasMaxLength(25);
            entity.Property(e => e.sto_kuresel_urun_numarasi).HasMaxLength(50);
            entity.Property(e => e.sto_lastup_date).HasColumnType("datetime");
            entity.Property(e => e.sto_marka_kodu).HasMaxLength(25);
            entity.Property(e => e.sto_mensei).HasMaxLength(30);
            entity.Property(e => e.sto_mkod_artik).HasMaxLength(10);
            entity.Property(e => e.sto_model_kodu).HasMaxLength(25);
            entity.Property(e => e.sto_muh_Iade_kod).HasMaxLength(40);
            entity.Property(e => e.sto_muh_aIiskmuhkod).HasMaxLength(40);
            entity.Property(e => e.sto_muh_kod).HasMaxLength(40);
            entity.Property(e => e.sto_muh_satIadmuhkod).HasMaxLength(40);
            entity.Property(e => e.sto_muh_sat_isk_kod).HasMaxLength(40);
            entity.Property(e => e.sto_muh_sat_muh_kod).HasMaxLength(40);
            entity.Property(e => e.sto_muh_satmalmuhkod).HasMaxLength(40);
            entity.Property(e => e.sto_muhgrup_kodu).HasMaxLength(25);
            entity.Property(e => e.sto_oto_barkod_kod_yapisi).HasMaxLength(50);
            entity.Property(e => e.sto_paket_kodu).HasMaxLength(25);
            entity.Property(e => e.sto_plu_no).ValueGeneratedOnAdd();
            entity.Property(e => e.sto_pozisyonbayrak_kodu).HasMaxLength(25);
            entity.Property(e => e.sto_prim_kodu).HasMaxLength(25);
            entity.Property(e => e.sto_renk_kodu).HasMaxLength(25);
            entity.Property(e => e.sto_resim_url).HasMaxLength(127);
            entity.Property(e => e.sto_reyon_kodu).HasMaxLength(25);
            entity.Property(e => e.sto_sat_cari_kod).HasMaxLength(25);
            entity.Property(e => e.sto_satfiyfark_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_satfiyfarkmuhkod).HasMaxLength(40);
            entity.Property(e => e.sto_satiade_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_satisk_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_satmal_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_sektor_kodu).HasMaxLength(25);
            entity.Property(e => e.sto_sezon_kodu).HasMaxLength(25);
            entity.Property(e => e.sto_sifirbedsatmal_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_sifirbedsatmalmuhkod).HasMaxLength(40);
            entity.Property(e => e.sto_special1).HasMaxLength(4);
            entity.Property(e => e.sto_special2).HasMaxLength(4);
            entity.Property(e => e.sto_special3).HasMaxLength(4);
            entity.Property(e => e.sto_tamamlayici_kodu).HasMaxLength(25);
            entity.Property(e => e.sto_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_uretici_kodu).HasMaxLength(25);
            entity.Property(e => e.sto_uretimkapasite_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_uretimmaliyet_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_urun_sorkod).HasMaxLength(25);
            entity.Property(e => e.sto_yabanci_isim).HasMaxLength(127);
            entity.Property(e => e.sto_yatirimtes_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_yatirimtesmuhkod).HasMaxLength(40);
            entity.Property(e => e.sto_yer_kod).HasMaxLength(25);
            entity.Property(e => e.sto_yurtdisi_satmuhk).HasMaxLength(40);
            entity.Property(e => e.sto_yurtdisisat_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_yurtdisisatmal_ufrsfark_kod).HasMaxLength(40);
            entity.Property(e => e.sto_yurtdisisatmalmuhkod).HasMaxLength(40);
            entity.Property(e => e.sto_yurticisat_ufrsfark_kod).HasMaxLength(40);
        });

        modelBuilder.Entity<STOK_FIYAT_DEGISIKLIKLERI>(entity =>
        {
            entity.HasKey(e => e.fid_Guid).HasName("NDX_STOK_FIYAT_DEGISIKLIKLERI_00");

            entity.ToTable("STOK_FIYAT_DEGISIKLIKLERI");

            entity.HasIndex(e => new { e.fid_evrak_seri_no, e.fid_evrak_sira_no, e.fid_evrak_satir_no }, "NDX_STOK_FIYAT_DEGISIKLIKLERI_02").IsUnique();

            entity.HasIndex(e => new { e.fid_stok_kod, e.fid_tarih, e.fid_saat }, "NDX_STOK_FIYAT_DEGISIKLIKLERI_03");

            entity.HasIndex(e => new { e.fid_tarih, e.fid_saat }, "NDX_STOK_FIYAT_DEGISIKLIKLERI_04");

            entity.Property(e => e.fid_Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.fid_belge_no).HasMaxLength(50);
            entity.Property(e => e.fid_belge_tarih).HasColumnType("datetime");
            entity.Property(e => e.fid_create_date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.fid_eskifiy_iskonto).HasMaxLength(4);
            entity.Property(e => e.fid_evrak_seri_no).HasMaxLength(20);
            entity.Property(e => e.fid_evrak_tarih).HasColumnType("datetime");
            entity.Property(e => e.fid_lastup_date).HasColumnType("datetime");
            entity.Property(e => e.fid_special1).HasMaxLength(4);
            entity.Property(e => e.fid_special2).HasMaxLength(4);
            entity.Property(e => e.fid_special3).HasMaxLength(4);
            entity.Property(e => e.fid_stok_kod).HasMaxLength(25);
            entity.Property(e => e.fid_tarih).HasColumnType("datetime");
            entity.Property(e => e.fid_yenifiy_iskonto).HasMaxLength(4);
        });

        modelBuilder.Entity<SAYIM_SONUCLARI>(entity =>
        {
            entity.HasKey(e => e.sym_Guid)
                .HasName("NDX_SAYIM_SONUCLARI_00");

            entity.ToTable("SAYIM_SONUCLARI");

            entity.Property(e => e.sym_Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.sym_Stokkodu).HasMaxLength(25);
            entity.Property(e => e.sym_barkod).HasMaxLength(50);
            entity.Property(e => e.sym_create_date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.sym_koridorkodu).HasMaxLength(4);
            entity.Property(e => e.sym_lastup_date).HasColumnType("datetime");
            entity.Property(e => e.sym_parti_kodu).HasMaxLength(25);
            entity.Property(e => e.sym_rafkodu).HasMaxLength(4);
            entity.Property(e => e.sym_reyonkodu).HasMaxLength(4);
            entity.Property(e => e.sym_serino).HasMaxLength(50);
            entity.Property(e => e.sym_special1).HasMaxLength(4);
            entity.Property(e => e.sym_special2).HasMaxLength(4);
            entity.Property(e => e.sym_special3).HasMaxLength(4);
            entity.Property(e => e.sym_tarihi).HasColumnType("datetime");
        });

        modelBuilder.Entity<STOK_HAREKETLERI>(entity =>
        {
            entity.HasKey(e => e.sth_Guid)
                .HasName("NDX_STOK_HAREKETLERI_00")
                .HasFillFactor(98);

            entity.ToTable("STOK_HAREKETLERI", tb =>
                {
                    tb.HasTrigger("TRG_STOK_HAREKETLERI_SILINEN_LOG");
                    tb.HasTrigger("mye_STOK_HAREKETLERI_Trigger");
                });

            entity.HasIndex(e => e.sth_belge_no, "IX_STOK_HAREKETLERI_BelgeNo_2025")
                .HasFilter("([sth_tarih]>='2025-01-01' AND [sth_tarih]<'2026-01-01')")
                .HasFillFactor(95);

            entity.HasIndex(e => new { e.sth_belge_no, e.sth_tarih }, "IX_STOK_HAREKETLERI_BelgeNo_Tarih_DESC").IsDescending(true, false);

            entity.HasIndex(e => e.sth_tarih, "NDX_STOK_HAREKETLERI_02").HasFillFactor(98);

            entity.HasIndex(e => new { e.sth_cari_cinsi, e.sth_cari_kodu, e.sth_tarih }, "NDX_STOK_HAREKETLERI_03").HasFillFactor(98);

            entity.HasIndex(e => new { e.sth_stok_kod, e.sth_tarih }, "NDX_STOK_HAREKETLERI_04").HasFillFactor(95);

            entity.HasIndex(e => new { e.sth_evraktip, e.sth_evrakno_seri, e.sth_evrakno_sira, e.sth_satirno }, "NDX_STOK_HAREKETLERI_05")
                .IsUnique()
                .HasFillFactor(98);

            entity.HasIndex(e => new { e.sth_plasiyer_kodu, e.sth_tarih }, "NDX_STOK_HAREKETLERI_06").HasFillFactor(98);

            entity.HasIndex(e => e.sth_fat_uid, "NDX_STOK_HAREKETLERI_07").HasFillFactor(98);

            entity.HasIndex(e => e.sth_sip_uid, "NDX_STOK_HAREKETLERI_08").HasFillFactor(98);

            entity.HasIndex(e => new { e.sth_cari_cinsi, e.sth_cari_kodu, e.sth_stok_kod, e.sth_tarih }, "NDX_STOK_HAREKETLERI_10").HasFillFactor(98);

            entity.HasIndex(e => new { e.sth_stok_kod, e.sth_cari_cinsi, e.sth_cari_kodu, e.sth_tarih }, "NDX_STOK_HAREKETLERI_11").HasFillFactor(98);

            entity.HasIndex(e => e.sth_exim_kodu, "NDX_STOK_HAREKETLERI_12").HasFillFactor(98);

            entity.HasIndex(e => e.sth_isemri_gider_kodu, "NDX_STOK_HAREKETLERI_13").HasFillFactor(98);

            entity.HasIndex(e => new { e.sth_stok_kod, e.sth_cins }, "NDX_STOK_HAREKETLERI_14").HasFillFactor(98);

            entity.HasIndex(e => e.sth_kons_uid, "NDX_STOK_HAREKETLERI_15").HasFillFactor(98);

            entity.Property(e => e.sth_Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.sth_HareketGrupKodu1).HasMaxLength(25);
            entity.Property(e => e.sth_HareketGrupKodu2).HasMaxLength(25);
            entity.Property(e => e.sth_HareketGrupKodu3).HasMaxLength(25);
            entity.Property(e => e.sth_aciklama).HasMaxLength(50);
            entity.Property(e => e.sth_bagli_ithalat_kodu).HasMaxLength(25);
            entity.Property(e => e.sth_belge_no).HasMaxLength(50);
            entity.Property(e => e.sth_belge_tarih).HasColumnType("datetime");
            entity.Property(e => e.sth_cari_kodu).HasMaxLength(25);
            entity.Property(e => e.sth_cari_srm_merkezi).HasMaxLength(25);
            entity.Property(e => e.sth_create_date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.sth_eticaret_kanal_kodu).HasMaxLength(25);
            entity.Property(e => e.sth_evrakno_seri).HasMaxLength(20);
            entity.Property(e => e.sth_exim_kodu).HasMaxLength(25);
            entity.Property(e => e.sth_fis_tarihi).HasColumnType("datetime");
            entity.Property(e => e.sth_isemri_gider_kodu).HasMaxLength(25);
            entity.Property(e => e.sth_ismerkezi_kodu).HasMaxLength(25);
            entity.Property(e => e.sth_lastup_date).HasColumnType("datetime");
            entity.Property(e => e.sth_malkbl_sevk_tarihi).HasColumnType("datetime");
            entity.Property(e => e.sth_parti_kodu).HasMaxLength(25);
            entity.Property(e => e.sth_plasiyer_kodu).HasMaxLength(25);
            entity.Property(e => e.sth_proje_kodu).HasMaxLength(25);
            entity.Property(e => e.sth_special1).HasMaxLength(4);
            entity.Property(e => e.sth_special2).HasMaxLength(4);
            entity.Property(e => e.sth_special3).HasMaxLength(4);
            entity.Property(e => e.sth_stok_kod).HasMaxLength(25);
            entity.Property(e => e.sth_stok_srm_merkezi).HasMaxLength(25);
            entity.Property(e => e.sth_tarih).HasColumnType("datetime");
            entity.Property(e => e.sth_teslim_tarihi).HasColumnType("datetime");
        });

        modelBuilder.Entity<STOK_HAREKETLERI_EK>(entity =>
        {
            entity.HasKey(e => e.sthek_Guid)
                .HasName("NDX_STOK_HAREKETLERI_EK_00")
                .HasFillFactor(98);

            entity.ToTable("STOK_HAREKETLERI_EK");

            entity.HasIndex(e => e.sthek_related_uid, "NDX_STOK_HAREKETLERI_EK_02")
                .IsUnique()
                .HasFillFactor(98);

            entity.HasIndex(e => new { e.sth_satistipi, e.sth_vardiya_tarihi, e.sth_vardiya_no }, "NDX_STOK_HAREKETLERI_EK_03").HasFillFactor(98);

            entity.HasIndex(e => new { e.sth_fiyfark_esas_evrak_seri, e.sth_fiyfark_esas_evrak_sira, e.sth_fiyfark_esas_satir_no }, "NDX_STOK_HAREKETLERI_EK_04").HasFillFactor(98);

            entity.HasIndex(e => e.sth_hizlisatis_promosyonkodu_1, "NDX_STOK_HAREKETLERI_EK_05").HasFillFactor(98);

            entity.HasIndex(e => e.sth_hizlisatis_promosyonkodu_2, "NDX_STOK_HAREKETLERI_EK_06").HasFillFactor(98);

            entity.HasIndex(e => e.sth_hizlisatis_promosyonkodu_3, "NDX_STOK_HAREKETLERI_EK_07").HasFillFactor(98);

            entity.Property(e => e.sthek_Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.sth_diib_belge_no).HasMaxLength(25);
            entity.Property(e => e.sth_fis_tarihi2).HasColumnType("datetime");
            entity.Property(e => e.sth_fiyfark_esas_evrak_seri).HasMaxLength(20);
            entity.Property(e => e.sth_hizlisatis_promosyonkodu_1).HasMaxLength(25);
            entity.Property(e => e.sth_hizlisatis_promosyonkodu_2).HasMaxLength(25);
            entity.Property(e => e.sth_hizlisatis_promosyonkodu_3).HasMaxLength(25);
            entity.Property(e => e.sth_hks_carikodu).HasMaxLength(25);
            entity.Property(e => e.sth_hks_kunye_no).HasMaxLength(50);
            entity.Property(e => e.sth_iade_evrak_seri).HasMaxLength(20);
            entity.Property(e => e.sth_ihracat_kredi_kodu).HasMaxLength(4);
            entity.Property(e => e.sth_istisna).HasMaxLength(5);
            entity.Property(e => e.sth_karsi_program_kodu).HasMaxLength(127);
            entity.Property(e => e.sth_mensey_ulke_kodu).HasMaxLength(4);
            entity.Property(e => e.sth_otv_istisnakodu).HasMaxLength(5);
            entity.Property(e => e.sth_sas_kalem_no).HasMaxLength(50);
            entity.Property(e => e.sth_utsdigergerekceaciklamasi).HasMaxLength(50);
            entity.Property(e => e.sth_vardiya_tarihi).HasColumnType("datetime");
            entity.Property(e => e.sth_yat_tes_kodu).HasMaxLength(25);
            entity.Property(e => e.sthek_create_date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.sthek_lastup_date).HasColumnType("datetime");
            entity.Property(e => e.sthek_special1).HasMaxLength(4);
            entity.Property(e => e.sthek_special2).HasMaxLength(4);
            entity.Property(e => e.sthek_special3).HasMaxLength(4);
        });

        modelBuilder.Entity<STOK_HAREKETLERI_OZET>(entity =>
        {
            entity.HasKey(e => e.sho_RECno)
                .HasName("NDX_STOK_HAREKETLERI_OZET_00")
                .HasFillFactor(30);

            entity.ToTable("STOK_HAREKETLERI_OZET");

            entity.HasIndex(e => new { e.sho_StokKodu, e.sho_Depo, e.sho_MaliYil, e.sho_Donem, e.sho_HareketCins, e.sho_SrmMerkezi, e.sho_ProjeKodu, e.sho_firmano, e.sho_subeno }, "NDX_STOK_HAREKETLERI_OZET_01")
                .IsUnique()
                .HasFillFactor(30);

            entity.Property(e => e.sho_Belge_Alt_Cikis).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Belge_Alt_CikisIade).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Belge_Alt_Giris).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Belge_Alt_GirisIade).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Belge_Ana_Cikis).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Belge_Ana_CikisIade).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Belge_Ana_Giris).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Belge_Ana_GirisIade).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Belge_Orj_Cikis).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Belge_Orj_CikisIade).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Belge_Orj_Giris).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Belge_Orj_GirisIade).HasDefaultValue(0.0);
            entity.Property(e => e.sho_CikisIade).HasDefaultValue(0.0);
            entity.Property(e => e.sho_CikisIade_2).HasDefaultValue(0.0);
            entity.Property(e => e.sho_CikisNormal).HasDefaultValue(0.0);
            entity.Property(e => e.sho_CikisNormal_2).HasDefaultValue(0.0);
            entity.Property(e => e.sho_GirisIade).HasDefaultValue(0.0);
            entity.Property(e => e.sho_GirisIade_2).HasDefaultValue(0.0);
            entity.Property(e => e.sho_GirisNormal).HasDefaultValue(0.0);
            entity.Property(e => e.sho_GirisNormal_2).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Maliyet_Alt_Cikis).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Maliyet_Alt_CikisIade).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Maliyet_Alt_Giris).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Maliyet_Alt_GirisIade).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Maliyet_Ana_Cikis).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Maliyet_Ana_CikisIade).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Maliyet_Ana_Giris).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Maliyet_Ana_GirisIade).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Maliyet_Orj_Cikis).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Maliyet_Orj_CikisIade).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Maliyet_Orj_Giris).HasDefaultValue(0.0);
            entity.Property(e => e.sho_Maliyet_Orj_GirisIade).HasDefaultValue(0.0);
            entity.Property(e => e.sho_ProjeKodu).HasMaxLength(25);
            entity.Property(e => e.sho_SrmMerkezi).HasMaxLength(25);
            entity.Property(e => e.sho_StokKodu).HasMaxLength(25);
        });

        modelBuilder.Entity<STOK_SATIS_FIYAT_LISTELERI>(entity =>
        {
            entity.HasKey(e => e.sfiyat_Guid).HasName("NDX_STOK_SATIS_FIYAT_LISTELERI_00");

            entity.ToTable("STOK_SATIS_FIYAT_LISTELERI");

            entity.HasIndex(e => new { e.sfiyat_stokkod, e.sfiyat_listesirano, e.sfiyat_deposirano, e.sfiyat_birim_pntr, e.sfiyat_odemeplan }, "NDX_STOK_SATIS_FIYAT_LISTELERI_02").IsUnique();

            entity.Property(e => e.sfiyat_Guid).HasDefaultValueSql("(newid())");
            entity.Property(e => e.sfiyat_create_date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.sfiyat_iskontokod).HasMaxLength(4);
            entity.Property(e => e.sfiyat_kampanyakod).HasMaxLength(4);
            entity.Property(e => e.sfiyat_lastup_date).HasColumnType("datetime");
            entity.Property(e => e.sfiyat_special1).HasMaxLength(4);
            entity.Property(e => e.sfiyat_special2).HasMaxLength(4);
            entity.Property(e => e.sfiyat_special3).HasMaxLength(4);
            entity.Property(e => e.sfiyat_stokkod).HasMaxLength(25);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
