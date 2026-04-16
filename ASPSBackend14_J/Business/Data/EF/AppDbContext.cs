using Common.Entities;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using DeviceAlertEntity = Common.Entities.DeviceAlertEntity;

namespace Business.Data.EF;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        try
        {

            // Force model initialization to happen now so we can catch the error
            var _ = this.Model;

        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DbContext] FATAL ERROR during model initialization:");
            Console.WriteLine($"  Type: {ex.GetType().Name}");
            Console.WriteLine($"  Message: {ex.Message}");
            Console.WriteLine($"  Stack: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Inner: {ex.InnerException.Message}");
            }
            throw;
        }
    }

    public DbSet<User> Users { get; set; }
    public DbSet<UserDevice> UserDevices { get; set; }
    public DbSet<UserAccount> UserAccounts { get; set; }
    public DbSet<AnalysisResultContainer> AnalysisResults { get; set; }
    public DbSet<AlertFlag> AlertFlags { get; set; }
    public DbSet<DeviceAlertEntity> DeviceAlerts { get; set; }
    public DbSet<KnownPhishingWebsite> KnownPhishingWebsites { get; set; }
    public DbSet<SafeDomain> SafeDomains { get; set; }
    public DbSet<TrackedDomain> TrackedDomains { get; set; }
    public DbSet<SensitiveSite> SensitiveSites { get; set; }
    public DbSet<DeviceTokenEntity> DeviceTokens { get; set; }
    public DbSet<Simulation> Simulations { get; set; }
    public DbSet<BlacklistedPhoneNumber> BlacklistedPhoneNumbers { get; set; }
    public DbSet<BankWebsite> BankWebsites { get; set; }
    public DbSet<WebsiteCategory> WebsiteCategories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.KeyField);
            entity.Property(e => e.KeyField)
                .HasColumnName("Key")
                .HasColumnType("varchar(36)")
                .IsRequired();
            
            // Explicitly configure all mapped properties
            entity.Property(e => e.KeycloakUserId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.FirstName).IsRequired();
            entity.Property(e => e.LastName).IsRequired();
            entity.Property(e => e.Address).IsRequired();
            entity.Property(e => e.City).IsRequired();
            entity.Property(e => e.State).IsRequired();
            entity.Property(e => e.Zip).IsRequired();
            entity.Property(e => e.Country).IsRequired();
            entity.Property(e => e.PhoneNumber).IsRequired();
            entity.Property(e => e.Email).IsRequired();
            entity.Property(e => e.Role).IsRequired();
            entity.Property(e => e.DateCreated).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired();
            entity.Property(e => e.IsDisabled).IsRequired();
            
            entity.HasIndex(e => e.KeycloakUserId).IsUnique();
            
            // Ignore computed properties
            entity.Ignore(e => e.Tag);
            entity.Ignore(e => e.TypeName);
            entity.Ignore(e => e.Key);
        });

        // UserDevice configuration (TPH - Table Per Hierarchy)
        modelBuilder.Entity<UserDevice>(entity =>
        {
            entity.HasDiscriminator<string>("Discriminator")
                .HasValue<PersonalComputer>("PC")
                .HasValue<SmartPhone>("Phone");
            
            entity.HasKey(e => e.KeyField);
            entity.Property(e => e.KeyField)
                .HasColumnName("Key")
                .HasColumnType("varchar(36)");
            entity.Property(e => e.UserKeyField)
                .HasColumnName("UserKey")
                .HasColumnType("varchar(36)")
                .IsRequired(false);
            entity.Property(e => e.DeviceUid).HasMaxLength(255).IsRequired();
            entity.HasIndex(e => e.DeviceUid).IsUnique();
            
            // Ignore computed properties
            entity.Ignore(e => e.Tag);
            entity.Ignore(e => e.TypeName);
            entity.Ignore(e => e.Key);
            entity.Ignore(e => e.UserKey);
        });

        // PersonalComputer specific configuration
        modelBuilder.Entity<PersonalComputer>(entity =>
        {
            entity.Property(e => e.MotherboardSerial).HasMaxLength(255);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
        });

        // SmartPhone specific configuration
        modelBuilder.Entity<SmartPhone>(entity =>
        {
            entity.Property(e => e.PhoneNumber).HasMaxLength(50);
        });

        // UserAccount configuration
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.HasKey(e => e.KeyField);
            entity.Property(e => e.KeyField)
                .HasColumnName("Key")
                .HasColumnType("varchar(36)");
            entity.Property(e => e.UserKeyField)
                .HasColumnName("UserKey")
                .HasColumnType("varchar(36)")
                .IsRequired();
            entity.Property(e => e.LoginUrl).HasMaxLength(500);
            entity.Property(e => e.UserName).HasMaxLength(255);
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.Property(e => e.LoginPhoneNumber).HasMaxLength(50);
            
            // Ignore computed properties
            entity.Ignore(e => e.Tag);
            entity.Ignore(e => e.TypeName);
            entity.Ignore(e => e.Key);
            entity.Ignore(e => e.UserKey);
            
            entity.HasIndex(e => e.UserKeyField);
            entity.HasIndex(e => e.UserName);
        });

        // AnalysisResultContainer configuration (TPH)
        modelBuilder.Entity<AnalysisResultContainer>(entity =>
        {
            entity.HasKey(e => e.KeyField);
            entity.Property(e => e.KeyField)
                .HasColumnName("Key")
                .HasColumnType("varchar(36)");
            entity.Property(e => e.UserKeyField)
                .HasColumnName("UserKey")
                .HasColumnType("varchar(36)")
                .IsRequired();

            entity.HasDiscriminator(e => e.Discriminator)
                .HasValue<AnalysisResultContainer>("AnalysisResultContainer")
                .HasValue<UrlAnalysisResultContainer>("UrlAnalysisResult")
                .HasValue<TrackUrlAnalysisResultContainer>("TrackUrlAnalysisResult")
                .HasValue<RemoteAccessAnalysisResultContainer>("RemoteAccessAnalysisResult");

            entity.Property(e => e.Discriminator)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.JsonValue).HasColumnType("TEXT");
            entity.Property(e => e.ErrorMessage).HasColumnType("TEXT");

            // Ignore computed properties
            entity.Ignore(e => e.Tag);
            entity.Ignore(e => e.TypeName);
            entity.Ignore(e => e.Key);
            entity.Ignore(e => e.UserKey);

            entity.HasIndex(e => e.UserKeyField);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Discriminator);
        });

        // UrlAnalysisResultContainer specific columns
        modelBuilder.Entity<UrlAnalysisResultContainer>(entity =>
        {
            entity.Property(e => e.Url).HasColumnName("Url").HasColumnType("TEXT");
            entity.Property(e => e.Domain).HasColumnName("Domain").HasMaxLength(255);
        });

        // TrackUrlAnalysisResultContainer specific columns (share Url/Domain columns)
        modelBuilder.Entity<TrackUrlAnalysisResultContainer>(entity =>
        {
            entity.Property(e => e.Url).HasColumnName("Url").HasColumnType("TEXT");
            entity.Property(e => e.Domain).HasColumnName("Domain").HasMaxLength(255);
            entity.Property(e => e.FromUrl).HasColumnType("TEXT");
        });

        // RemoteAccessAnalysisResultContainer specific columns
        modelBuilder.Entity<RemoteAccessAnalysisResultContainer>(entity =>
        {
            entity.Property(e => e.RemoteAccessApp).HasDefaultValue(0);
            entity.Property(e => e.SessionStatus).HasDefaultValue(0);
        });

        // DeviceAlert configuration (TPH)
        modelBuilder.Entity<DeviceAlertEntity>(entity =>
        {
            entity.ToTable("DeviceAlerts");
            
            entity.HasDiscriminator<string>("Discriminator")
                .HasValue<RemoteAccessAlertEntity>("RemoteAccess")
                .HasValue<UrlAlertEntity>("Url")
                .HasValue<TrackUrlAlertEntity>("TrackUrl");
            
            entity.HasKey(e => e.KeyField);
            entity.Property(e => e.KeyField)
                .HasColumnName("Key")
                .HasColumnType("varchar(36)");
            
            // User foreign key
            entity.Property(e => e.UserKeyField)
                .HasColumnName("UserKey")
                .HasColumnType("varchar(36)")
                .IsRequired(false);
            
            // Device foreign key
            entity.Property(e => e.DeviceKeyField)
                .HasColumnName("DeviceKey")
                .HasColumnType("varchar(36)")
                .IsRequired(false);
            
            entity.Property(e => e.AlertType).HasMaxLength(100);
            entity.Property(e => e.Token).HasMaxLength(500);
            entity.Property(e => e.DeviceUid).HasMaxLength(255);
            entity.Property(e => e.MAC).HasMaxLength(50);
            entity.Property(e => e.IPAddress).HasMaxLength(50);
            
            // Configure foreign key relationships
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserKeyField)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.Device)
                .WithMany()
                .HasForeignKey(e => e.DeviceKeyField)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Ignore computed properties
            entity.Ignore(e => e.Tag);
            entity.Ignore(e => e.TypeName);
            entity.Ignore(e => e.Key);
            entity.Ignore(e => e.UserKey);
            entity.Ignore(e => e.DeviceKey);
            
            entity.HasIndex(e => e.DeviceUid);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.UserKeyField);
            entity.HasIndex(e => e.DeviceKeyField);
            entity.HasIndex(e => e.Priority);
        });

        // RemoteAccessAlertEntity specific configuration
        modelBuilder.Entity<RemoteAccessAlertEntity>(entity =>
        {
            entity.Property(e => e.ConnectionUrl).HasMaxLength(2000);
            entity.Property(e => e.RemoteOS).HasMaxLength(100);
            entity.Property(e => e.RemoteVersion).HasMaxLength(50);
            entity.Property(e => e.ConnectionType).HasMaxLength(50);
        });

        // WebAlertEntity shared configuration (base for UrlAlertEntity and TrackUrlAlertEntity)
        modelBuilder.Entity<WebAlertEntity>(entity =>
        {
            entity.Property(e => e.Url).HasColumnType("TEXT");
            entity.Property(e => e.UserAgent).HasColumnType("TEXT");
            entity.Property(e => e.TabId).HasMaxLength(100);
        });

        // UrlAlertEntity specific configuration
        modelBuilder.Entity<UrlAlertEntity>(entity =>
        {
            entity.Property(e => e.TrackerKeys).HasColumnType("TEXT");
            entity.Property(e => e.IFrameDomains).HasColumnType("TEXT");
        });

        // TrackUrlAlertEntity specific configuration
        modelBuilder.Entity<TrackUrlAlertEntity>(entity =>
        {
            entity.Property(e => e.FromUrl).HasColumnType("TEXT");
            entity.Property(e => e.Duration);
            entity.Property(e => e.ScamInProgressKey).HasMaxLength(255);
            entity.Property(e => e.Timezone).HasMaxLength(100);
        });

        // AlertFlag configuration (this entity doesn't inherit from Entity - uses int Key)
        modelBuilder.Entity<AlertFlag>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).ValueGeneratedOnAdd();
            entity.HasIndex(e => new { e.UserKey, e.Status });
        });

        // KnownPhishingWebsite configuration (INT AUTO_INCREMENT key)
        modelBuilder.Entity<KnownPhishingWebsite>(entity =>
        {
            entity.ToTable("KnownPhishingWebsites");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasColumnName("Key")
                .ValueGeneratedOnAdd();
            
            entity.Property(e => e.Url)
                .IsRequired()
                .HasColumnType("TEXT");
            
            entity.Property(e => e.Domain)
                .IsRequired()
                .HasMaxLength(255);
            
            entity.Property(e => e.Source)
                .HasMaxLength(100);
            
            entity.Property(e => e.DateCreated)
                .IsRequired();
            
            entity.Property(e => e.DateDeleted);
            
            // Indexes for performance
            entity.HasIndex(e => e.Domain);
            entity.HasIndex(e => e.DateDeleted);
            entity.HasIndex(e => e.Url).HasDatabaseName("idx_url");
        });

        // SafeDomain configuration (INT AUTO_INCREMENT key)
        modelBuilder.Entity<SafeDomain>(entity =>
        {
            entity.ToTable("SafeDomains");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasColumnName("Key")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Domain)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.DateCreated);

            entity.Property(e => e.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            entity.HasIndex(e => e.Domain);
        });

        // TrackedDomain configuration (INT AUTO_INCREMENT key)
        modelBuilder.Entity<TrackedDomain>(entity =>
        {
            entity.ToTable("TrackedDomains");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasColumnName("Key")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Domain)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Category)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.DateCreated)
                .IsRequired();

            entity.Property(e => e.DateModified)
                .IsRequired();

            entity.Property(e => e.DateDeleted);

            // Indexes for performance
            entity.HasIndex(e => e.Domain);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.DateDeleted);
            entity.HasIndex(e => e.IsActive);
        });

        // SensitiveSite configuration (INT AUTO_INCREMENT key)
        modelBuilder.Entity<SensitiveSite>(entity =>
        {
            entity.ToTable("SensitiveSites");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasColumnName("Key")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.DomainPattern)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Category)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.RiskMultiplier)
                .IsRequired()
                .HasColumnType("decimal(5,2)")
                .HasDefaultValue(2.0m);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.DateCreated)
                .IsRequired();

            entity.Property(e => e.DateModified)
                .IsRequired();

            entity.Property(e => e.DateDeleted);

            // Indexes for performance
            entity.HasIndex(e => e.DomainPattern);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.DateDeleted);
            entity.HasIndex(e => e.IsActive);
        });

        // DeviceToken configuration
        modelBuilder.Entity<DeviceTokenEntity>(entity =>
        {
            entity.ToTable("DeviceTokens");
            entity.HasKey(e => e.DeviceUid);
            entity.Property(e => e.DeviceUid).HasMaxLength(255);
            entity.Property(e => e.TokenValue).IsRequired().HasMaxLength(128);
            entity.Property(e => e.UserKeyField).IsRequired().HasMaxLength(36);
            entity.Property(e => e.DateCreated).IsRequired();
            entity.Property(e => e.Expiration).IsRequired();

            entity.HasIndex(e => e.TokenValue);
        });

        // Simulation configuration
        modelBuilder.Entity<Simulation>(entity =>
        {
            entity.ToTable("Simulations");
            entity.HasKey(e => e.KeyField);
            entity.Property(e => e.KeyField)
                .HasColumnName("Key")
                .HasColumnType("varchar(36)")
                .IsRequired();
            
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);
            
            entity.Property(e => e.Description)
                .HasMaxLength(1000);
            
            entity.Property(e => e.CreatorKeyField)
                .HasColumnName("CreatorKey")
                .HasColumnType("varchar(36)")
                .IsRequired();
            
            entity.Property(e => e.SimulationStepsJson)
                .HasColumnName("SimulationSteps")
                .HasColumnType("TEXT");
            
            entity.Property(e => e.DateCreated).IsRequired();
            entity.Property(e => e.DateModified);
            entity.Property(e => e.DateDeleted);
            entity.Property(e => e.IsDeleted).IsRequired();
            entity.Property(e => e.IsDisabled).IsRequired();
            
            // Foreign key relationship to Creator (User)
            entity.HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatorKeyField)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Ignore computed properties
            entity.Ignore(e => e.Tag);
            entity.Ignore(e => e.TypeName);
            entity.Ignore(e => e.Key);
            entity.Ignore(e => e.CreatorKey);
            
            // Indexes for performance
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.CreatorKeyField);
            entity.HasIndex(e => e.DateCreated);
        });

        // BlacklistedPhoneNumber configuration (INT AUTO_INCREMENT key)
        // JIRA: ASPS-282
        modelBuilder.Entity<BlacklistedPhoneNumber>(entity =>
        {
            entity.ToTable("BlacklistedPhoneNumbers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasColumnName("Key")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.PhoneNumber)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Source)
                .HasMaxLength(100);

            entity.Property(e => e.Notes)
                .HasMaxLength(500);

            entity.Property(e => e.DateCreated)
                .IsRequired();

            entity.Property(e => e.DateDeleted);

            entity.Property(e => e.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            // Indexes for performance
            entity.HasIndex(e => e.PhoneNumber);
            entity.HasIndex(e => e.DateDeleted);
        });

        // BankWebsite configuration (INT AUTO_INCREMENT key)
        // JIRA: ASPS-297
        modelBuilder.Entity<BankWebsite>(entity =>
        {
            entity.ToTable("BankWebsites");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasColumnName("Key")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Domain)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.BankName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Country)
                .HasMaxLength(100);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.DateCreated)
                .IsRequired();

            entity.Property(e => e.DateModified);

            entity.Property(e => e.DateDeleted);

            entity.Property(e => e.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            // Indexes for performance
            entity.HasIndex(e => e.Domain);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.DateDeleted);
        });

        // WebsiteCategory configuration
        // JIRA: SCRUM-819
        modelBuilder.Entity<WebsiteCategory>(entity =>
        {
            entity.ToTable("WebsiteCategories");
            entity.HasKey(e => e.KeyField);
            entity.Property(e => e.KeyField)
                .HasColumnName("Key")
                .HasColumnType("varchar(36)")
                .IsRequired();

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.ParentId)
                .HasColumnType("varchar(36)")
                .IsRequired(false);

            entity.Property(e => e.DateCreated)
                .IsRequired();

            entity.Property(e => e.DateDeleted);

            entity.Property(e => e.Source)
                .HasMaxLength(100);

            // Self-referencing Parent relationship
            entity.HasOne(e => e.Parent)
                .WithMany()
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Ignore computed properties
            entity.Ignore(e => e.Tag);
            entity.Ignore(e => e.TypeName);
            entity.Ignore(e => e.Key);

            // Unique index on Name
            entity.HasIndex(e => e.Name)
                .IsUnique();

            // Index on ParentId for performance
            entity.HasIndex(e => e.ParentId);

            // Index on DateDeleted for soft-delete queries
            entity.HasIndex(e => e.DateDeleted);

            // Seed data for WebsiteCategory
            // JIRA: SCRUM-819 Section 6
            entity.HasData(
                new { KeyField = "cat-banking", Name = "banking", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-credit_union", Name = "credit_union", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-insurance", Name = "insurance", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-investment", Name = "investment", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-stock_trading", Name = "stock_trading", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-crypto_exchange", Name = "crypto_exchange", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-payment_service", Name = "payment_service", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-lending", Name = "lending", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-ecommerce", Name = "ecommerce", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-marketplace", Name = "marketplace", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-auction", Name = "auction", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-classifieds", Name = "classifieds", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-grocery", Name = "grocery", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-fashion", Name = "fashion", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-electronics", Name = "electronics", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-government", Name = "government", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-municipality", Name = "municipality", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-military", Name = "military", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-court", Name = "court", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-tax_authority", Name = "tax_authority", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-public_service", Name = "public_service", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-hospital", Name = "hospital", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-clinic", Name = "clinic", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-pharmacy", Name = "pharmacy", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-telehealth", Name = "telehealth", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-mental_health", Name = "mental_health", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-university", Name = "university", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-school", Name = "school", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-online_course", Name = "online_course", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-elearning", Name = "elearning", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-streaming", Name = "streaming", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-gaming", Name = "gaming", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-gambling", Name = "gambling", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-sports_betting", Name = "sports_betting", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-adult_content", Name = "adult_content", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-news", Name = "news", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-blog", Name = "blog", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-forum", Name = "forum", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-social_network", Name = "social_network", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-messaging", Name = "messaging", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-legal", Name = "legal", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-accounting", Name = "accounting", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-real_estate", Name = "real_estate", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-travel", Name = "travel", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-job_board", Name = "job_board", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-saas", Name = "saas", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-cloud", Name = "cloud", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-web_hosting", Name = "web_hosting", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-vpn_proxy", Name = "vpn_proxy", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-developer_tools", Name = "developer_tools", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-restaurant", Name = "restaurant", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-automotive", Name = "automotive", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-pets", Name = "pets", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-nonprofit", Name = "nonprofit", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-language_learning", Name = "language_learning", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-review_directory", Name = "review_directory", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-ride_delivery", Name = "ride_delivery", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" },
                new { KeyField = "cat-religious", Name = "religious", ParentId = (string?)null, DateCreated = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), DateDeleted = (DateTime?)null, Source = "category_patterns.json" }
            );
        });
    }
}
