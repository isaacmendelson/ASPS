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
    public DbSet<DeviceTokenEntity> DeviceTokens { get; set; }

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

        // AnalysisResultContainer configuration
        // NOTE: Discriminator is just a string field, not used for TPH inheritance
        // This allows any discriminator value without EF validation
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
            
            // Discriminator is just a regular string property
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
            entity.HasIndex(e => e.Discriminator); // Index for filtering by type
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
    }
}
