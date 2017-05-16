using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MigrationTool.Models
{
    // ReSharper disable once ClassNeverInstantiated.Global
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class ErmContext : DbContext
    {
        public ErmContext(DbContextOptions<ErmContext> options)
            : base(options)
        {
            // ReSharper disable once VirtualMemberCallInConstructor
            ChangeTracker.AutoDetectChangesEnabled = false;
        }

        public virtual DbSet<ElementTemplateLink> AdsTemplatesAdsElementTemplates { get; set; }

        public virtual DbSet<AdvertisementElementTemplate> AdvertisementElementTemplates { get; set; }

        public virtual DbSet<AdvertisementElement> AdvertisementElements { get; set; }

        public virtual DbSet<AdvertisementTemplate> AdvertisementTemplates { get; set; }

        public virtual DbSet<Advertisement> Advertisements { get; set; }

        public virtual DbSet<File> Files { get; set; }

        public virtual DbSet<OrderPositionAdvertisement> OrderPositionAdvertisement { get; set; }

        public virtual DbSet<OrderPosition> OrderPositions { get; set; }

        public virtual DbSet<Order> Orders { get; set; }

        public virtual DbSet<Position> Positions { get; set; }

        public virtual DbSet<PricePosition> PricePositions { get; set; }

        public virtual DbSet<Price> Prices { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ElementTemplateLink>(entity =>
            {
                entity.ToTable("AdsTemplatesAdsElementTemplates", "Billing");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.CreatedOn).HasColumnType("datetime2(2)");

                entity.Property(e => e.IsDeleted).HasDefaultValueSql("0");

                entity.Property(e => e.ModifiedOn).HasColumnType("datetime2(2)");

                entity.HasOne(d => d.ElementTemplate)
                    .WithMany(p => p.AdsTemplatesAdsElementTemplates)
                    .HasForeignKey(d => d.AdsElementTemplateId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_AdsTemplatesAdsElementTemplates_AdvertisementElementTemplates");

                entity.HasOne(d => d.Template)
                    .WithMany(p => p.ElementTemplatesLink)
                    .HasForeignKey(d => d.AdsTemplateId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_AdsTemplatesAdsElementTemplates_AdvertisementTemplates");
            });

            modelBuilder.Entity<AdvertisementElementTemplate>(entity =>
            {
                entity.ToTable("AdvertisementElementTemplates", "Billing");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.CreatedOn).HasColumnType("datetime2(2)");

                entity.Property(e => e.FileExtensionRestriction).HasMaxLength(128);

                entity.Property(e => e.ImageDimensionRestriction).HasMaxLength(128);

                entity.Property(e => e.IsAdvertisementLink).IsRequired().HasDefaultValueSql("0");

                entity.Property(e => e.IsPhoneNumber).IsRequired();

                entity.Property(e => e.IsAlphaChannelRequired).IsRequired().HasDefaultValueSql("0");

                entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("0");

                entity.Property(e => e.IsRequired).IsRequired();

                entity.Property(e => e.ModifiedOn).HasColumnType("datetime2(2)");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(256);

                entity.Property(e => e.NeedsValidation).IsRequired().HasDefaultValueSql("0");
            });

            modelBuilder.Entity<AdvertisementElement>(entity =>
            {
                entity.ToTable("AdvertisementElements", "Billing");

                entity.HasIndex(e => new { e.AdvertisementId, e.FileId, e.IsDeleted })
                    .HasName("UIX_AdvertismentElements_AdvertisementId_FileId_IsDeleted");

                entity.HasIndex(e => new { e.Id, e.BeginDate, e.EndDate, e.IsDeleted, e.AdvertisementId })
                    .HasName("IX_AdvertisementElements_AdvertisementId_Id-BeginDate-EndDate-IsDeleted");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.BeginDate).HasColumnType("datetime2(2)");

                entity.Property(e => e.CreatedOn).HasColumnType("datetime2(2)");

                entity.Property(e => e.EndDate).HasColumnType("datetime2(2)");

                entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("0");

                entity.Property(e => e.ModifiedOn).HasColumnType("datetime2(2)");

                entity.HasOne(d => d.AdsTemplatesAdsElementTemplates)
                    .WithMany(p => p.Elements)
                    .HasForeignKey(d => d.AdsTemplatesAdsElementTemplatesId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_AdvertisementElements_AdsTemplatesAdsElementTemplates");

                entity.HasOne(d => d.AdvertisementElementTemplate)
                    .WithMany(p => p.AdvertisementElements)
                    .HasForeignKey(d => d.AdvertisementElementTemplateId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_AdvertisementElements_AdvertisementElementTemplates");

                entity.HasOne(d => d.Advertisement)
                    .WithMany(p => p.AdvertisementElements)
                    .HasForeignKey(d => d.AdvertisementId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_AdvertisementElement_Advertisement");

                entity.HasOne(d => d.File)
                    .WithMany(p => p.AdvertisementElements)
                    .HasForeignKey(d => d.FileId)
                    .HasConstraintName("FK_AdvertisementElements_Files");
            });

            modelBuilder.Entity<AdvertisementTemplate>(entity =>
            {
                entity.ToTable("AdvertisementTemplates", "Billing");

                entity.HasIndex(e => e.Name)
                    .HasName("IX_AdvertisementTemplates_Name");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.Comment).HasMaxLength(512);

                entity.Property(e => e.CreatedOn).HasColumnType("datetime2(2)");

                entity.Property(e => e.IsAllowedToWhiteList).IsRequired().HasDefaultValueSql("0");

                entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("0");

                entity.Property(e => e.IsPublished).IsRequired().HasDefaultValueSql("0");

                entity.Property(e => e.ModifiedOn).HasColumnType("datetime2(2)");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(256);
            });

            modelBuilder.Entity<Advertisement>(entity =>
            {
                entity.ToTable("Advertisements", "Billing");

                entity.HasIndex(e => e.CreatedOn)
                    .HasName("IX_Advertisements_CreatedOn");

                entity.HasIndex(e => new { e.FirmId, e.AdvertisementTemplateId, e.IsDeleted })
                    .HasName("IX_Advertisements_FirmId_AdvertisementTemplateId_IsDeleted");

                entity.HasIndex(e => new { e.Id, e.AdvertisementTemplateId, e.IsSelectedToWhiteList })
                    .HasName("IX_Advertisements_IsSelectedToWhiteList");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.Comment).HasMaxLength(512);

                entity.Property(e => e.CreatedOn).HasColumnType("datetime2(2)");

                entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("0");

                entity.Property(e => e.ModifiedOn).HasColumnType("datetime2(2)");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(128);

                entity.HasOne(d => d.AdvertisementTemplate)
                    .WithMany(p => p.Advertisements)
                    .HasForeignKey(d => d.AdvertisementTemplateId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_Advertisement_AdvertisementTemplates");
            });

            modelBuilder.Entity<File>(entity =>
            {
                entity.ToTable("Files", "Shared");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.ContentType)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.CreatedOn).HasColumnType("datetime2(2)");

                entity.Property(e => e.Data).IsRequired();

                entity.Property(e => e.FileName)
                    .IsRequired()
                    .HasMaxLength(1024);

                entity.Property(e => e.ModifiedOn).HasColumnType("datetime2(2)");
            });

            modelBuilder.Entity<OrderPositionAdvertisement>(entity =>
            {
                entity.ToTable("OrderPositionAdvertisement", "Billing");

                entity.HasIndex(e => new { e.AdvertisementId, e.OrderPositionId })
                    .HasName("IX_OrderPositionAdvertisement_OrderPositionId");

                entity.HasIndex(e => new { e.OrderPositionId, e.PositionId })
                    .HasName("IX_OrderPositionAdvertisement_OrderPositionId_PositionId");

                entity.HasIndex(e => new { e.OrderPositionId, e.AdvertisementId, e.PositionId })
                    .HasName("IX_OrderPositionAdvertisement_OrderPositionId-AdvertisementId-PositionId");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.CreatedOn).HasColumnType("datetime2(2)");

                entity.Property(e => e.ModifiedOn).HasColumnType("datetime2(2)");

                entity.HasOne(d => d.Advertisement)
                    .WithMany(p => p.OrderPositionAdvertisement)
                    .HasForeignKey(d => d.AdvertisementId)
                    .HasConstraintName("FK_OrderPositionAdvertisement_Advertisements");

                entity.HasOne(d => d.Position)
                    .WithMany(p => p.OrderPositionAdvertisement)
                    .HasForeignKey(d => d.PositionId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_OrderPositionAdvertisement_Positions");

                entity.HasOne(d => d.OrderPosition)
                    .WithMany(p => p.OrderPositionAdvertisement)
                    .HasForeignKey(d => d.OrderPositionId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_OrderPositionAdvertisement_OrderPositions");
            });

            modelBuilder.Entity<OrderPosition>(entity =>
            {
                entity.ToTable("OrderPositions", "Billing");

                entity.HasIndex(e => new { e.OrderId, e.IsActive, e.IsDeleted })
                    .HasName("IX_OrderPositions_OrderId_IsActive_IsDeleted");

                entity.HasIndex(e => new { e.PricePositionId, e.IsActive, e.IsDeleted })
                    .HasName("IX_OrderPositions_PricePositionId_IsActive_IsDeleted");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.CreatedOn).HasColumnType("datetime2(2)");

                entity.Property(e => e.Extensions).IsRequired();

                entity.Property(e => e.IsActive).IsRequired().HasDefaultValueSql("1");

                entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("0");

                entity.Property(e => e.ModifiedOn).HasColumnType("datetime2(2)");

                entity.HasOne(d => d.Order)
                    .WithMany(p => p.OrderPositions)
                    .HasForeignKey(d => d.OrderId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_OrderPositions_Orders");

                entity.HasOne(d => d.PricePosition)
                    .WithMany(p => p.OrderPositions)
                    .HasForeignKey(d => d.PricePositionId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_OrderPositions_PricePositions");
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.ToTable("Orders", "Billing");

                entity.HasIndex(e => e.ReplicationCode)
                    .HasName("IX_Orders_ReplicationCode");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.ApprovalDate).HasColumnType("datetime2(2)");

                entity.Property(e => e.BeginDistributionDate).HasColumnType("datetime2(2)");

                entity.Property(e => e.Comment).HasMaxLength(300);

                entity.Property(e => e.CreatedOn).HasColumnType("datetime2(2)");

                entity.Property(e => e.DocumentsComment).HasMaxLength(300);

                entity.Property(e => e.EndDistributionDateFact).HasColumnType("datetime2(2)");

                entity.Property(e => e.EndDistributionDatePlan).HasColumnType("datetime2(2)");

                entity.Property(e => e.HasDocumentsDebt).HasDefaultValueSql("1");

                entity.Property(e => e.IsActive).IsRequired().HasDefaultValueSql("1");

                entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("0");

                entity.Property(e => e.IsTerminated).IsRequired().HasDefaultValueSql("0");

                entity.Property(e => e.ModifiedOn).HasColumnType("datetime2(2)");

                entity.Property(e => e.Number)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.RejectionDate).HasColumnType("datetime2(2)");

                entity.Property(e => e.SignupDate).HasColumnType("datetime2(2)");
            });

            modelBuilder.Entity<Position>(entity =>
            {
                entity.ToTable("Positions", "Billing");

                entity.HasIndex(e => e.Name)
                    .HasName("IX_Positions_Name");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.IsComposite).HasDefaultValueSql("0");

                entity.Property(e => e.IsContentSales).IsRequired();

                entity.Property(e => e.IsDeleted).HasDefaultValueSql("0");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(256);

                entity.HasOne(d => d.AdvertisementTemplate)
                    .WithMany(p => p.Positions)
                    .HasForeignKey(d => d.AdvertisementTemplateId)
                    .HasConstraintName("FK_Positions_AdvertisementTemplates");
            });

            modelBuilder.Entity<PricePosition>(entity =>
            {
                entity.ToTable("PricePositions", "Billing");

                entity.HasIndex(e => e.PriceId)
                    .HasName("IX_FK_Price_Position_Price");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.Cost).HasColumnType("decimal");

                entity.Property(e => e.CreatedOn).HasColumnType("datetime2(2)");

                entity.Property(e => e.IsActive).HasDefaultValueSql("1");

                entity.Property(e => e.IsDeleted).HasDefaultValueSql("0");

                entity.Property(e => e.ModifiedOn).HasColumnType("datetime2(2)");

                entity.HasOne(d => d.Position)
                    .WithMany(p => p.PricePositions)
                    .HasForeignKey(d => d.PositionId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_Price_Position_Position");

                entity.HasOne(d => d.Price)
                    .WithMany(p => p.PricePositions)
                    .HasForeignKey(d => d.PriceId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_Price_Position_Price");
            });

            modelBuilder.Entity<Price>(entity =>
            {
                entity.ToTable("Prices", "Billing");

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.BeginDate).HasColumnType("datetime2(2)");

                entity.Property(e => e.CreateDate).HasColumnType("datetime2(2)");

                entity.Property(e => e.CreatedOn).HasColumnType("datetime2(2)");

                entity.Property(e => e.IsActive).HasDefaultValueSql("1");

                entity.Property(e => e.IsDeleted).HasDefaultValueSql("0");

                entity.Property(e => e.IsPublished).HasDefaultValueSql("0");

                entity.Property(e => e.ModifiedOn).HasColumnType("datetime2(2)");

                entity.Property(e => e.PublishDate).HasColumnType("datetime2(2)");
            });
        }
    }
}