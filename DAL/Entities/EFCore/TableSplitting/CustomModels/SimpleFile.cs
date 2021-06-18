using DAL.Entities.EFCore.TableSplitting.Models;

namespace DAL.Entities.EFCore.TableSplitting.CustomModels
{
    public partial class SimpleFile
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ContentType { get; set; }
        public long? Size { get; set; }

        public virtual File FileDetails { get; set; }
    }
}
