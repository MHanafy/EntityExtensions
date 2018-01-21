using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace EntityExtensions.Tests
{
    public class Employee
    {
        public Employee()
        {
            CreatedDate = DateTime.Now;
            UpdatedDate = DateTime.Now;
        }
        public int Id { get; set; }
        public string Name { get; set; }
        public int? ManagerId { get; set; }
        public virtual Employee Manager { get; set; }
        public int? Level1Id { get; set; }
        public int? Level2Id { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime CreatedDate { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime UpdatedDate { get; set; }
        
        public virtual Employee Level1 { get; set; }
        public virtual Employee Level2 { get; set; }
    }
}
