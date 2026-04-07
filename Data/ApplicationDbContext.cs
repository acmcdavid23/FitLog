using FitLog.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FitLog.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<WorkoutEntry> WorkoutEntries { get; set; }
        public DbSet<Exercise> Exercises { get; set; }
        public DbSet<NutritionLog> NutritionLogs { get; set; }
        public DbSet<Supplement> Supplements { get; set; }
        public DbSet<SupplementLog> SupplementLogs { get; set; }
        public DbSet<WaterLog> WaterLogs { get; set; }
        public DbSet<WorkoutSession> WorkoutSessions { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<SupplementLibraryItem> SupplementLibraryItems { get; set; }
        public DbSet<FriendRequest> FriendRequests { get; set; }
        public DbSet<FitLogGroup> Groups { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<WeightLog> WeightLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<WorkoutEntry>()
                .Property(w => w.WeightLbs)
                .HasPrecision(8, 2);

            builder.Entity<NutritionLog>()
                .Property(n => n.Protein)
                .HasPrecision(8, 2);

            builder.Entity<NutritionLog>()
                .Property(n => n.Carbs)
                .HasPrecision(8, 2);

            builder.Entity<NutritionLog>()
                .Property(n => n.Fat)
                .HasPrecision(8, 2);

            builder.Entity<WaterLog>()
                .Property(w => w.AmountOz)
                .HasPrecision(8, 2);

            builder.Entity<WaterLog>()
                .Property(w => w.DailyGoalOz)
                .HasPrecision(8, 2);

            builder.Entity<UserSettings>()
                .Property(u => u.CurrentWeight)
                .HasPrecision(8, 2);

            builder.Entity<UserSettings>()
                .Property(u => u.GoalWeight)
                .HasPrecision(8, 2);

            builder.Entity<UserSettings>()
                .Property(u => u.HeightInches)
                .HasPrecision(8, 2);

            builder.Entity<WeightLog>()
                .Property(w => w.WeightLbs)
                .HasPrecision(8, 2);
        }
    }
}