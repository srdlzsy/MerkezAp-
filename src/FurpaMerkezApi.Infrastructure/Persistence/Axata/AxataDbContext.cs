using FurpaMerkezApi.Infrastructure.Persistence.Axata.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Axata;

public sealed class AxataDbContext(DbContextOptions<AxataDbContext> options) : DbContext(options)
{
    public DbSet<ENT000> ENT000s => Set<ENT000>();

    public DbSet<ENT001> ENT001s => Set<ENT001>();

    public DbSet<ENT006> ENT006s => Set<ENT006>();

    public DbSet<ENT007> ENT007s => Set<ENT007>();

    public DbSet<ENT009> ENT009s => Set<ENT009>();

    public DbSet<ENT009_DETAIL> ENT009_DETAILs => Set<ENT009_DETAIL>();

    public DbSet<ENT013> ENT013s => Set<ENT013>();

    public DbSet<ENT013_MST> ENT013_MSTs => Set<ENT013_MST>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureOutboundOrders(modelBuilder);
        ConfigureOutboundOrderLines(modelBuilder);
        ConfigureOutboundDeliveries(modelBuilder);
        ConfigureOutboundDeliveryLines(modelBuilder);
        ConfigureInboundAtfHeaders(modelBuilder);
        ConfigureInboundAtfDetails(modelBuilder);
        ConfigureInboundOrders(modelBuilder);
        ConfigureInboundOrderHeaders(modelBuilder);
    }

    private static void ConfigureOutboundOrders(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ENT000>();
        entity.ToTable("ENT000");
        entity.HasKey(item => item.S00ID);
        entity.Property(item => item.S00ID).ValueGeneratedOnAdd();

        ConfigureMaxLength(entity, 1, nameof(ENT000.S00FDRM));
        ConfigureMaxLength(entity, 3, nameof(ENT000.S00HTP1), nameof(ENT000.S00HTP2), nameof(ENT000.S00EC_UKOD), nameof(ENT000.S00EC_ILKOD));
        ConfigureMaxLength(entity, 4, nameof(ENT000.S00DKAN), nameof(ENT000.S00FBLK), nameof(ENT000.S00SKOD));
        ConfigureMaxLength(entity, 5, nameof(ENT000.S00KTUR), nameof(ENT000.S00TSEK));
        ConfigureMaxLength(entity, 6, nameof(ENT000.S00TESS));
        ConfigureMaxLength(entity, 8, nameof(ENT000.S00TEST));
        ConfigureMaxLength(entity, 10, nameof(ENT000.S00SIST), nameof(ENT000.S00EC_PKOD));
        ConfigureMaxLength(entity, 12, nameof(ENT000.S00SUSR));
        ConfigureMaxLength(entity, 16, nameof(ENT000.S00IDOC));
        ConfigureMaxLength(entity, 20, nameof(ENT000.S00ACIN));
        ConfigureMaxLength(
            entity,
            50,
            nameof(ENT000.S00TESN),
            nameof(ENT000.S00SIPN),
            nameof(ENT000.S00SORG),
            nameof(ENT000.S00SMUS),
            nameof(ENT000.S00TMUS),
            nameof(ENT000.S00NKOD),
            nameof(ENT000.S00MSIP),
            nameof(ENT000.S00PLAKA),
            nameof(ENT000.S00MALIK),
            nameof(ENT000.S00TADR),
            nameof(ENT000.S00CARGO),
            nameof(ENT000.S00EC_TEL1),
            nameof(ENT000.S00EC_TEL2),
            nameof(ENT000.S00EC_PLATFORM),
            nameof(ENT000.S00EC_HIZMETSEKLI),
            nameof(ENT000.S00EC_TESLIMSEKLI),
            nameof(ENT000.S00EC_ODEMESEKLI),
            nameof(ENT000.S00EC_VERDA),
            nameof(ENT000.S00EC_VERNO),
            nameof(ENT000.S00EC_CARGOLABEL),
            nameof(ENT000.S00PLAKA2),
            nameof(ENT000.S00EC_TAHSEKLI));
        ConfigureMaxLength(entity, 100, nameof(ENT000.S00EC_ILCE), nameof(ENT000.S00EC_SEMT));
        ConfigureMaxLength(entity, 200, nameof(ENT000.S00EC_FIRADI));
        ConfigureMaxLength(entity, 500, nameof(ENT000.S00SNOT), nameof(ENT000.S00INT1), nameof(ENT000.S00INT2), nameof(ENT000.S00EC_NOT));
        ConfigureMaxLength(entity, 1000, nameof(ENT000.S00EC_EMAIL));
        ConfigurePrecision(entity, 8, 0, nameof(ENT000.S00ITAR), nameof(ENT000.S00IZMN));
        ConfigurePrecision(entity, 10, 0, nameof(ENT000.S00YUKN));
        entity.Property(item => item.S00EC_TUTAR).HasColumnType("money");
        ConfigureDateTime(entity, nameof(ENT000.S00CDAT), nameof(ENT000.S00UDAT), nameof(ENT000.S00EC_KARGOTESZMN), nameof(ENT000.S00EC_KARGOTESZMN2));
    }

    private static void ConfigureOutboundOrderLines(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ENT001>();
        entity.ToTable("ENT001");
        entity.HasKey(item => item.S01ID);
        entity.Property(item => item.S01ID).ValueGeneratedOnAdd();

        ConfigureMaxLength(entity, 1, nameof(ENT001.S01SARG));
        ConfigureMaxLength(entity, 2, nameof(ENT001.S01BNED));
        ConfigureMaxLength(entity, 3, nameof(ENT001.S01FRDP), nameof(ENT001.S01HKOD));
        ConfigureMaxLength(entity, 4, nameof(ENT001.S01SKOD), nameof(ENT001.S01DEPO));
        ConfigureMaxLength(entity, 5, nameof(ENT001.S01ITIP));
        ConfigureMaxLength(entity, 16, nameof(ENT001.S01IDOC));
        ConfigureMaxLength(
            entity,
            50,
            nameof(ENT001.S01TESL),
            nameof(ENT001.S01KALN),
            nameof(ENT001.S01UKAL),
            nameof(ENT001.S01MURU),
            nameof(ENT001.S01BUNDLESKU),
            nameof(ENT001.S01BUNDLELINE));
        ConfigureMaxLength(entity, 200, nameof(ENT001.S01LOTN), nameof(ENT001.S01LOTN2), nameof(ENT001.S01LOTN3));
        entity.Property(item => item.S01SKU).HasColumnType("ft_SKU");
        ConfigurePrecision(entity, 8, 0, nameof(ENT001.S01MSTR), nameof(ENT001.S01ITAR), nameof(ENT001.S01IZMN), nameof(ENT001.S01MYTR));
        ConfigurePrecision(entity, 15, 2, nameof(ENT001.S01PRICE));
        ConfigurePrecision(entity, 15, 3, nameof(ENT001.S01MIKT), nameof(ENT001.S01SIM), nameof(ENT001.S01TMIK), nameof(ENT001.S01OSMIK), nameof(ENT001.S01PICKSIM), nameof(ENT001.S01BUNDLEQTY));
        ConfigureDateTime(entity, nameof(ENT001.S01CDAT), nameof(ENT001.S01UDAT));
    }

    private static void ConfigureOutboundDeliveries(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ENT006>();
        entity.ToTable("ENT006");
        entity.HasKey(item => item.S06SIRA);
        entity.Property(item => item.S06SIRA).ValueGeneratedOnAdd();

        ConfigureMaxLength(entity, 1, nameof(ENT006.S06HTIP), nameof(ENT006.S06STTU), nameof(ENT006.S06SSIP), nameof(ENT006.S06GBEK), nameof(ENT006.S06RPRT));
        ConfigureMaxLength(entity, 3, nameof(ENT006.S06OHTP), nameof(ENT006.S06HKOD));
        ConfigureMaxLength(entity, 4, nameof(ENT006.S06SKOD), nameof(ENT006.S06DEPO));
        ConfigureMaxLength(entity, 6, nameof(ENT006.S06IRSZ));
        ConfigureMaxLength(entity, 7, nameof(ENT006.S06REFN));
        ConfigureMaxLength(entity, 8, nameof(ENT006.S06IRST), nameof(ENT006.S06FSTR));
        ConfigureMaxLength(entity, 10, nameof(ENT006.S06KAMT));
        ConfigureMaxLength(entity, 11, nameof(ENT006.S06KAMN));
        ConfigureMaxLength(entity, 12, nameof(ENT006.S06INUM));
        ConfigureMaxLength(entity, 16, nameof(ENT006.S06KONT));
        ConfigureMaxLength(entity, 20, nameof(ENT006.S06ACIN));
        ConfigureMaxLength(
            entity,
            50,
            nameof(ENT006.S06TESL),
            nameof(ENT006.S06PLKA),
            nameof(ENT006.S06RSIP),
            nameof(ENT006.S06FIRM),
            nameof(ENT006.S06TFIR),
            nameof(ENT006.S06NTIP),
            nameof(ENT006.S06PLKA2),
            nameof(ENT006.S06IPTKOD),
            nameof(ENT006.S06KIMLIKNO),
            nameof(ENT006.S06EXPLAKA),
            nameof(ENT006.S06EXSEALNO),
            nameof(ENT006.S06EXNAKKOD));
        ConfigureMaxLength(entity, 100, nameof(ENT006.S06FNAME), nameof(ENT006.S06SURUCU));
        ConfigurePrecision(entity, 1, 0, nameof(ENT006.S06STAT));
        ConfigurePrecision(entity, 6, 0, nameof(ENT006.S06IZMN), nameof(ENT006.S06TMZM), nameof(ENT006.S06PERS));
        ConfigurePrecision(entity, 8, 0, nameof(ENT006.S06ITAR), nameof(ENT006.S06TMTR));
        ConfigurePrecision(entity, 10, 0, nameof(ENT006.S06YUKN));
    }

    private static void ConfigureOutboundDeliveryLines(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ENT007>();
        entity.ToTable("ENT007");
        entity.HasKey(item => item.S07SIRA);
        entity.Property(item => item.S07SIRA).ValueGeneratedOnAdd();

        ConfigureMaxLength(entity, 1, nameof(ENT007.S07STTU));
        ConfigureMaxLength(entity, 2, nameof(ENT007.S07BNED));
        ConfigureMaxLength(entity, 3, nameof(ENT007.S07HKOD));
        ConfigureMaxLength(entity, 4, nameof(ENT007.S07SIRK));
        ConfigureMaxLength(
            entity,
            50,
            nameof(ENT007.S07TESL),
            nameof(ENT007.S07KALN),
            nameof(ENT007.S07KNED),
            nameof(ENT007.S07BARKOD));
        ConfigureMaxLength(entity, 100, nameof(ENT007.S07FNAME));
        ConfigureMaxLength(entity, 200, nameof(ENT007.S07LOTN), nameof(ENT007.S07LOTN2), nameof(ENT007.S07LOTN3));
        entity.Property(item => item.S07SKOD).HasColumnType("ft_SKU");
        ConfigurePrecision(entity, 1, 0, nameof(ENT007.S07STAT));
        ConfigurePrecision(entity, 6, 0, nameof(ENT007.S07IZMN), nameof(ENT007.S07TMZM));
        ConfigurePrecision(entity, 8, 0, nameof(ENT007.S07ITAR), nameof(ENT007.S07TMTR), nameof(ENT007.S07MFIFO));
        ConfigurePrecision(entity, 10, 0, nameof(ENT007.S07YUKN));
        ConfigurePrecision(entity, 15, 3, nameof(ENT007.S07MIKT), nameof(ENT007.S07SIMM), nameof(ENT007.S07TOPM), nameof(ENT007.S07OSMIK));
    }

    private static void ConfigureInboundAtfHeaders(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ENT009>();
        entity.ToTable("ENT009");
        entity.HasKey(item => item.S09SIRA);
        entity.Property(item => item.S09SIRA).ValueGeneratedOnAdd();

        ConfigureMaxLength(entity, 3, nameof(ENT009.S09HTIP), nameof(ENT009.S09EHTP));
        ConfigureMaxLength(entity, 4, nameof(ENT009.S09SKOD), nameof(ENT009.S09DEPO));
        ConfigureMaxLength(entity, 10, nameof(ENT009.S09USER), nameof(ENT009.S09BSNO));
        ConfigureMaxLength(entity, 20, nameof(ENT009.S09ATEL));
        ConfigureMaxLength(
            entity,
            50,
            nameof(ENT009.S09TESL),
            nameof(ENT009.S09SIPN),
            nameof(ENT009.S09PLKA),
            nameof(ENT009.S09SOFOR),
            nameof(ENT009.S09NAKL),
            nameof(ENT009.S09MALIK),
            nameof(ENT009.S09CONT),
            nameof(ENT009.S09NAKTIPI),
            nameof(ENT009.S09ATIPI),
            nameof(ENT009.S09EWAYBILLNO),
            nameof(ENT009.S09SEVKTIPI),
            nameof(ENT009.S09KIMLIKNO),
            nameof(ENT009.S09FIRMA));
        ConfigureMaxLength(entity, 100, nameof(ENT009.S09FNAME), nameof(ENT009.S09MUHURNO));
        ConfigureMaxLength(entity, 200, nameof(ENT009.S09ACK1), nameof(ENT009.S09ACK2), nameof(ENT009.S09ACK3));
        ConfigurePrecision(entity, 1, 0, nameof(ENT009.S09STAT));
        ConfigurePrecision(entity, 4, 0, nameof(ENT009.S09SYIL));
        ConfigurePrecision(entity, 5, 0, nameof(ENT009.S09ITIP));
        ConfigurePrecision(entity, 6, 0, nameof(ENT009.S09IRZM), nameof(ENT009.S09ISZM), nameof(ENT009.S09TMZM));
        ConfigurePrecision(entity, 7, 0, nameof(ENT009.S09SNO));
        ConfigurePrecision(entity, 8, 0, nameof(ENT009.S09IRTR), nameof(ENT009.S09FSTR), nameof(ENT009.S09ISTR), nameof(ENT009.S09TMTR), nameof(ENT009.S09TMSTESTAR));
        ConfigurePrecision(entity, 10, 0, nameof(ENT009.S09YUKN), nameof(ENT009.S09INUM));
        ConfigurePrecision(entity, 15, 3, nameof(ENT009.S09CONA), nameof(ENT009.S09BRUTAGR));
    }

    private static void ConfigureInboundAtfDetails(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ENT009_DETAIL>();
        entity.ToTable("ENT009_DETAIL");
        entity.HasKey(item => item.S09SIRA);
        entity.Property(item => item.S09SIRA).ValueGeneratedOnAdd();

        ConfigureMaxLength(entity, 3, nameof(ENT009_DETAIL.S09HTIP), nameof(ENT009_DETAIL.S09EHTP));
        ConfigureMaxLength(entity, 4, nameof(ENT009_DETAIL.S09SKOD), nameof(ENT009_DETAIL.S09DEPO));
        ConfigureMaxLength(entity, 10, nameof(ENT009_DETAIL.S09BSNO), nameof(ENT009_DETAIL.S09USER));
        ConfigureMaxLength(
            entity,
            50,
            nameof(ENT009_DETAIL.S09SIPN),
            nameof(ENT009_DETAIL.S09KALN),
            nameof(ENT009_DETAIL.S09REFSIP),
            nameof(ENT009_DETAIL.S09MALIK),
            nameof(ENT009_DETAIL.S09PLKA),
            nameof(ENT009_DETAIL.S09CONT),
            nameof(ENT009_DETAIL.S09ATEL),
            nameof(ENT009_DETAIL.S09SOFOR),
            nameof(ENT009_DETAIL.S09NAKL),
            nameof(ENT009_DETAIL.S09FORMAT));
        ConfigureMaxLength(entity, 100, nameof(ENT009_DETAIL.S09FNAME));
        ConfigurePrecision(entity, 6, 0, nameof(ENT009_DETAIL.S09IRZM), nameof(ENT009_DETAIL.S09ISZM), nameof(ENT009_DETAIL.S09TMZM));
        ConfigurePrecision(entity, 8, 0, nameof(ENT009_DETAIL.S09IRTR), nameof(ENT009_DETAIL.S09FSTR), nameof(ENT009_DETAIL.S09ISTR), nameof(ENT009_DETAIL.S09TMTR));
        ConfigurePrecision(entity, 15, 3, nameof(ENT009_DETAIL.S09CONA));
    }

    private static void ConfigureInboundOrders(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ENT013>();
        entity.ToTable("ENT013");
        entity.HasKey(item => item.S13ID);
        entity.Property(item => item.S13ID).ValueGeneratedOnAdd();

        ConfigureMaxLength(entity, 3, nameof(ENT013.S13HKOD));
        ConfigureMaxLength(entity, 4, nameof(ENT013.S13SKOD), nameof(ENT013.S13AKOD));
        ConfigureMaxLength(
            entity,
            50,
            nameof(ENT013.S13BNUM),
            nameof(ENT013.S13KALN),
            nameof(ENT013.S13FIRM),
            nameof(ENT013.S13UYER),
            nameof(ENT013.S13TESS),
            nameof(ENT013.S13MAKK),
            nameof(ENT013.S13MALIK),
            nameof(ENT013.S13IRSN),
            nameof(ENT013.S13REFKAL),
            nameof(ENT013.S13IPKOD));
        ConfigureMaxLength(entity, 200, nameof(ENT013.S13LOT), nameof(ENT013.S13LOT2), nameof(ENT013.S13LOT3));
        ConfigureMaxLength(entity, 4000, nameof(ENT013.S13NOT1), nameof(ENT013.S13NOT2));
        entity.Property(item => item.S13SKU).HasColumnType("ft_SKU");
        ConfigurePrecision(entity, 1, 0, nameof(ENT013.S13KTIP));
        ConfigurePrecision(entity, 6, 0, nameof(ENT013.S13IZMN), nameof(ENT013.S13TMZM));
        ConfigurePrecision(entity, 8, 0, nameof(ENT013.S13SIPT), nameof(ENT013.S13ITAR), nameof(ENT013.S13TMTR), nameof(ENT013.S13IRST), nameof(ENT013.S13TEST));
        ConfigurePrecision(entity, 15, 3, nameof(ENT013.S13MIKT), nameof(ENT013.S13SIM), nameof(ENT013.S13TMIK), nameof(ENT013.S13OSMIK));
        ConfigureDateTime(entity, nameof(ENT013.S13RCVDATE));
    }

    private static void ConfigureInboundOrderHeaders(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ENT013_MST>();
        entity.ToTable("ENT013_MST");
        entity.HasKey(item => item.S13ID);
        entity.Property(item => item.S13ID).ValueGeneratedOnAdd();

        ConfigureMaxLength(entity, 3, nameof(ENT013_MST.S13HKOD), nameof(ENT013_MST.S13EHKD));
        ConfigureMaxLength(entity, 4, nameof(ENT013_MST.S13SKOD), nameof(ENT013_MST.S13AKOD));
        ConfigureMaxLength(entity, 10, nameof(ENT013_MST.S13SIST));
        ConfigureMaxLength(entity, 20, nameof(ENT013_MST.S13TEL1), nameof(ENT013_MST.S13TEL2));
        ConfigureMaxLength(
            entity,
            50,
            nameof(ENT013_MST.S13BNUM),
            nameof(ENT013_MST.S13REFN),
            nameof(ENT013_MST.S13FIRM),
            nameof(ENT013_MST.S13MALIK),
            nameof(ENT013_MST.S13IRSN),
            nameof(ENT013_MST.S13PLKA),
            nameof(ENT013_MST.S13SOFOR),
            nameof(ENT013_MST.S13SSIP),
            nameof(ENT013_MST.S13SFIRM));
        ConfigureMaxLength(entity, 500, nameof(ENT013_MST.S13NOT1), nameof(ENT013_MST.S13NOT2), nameof(ENT013_MST.S13NOT3));
        ConfigurePrecision(entity, 1, 0, nameof(ENT013_MST.S13KTIP));
        ConfigurePrecision(entity, 6, 0, nameof(ENT013_MST.S13IZMN));
        ConfigurePrecision(entity, 8, 0, nameof(ENT013_MST.S13SIPT), nameof(ENT013_MST.S13TEST), nameof(ENT013_MST.S13IRST), nameof(ENT013_MST.S13ITAR));
        ConfigureDateTime(entity, nameof(ENT013_MST.S13RCVDATE));
    }

    private static void ConfigureMaxLength<TEntity>(
        EntityTypeBuilder<TEntity> entity,
        int maxLength,
        params string[] propertyNames)
        where TEntity : class
    {
        foreach (var propertyName in propertyNames)
        {
            entity.Property(propertyName).HasMaxLength(maxLength);
        }
    }

    private static void ConfigurePrecision<TEntity>(
        EntityTypeBuilder<TEntity> entity,
        int precision,
        int scale,
        params string[] propertyNames)
        where TEntity : class
    {
        foreach (var propertyName in propertyNames)
        {
            entity.Property(propertyName).HasPrecision(precision, scale);
        }
    }

    private static void ConfigureDateTime<TEntity>(
        EntityTypeBuilder<TEntity> entity,
        params string[] propertyNames)
        where TEntity : class
    {
        foreach (var propertyName in propertyNames)
        {
            entity.Property(propertyName).HasColumnType("datetime");
        }
    }
}
