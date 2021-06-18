using System;
using System.Collections.Generic;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace DAL.Entities.EFCore.TableSplitting.Models
{
    public partial class File
    {
        public byte[] Data { get; set; }
        public string Name { get; set; }
        public string ContentType { get; set; }
        public long? Size { get; set; }
        public int Id { get; set; }
    }
}
