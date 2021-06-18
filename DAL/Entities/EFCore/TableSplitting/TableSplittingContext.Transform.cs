using DAL.Entities.EFCore.TableSplitting.CustomModels;
using DAL.Helpers;
using Microsoft.EntityFrameworkCore;

namespace DAL.Entities.EFCore.TableSplitting
{
    public partial class TableSplittingContext
    {
        public virtual DbSet<SimpleFile> SimpleFiles { get; set; }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            EFCoreHelpers.ApplyEntityTypeConfigurations<TableSplittingContext>(modelBuilder);
        }
    }
}
