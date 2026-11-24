using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Models;

namespace ContosoDashboard.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<TaskItem> Tasks { get; set; } = null!;
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<TaskComment> TaskComments { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<ProjectMember> ProjectMembers { get; set; } = null!;
    public DbSet<Announcement> Announcements { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<DocumentTag> DocumentTags { get; set; } = null!;
    public DbSet<DocumentShare> DocumentShares { get; set; } = null!;
    public DbSet<DocumentShareRecipient> DocumentShareRecipients { get; set; } = null!;
    public DbSet<DocumentActivity> DocumentActivities { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User relationships
        modelBuilder.Entity<User>()
            .HasMany(u => u.AssignedTasks)
            .WithOne(t => t.AssignedUser)
            .HasForeignKey(t => t.AssignedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasMany(u => u.CreatedTasks)
            .WithOne(t => t.CreatedByUser)
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasMany(u => u.ManagedProjects)
            .WithOne(p => p.ProjectManager)
            .HasForeignKey(p => p.ProjectManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure indexes for performance
        modelBuilder.Entity<TaskItem>()
            .HasIndex(t => t.AssignedUserId);

        modelBuilder.Entity<TaskItem>()
            .HasIndex(t => t.Status);

        modelBuilder.Entity<TaskItem>()
            .HasIndex(t => t.DueDate);

        modelBuilder.Entity<Project>()
            .HasIndex(p => p.ProjectManagerId);

        modelBuilder.Entity<Project>()
            .HasIndex(p => p.Status);

        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.IsRead });
        modelBuilder.Entity<Notification>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(notification => notification.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Document>(entity =>
        {
            entity.Property(document => document.Category).HasConversion<string>().HasMaxLength(50);
            entity.HasIndex(document => document.StorageKey).IsUnique();
            entity.HasIndex(document => document.UploaderUserId);
            entity.HasIndex(document => new { document.ProjectId, document.UploadedUtc });
            entity.HasIndex(document => new { document.Category, document.UploadedUtc });
            entity.HasOne(document => document.UploaderUser).WithMany()
                .HasForeignKey(document => document.UploaderUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(document => document.Project).WithMany()
                .HasForeignKey(document => document.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(document => document.TaskItem).WithMany()
                .HasForeignKey(document => document.TaskId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DocumentTag>(entity =>
        {
            entity.HasIndex(tag => new { tag.DocumentId, tag.Tag }).IsUnique();
            entity.HasOne(tag => tag.Document).WithMany(document => document.Tags)
                .HasForeignKey(tag => tag.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentShare>(entity =>
        {
            entity.Property(share => share.ShareType).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(share => new { share.DocumentId, share.ShareType, share.TeamIdentifier });
            entity.HasOne(share => share.Document).WithMany(document => document.Shares)
                .HasForeignKey(share => share.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(share => share.SharedByUser).WithMany()
                .HasForeignKey(share => share.SharedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DocumentShareRecipient>(entity =>
        {
            entity.HasIndex(recipient => new { recipient.RecipientUserId, recipient.RevokedUtc });
            entity.HasIndex(recipient => new { recipient.DocumentShareId, recipient.RecipientUserId }).IsUnique();
            entity.HasOne(recipient => recipient.Share).WithMany(share => share.Recipients)
                .HasForeignKey(recipient => recipient.DocumentShareId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(recipient => recipient.RecipientUser).WithMany()
                .HasForeignKey(recipient => recipient.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(recipient => recipient.RevokedByUser).WithMany()
                .HasForeignKey(recipient => recipient.RevokedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DocumentActivity>(entity =>
        {
            entity.Property(activity => activity.ActivityType).HasConversion<string>().HasMaxLength(30);
            entity.HasIndex(activity => activity.ActivityUtc);
            entity.HasIndex(activity => new { activity.DocumentId, activity.ActivityUtc });
            entity.HasIndex(activity => new { activity.ActorUserId, activity.ActivityUtc });
            entity.HasOne(activity => activity.Document).WithMany(document => document.Activities)
                .HasForeignKey(activity => activity.DocumentId).OnDelete(DeleteBehavior.SetNull);
        });

        // Seed initial data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Seed an admin user
        modelBuilder.Entity<User>().HasData(
            new User
            {
                UserId = 1,
                Email = "admin@contoso.com",
                DisplayName = "System Administrator",
                Department = "IT",
                JobTitle = "Administrator",
                Role = UserRole.Administrator,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedDate = DateTime.UtcNow,
                EmailNotificationsEnabled = true,
                InAppNotificationsEnabled = true
            },
            new User
            {
                UserId = 2,
                Email = "camille.nicole@contoso.com",
                DisplayName = "Camille Nicole",
                Department = "Engineering",
                JobTitle = "Project Manager",
                Role = UserRole.ProjectManager,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedDate = DateTime.UtcNow,
                EmailNotificationsEnabled = true,
                InAppNotificationsEnabled = true
            },
            new User
            {
                UserId = 3,
                Email = "floris.kregel@contoso.com",
                DisplayName = "Floris Kregel",
                Department = "Engineering",
                JobTitle = "Team Lead",
                Role = UserRole.TeamLead,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedDate = DateTime.UtcNow,
                EmailNotificationsEnabled = true,
                InAppNotificationsEnabled = true
            },
            new User
            {
                UserId = 4,
                Email = "ni.kang@contoso.com",
                DisplayName = "Ni Kang",
                Department = "Engineering",
                JobTitle = "Software Engineer",
                Role = UserRole.Employee,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedDate = DateTime.UtcNow,
                EmailNotificationsEnabled = true,
                InAppNotificationsEnabled = true
            }
        );

        // Seed a sample project
        modelBuilder.Entity<Project>().HasData(
            new Project
            {
                ProjectId = 1,
                Name = "ContosoDashboard Development",
                Description = "Internal employee productivity dashboard",
                ProjectManagerId = 2,
                StartDate = DateTime.UtcNow.AddDays(-30),
                TargetCompletionDate = DateTime.UtcNow.AddDays(60),
                Status = ProjectStatus.Active,
                CreatedDate = DateTime.UtcNow.AddDays(-30),
                UpdatedDate = DateTime.UtcNow
            }
        );

        // Seed sample tasks
        modelBuilder.Entity<TaskItem>().HasData(
            new TaskItem
            {
                TaskId = 1,
                Title = "Design database schema",
                Description = "Create entity relationship diagram and database design",
                Priority = TaskPriority.High,
                Status = Models.TaskStatus.Completed,
                DueDate = DateTime.UtcNow.AddDays(-20),
                AssignedUserId = 4,
                CreatedByUserId = 2,
                ProjectId = 1,
                CreatedDate = DateTime.UtcNow.AddDays(-30),
                UpdatedDate = DateTime.UtcNow.AddDays(-20)
            },
            new TaskItem
            {
                TaskId = 2,
                Title = "Implement authentication",
                Description = "Set up Microsoft Entra ID authentication",
                Priority = TaskPriority.Critical,
                Status = Models.TaskStatus.InProgress,
                DueDate = DateTime.UtcNow.AddDays(5),
                AssignedUserId = 4,
                CreatedByUserId = 2,
                ProjectId = 1,
                CreatedDate = DateTime.UtcNow.AddDays(-25),
                UpdatedDate = DateTime.UtcNow
            },
            new TaskItem
            {
                TaskId = 3,
                Title = "Create UI mockups",
                Description = "Design user interface mockups for all main pages",
                Priority = TaskPriority.Medium,
                Status = Models.TaskStatus.NotStarted,
                DueDate = DateTime.UtcNow.AddDays(10),
                AssignedUserId = 4,
                CreatedByUserId = 2,
                ProjectId = 1,
                CreatedDate = DateTime.UtcNow.AddDays(-20),
                UpdatedDate = DateTime.UtcNow.AddDays(-20)
            }
        );

        // Seed project members
        modelBuilder.Entity<ProjectMember>().HasData(
            new ProjectMember
            {
                ProjectMemberId = 1,
                ProjectId = 1,
                UserId = 3,
                Role = "TeamLead",
                AssignedDate = DateTime.UtcNow.AddDays(-30)
            },
            new ProjectMember
            {
                ProjectMemberId = 2,
                ProjectId = 1,
                UserId = 4,
                Role = "Developer",
                AssignedDate = DateTime.UtcNow.AddDays(-30)
            }
        );

        // Seed announcement
        modelBuilder.Entity<Announcement>().HasData(
            new Announcement
            {
                AnnouncementId = 1,
                Title = "Welcome to ContosoDashboard",
                Content = "Welcome to the new ContosoDashboard application. This platform will help you manage your tasks and projects more efficiently.",
                CreatedByUserId = 1,
                PublishDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(30),
                IsActive = true
            }
        );
    }
}
