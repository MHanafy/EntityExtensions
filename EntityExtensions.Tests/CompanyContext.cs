using System.Data.Entity;

namespace EntityExtensions.Tests
{
    class CompanyContext : DbContext
    {
        public CompanyContext(): base("Company") { }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<EmpNoId> EmpsNoIds { get; set; }
    }
}
