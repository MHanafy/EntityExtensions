using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityExtensions.Tests
{
    class CompanyContext : DbContext
    {
        public CompanyContext(): base("Company") { }
        public DbSet<Employee> Employees { get; set; }
    }
}
