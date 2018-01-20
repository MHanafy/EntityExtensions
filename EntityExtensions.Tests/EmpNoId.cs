using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityExtensions.Tests
{
    public class EmpNoId
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public string Name { get; set; }
        public int? ManagerId { get; set; }
        public int? Level1Id { get; set; }
        public int? Level2Id { get; set; }

        public virtual Employee Manager { get; set; }
        public virtual Employee Level1 { get; set; }
        public virtual Employee Level2 { get; set; }
    }
}
