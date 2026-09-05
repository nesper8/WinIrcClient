using Microsoft.EntityFrameworkCore;
using WinIrcClient.Models;

namespace WinIrcClient.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Message> Messages { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Message>(eb =>
            {
                eb.HasKey(m => m.Id);
                eb.Property(m => m.Channel).IsRequired();
                eb.Property(m => m.Sender).IsRequired();
                eb.Property(m => m.Text).IsRequired();
            });

            modelBuilder.Entity<User>(eb =>
            {
                eb.HasKey(u => u.Id);
                eb.Property(u => u.Nick).IsRequired();
            });
        }
    }
}
