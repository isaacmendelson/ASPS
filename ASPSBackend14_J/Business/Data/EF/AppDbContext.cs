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
    public DbSet<Roadmap> Roadmaps { get; set; }
    public DbSet<ImmediateDanger> ImmediateDangers { get; set; }

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
                .HasValue<TrackUrlAlertEntity>("TrackUrl")
                .HasValue<TabClosedAlertEntity>("TabClosed");
            
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
            // Pin the column name so the TPH share with TabClosedAlertEntity.TabId
            // resolves to the same physical column. Without this, EF synthesises
            // `WebAlertEntity_TabId` on parallel siblings and orphans existing data.
            entity.Property(e => e.TabId).HasColumnName("TabId").HasMaxLength(100);
        });

        // UrlAlertEntity specific configuration
        modelBuilder.Entity<UrlAlertEntity>(entity =>
        {
            entity.Property(e => e.TrackerKeys).HasColumnType("TEXT");
            entity.Property(e => e.IFrameDomains).HasColumnType("TEXT");
        });

        // TabClosedAlertEntity specific configuration —
        //   * TabId is shared with WebAlertEntity.TabId via TPH; aliased to the
        //     same column so EF doesn't synthesise a `WebAlertEntity_TabId`
        //     parallel column (which would orphan existing UrlAlert.TabId data).
        //   * Url is mapped to a NEW column ("TabClosedUrl") because
        //     WebAlertEntity already owns "Url" and EF can't share a column
        //     between siblings at different inheritance levels.
        modelBuilder.Entity<TabClosedAlertEntity>(entity =>
        {
            entity.Property(e => e.TabId)
                .HasColumnName("TabId")
                .HasMaxLength(100);
            entity.Property(e => e.Url)
                .HasColumnName("TabClosedUrl")
                .HasColumnType("TEXT");
        });

        // TrackUrlAlertEntity specific configuration
        modelBuilder.Entity<TrackUrlAlertEntity>(entity =>
        {
            entity.Property(e => e.FromUrl).HasColumnType("TEXT");
            entity.Property(e => e.Duration);
            entity.Property(e => e.ScamInProgressKey).HasMaxLength(255);
            entity.Property(e => e.Timezone).HasMaxLength(100);
        });

        // ImmediateDanger configuration (TPH)
        modelBuilder.Entity<ImmediateDanger>(entity =>
        {
            entity.ToTable("ImmediateDangers");

            entity.HasDiscriminator<string>("Discriminator")
                .HasValue<ImmediateDangerByRemoteAccess>("RemoteAccess");

            entity.HasKey(e => e.KeyField);
            entity.Property(e => e.KeyField)
                .HasColumnName("Key")
                .HasColumnType("varchar(36)");

            entity.Property(e => e.UserKeyField)
                .HasColumnName("UserKey")
                .HasColumnType("varchar(36)")
                .IsRequired();

            entity.Property(e => e.DeviceKeyField)
                .HasColumnName("DeviceKey")
                .HasColumnType("varchar(36)")
                .IsRequired(false);

            entity.Property(e => e.DeviceAlertKeyField)
                .HasColumnName("DeviceAlertKey")
                .HasColumnType("varchar(36)")
                .IsRequired(false);

            entity.Property(e => e.ScamInProgressKeyField)
                .HasColumnName("ScamInProgressKey")
                .HasColumnType("varchar(36)")
                .IsRequired(false);

            entity.Property(e => e.DeviceUid).HasMaxLength(255).IsRequired();

            // ProtectiveActions is [NotMapped] — backed by ProtectiveActionsJson string column
            entity.Property(e => e.ProtectiveActionsJson).HasColumnType("TEXT");

            // FK to User (required)
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserKeyField)
                .OnDelete(DeleteBehavior.Cascade);

            // Ignore computed/navigation properties not persisted
            entity.Ignore(e => e.Tag);
            entity.Ignore(e => e.TypeName);
            entity.Ignore(e => e.Typename);
            entity.Ignore(e => e.Key);
            entity.Ignore(e => e.Device);
            entity.Ignore(e => e.DeviceAlert);

            entity.HasIndex(e => e.UserKeyField);
            entity.HasIndex(e => e.DeviceUid);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.DeviceAlertKeyField);
            entity.HasIndex(e => e.ScamInProgressKeyField);
        });

        // ImmediateDangerByRemoteAccess specific configuration
        modelBuilder.Entity<ImmediateDangerByRemoteAccess>(entity =>
        {
            entity.Property(e => e.SensitiveUrl).HasColumnType("TEXT");
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
        // Roadmap configuration (JSON-blob design — see Common/Entities/Roadmap.cs)
        modelBuilder.Entity<Roadmap>(entity =>
        {
            entity.ToTable("Roadmaps");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasColumnName("Key")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Description)
                .HasMaxLength(500);

            entity.Property(e => e.Data)
                .IsRequired()
                .HasColumnType("longtext");

            entity.Property(e => e.Version)
                .IsRequired()
                .HasDefaultValue(1)
                .IsConcurrencyToken();

            entity.Property(e => e.DateCreated)
                .IsRequired();

            entity.Property(e => e.LastUpdatedAt);

            entity.Property(e => e.CreatedBy)
                .HasMaxLength(255);

            entity.Property(e => e.LastUpdatedBy)
                .HasMaxLength(255);

            entity.Property(e => e.IsArchived)
                .IsRequired()
                .HasDefaultValue(false);

            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.IsArchived);
        });

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

            // Ignore computed properties only
            entity.Ignore(e => e.Tag);
            entity.Ignore(e => e.TypeName);
            entity.Ignore(e => e.Key);
            entity.Ignore(e => e.IsDeleted);
            entity.Ignore(e => e.IsDisabled);
            // IsEnabled does not exist

            // Unique index on Name
            entity.HasIndex(e => e.Name)
                .IsUnique();

            // Index on ParentId for performance
            entity.HasIndex(e => e.ParentId);

            // Index on DateDeleted for soft-delete queries
            entity.HasIndex(e => e.DateDeleted);

        });
    }
}
