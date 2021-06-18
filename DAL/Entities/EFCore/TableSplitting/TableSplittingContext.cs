using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using DAL.Entities.EFCore.TableSplitting.Models;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace DAL.Entities.EFCore.TableSplitting
{
    public partial class TableSplittingContext : DbContext
    {
        public TableSplittingContext()
        {
        }

        public TableSplittingContext(DbContextOptions<TableSplittingContext> options)
            : base(options)
        {
        }

        public virtual DbSet<File> File { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<File>(entity =>
            {
                entity.Property(e => e.ContentType)
                    .HasMaxLength(128)
                    .IsUnicode(false);

                entity.Property(e => e.Name)
                    .HasMaxLength(128)
                    .IsUnicode(false);

                entity.Property(e => e.Size).HasComputedColumnSql("(datalength([Data]))");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
