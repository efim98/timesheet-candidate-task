using Microsoft.EntityFrameworkCore;
using TimeSheetCandidateTask.Domain.Models;

namespace TimesheetCandidateTask.Api.Infrastructure;

public sealed class TimesheetDbContext : DbContext
{
    public TimesheetDbContext(DbContextOptions<TimesheetDbContext> options) : base(options)
    {
    }

    public DbSet<Timesheet> Timesheets => Set<Timesheet>();
    public DbSet<TimesheetLine> TimesheetLines => Set<TimesheetLine>();
    public DbSet<TimesheetDay> TimesheetDays => Set<TimesheetDay>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Timesheet>(entity =>
        {
            entity.Property(x => x.PeriodStart).HasColumnType("date");
            entity.HasIndex(x => new { x.RetailId, x.PeriodStart })
                .IsUnique()
                .HasDatabaseName("UX_Timesheets_RetailId_PeriodStart");
            entity.HasMany(x => x.Lines)
                .WithOne()
                .HasForeignKey(x => x.TimesheetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TimesheetLine>(entity =>
        {
            entity.HasIndex(x => new { x.TimesheetId, x.EmployeeId })
                .IsUnique()
                .HasDatabaseName("UX_TimesheetLines_TimesheetId_EmployeeId");
            entity.HasMany(x => x.Days)
                .WithOne()
                .HasForeignKey(x => x.TimesheetLineId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TimesheetDay>(entity =>
        {
            entity.HasIndex(x => new { x.TimesheetLineId, x.Date })
                .IsUnique()
                .HasDatabaseName("UX_TimesheetDays_LineId_Date");
            entity.Property(x => x.Date).HasColumnType("date");
            entity.Property(x => x.Hours).HasPrecision(5, 2);
        });
    }
}
