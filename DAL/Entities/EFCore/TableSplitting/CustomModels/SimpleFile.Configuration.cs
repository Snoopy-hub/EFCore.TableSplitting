using DAL.Entities.EFCore.TableSplitting.Models;
using DAL.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Entities.EFCore.TableSplitting.CustomModels
{
    public partial class SimpleFile
    {
        private class Configuration : IEntityTypeConfiguration<SimpleFile>
        {
            /// <summary>
            /// This one is injected via <see cref="EFCoreHelpers.ApplyEntityTypeConfigurations{TContext}(ModelBuilder)"/>
            /// </summary>
            public ModelBuilder ModelBuilder { get; set; }

            public void Configure(EntityTypeBuilder<SimpleFile> builder)
            {
                //Not obvious, its recognized by convention
                builder.HasKey(e => e.Id);

                EFCoreHelpers.ConfigureTableSplitting(ModelBuilder.Entity<File>(), builder);
            }
        }
    }
}
