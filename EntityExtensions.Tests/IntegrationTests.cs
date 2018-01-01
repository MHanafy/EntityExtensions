using System.Collections.Generic;
using System.Linq;
using EntityExtensions.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EntityExtensions.Tests
{
    [TestClass]
    public class IntegrationTests
    {

        [TestMethod]
        public void BulkInsert_EfSingleIdentity_RefreshsIdentities()
        {
            using (var context = new CompanyContext())
            {
                //Arrange

                //Clear existing data
                var emps = context.Employees.ToList();
                context.BulkUpdate(null, null, emps);

                emps = new List<Employee>
                {
                    new Employee {Name = "Emp 01"},
                    new Employee {Name = "Emp 02"},
                    new Employee {Name = "Emp 03"}
                };
                context.Employees.AddRange(emps);

                //Act
                context.BulkUpdate(emps);

                //Assert
                //All ids should be updated to none zero values.
                Assert.AreEqual(0, emps.Count(x=>x.Id == 0));
            }
        }

        [TestMethod]
        public void Test()
        {
            using (var context = new CompanyContext())
            {
                var emps = context.Employees.ToList();
                context.BulkUpdate(null, null, emps);

                var emp1 = new Employee { Name = "Emp 01" };
                var emp2 = new Employee { Name = "Emp 02" };
                var emp3 = new Employee { Name = "Emp 03" };
                var emp4 = new Employee { Name = "Emp 04" };
                var emp5 = new Employee { Name = "Emp 05" };

                emp2.Manager = emp1;
                emp3.Manager = emp2;
                emp4.Manager = emp3;
                emp5.Manager = emp4;


                emps = new List<Employee> { emp1, emp2, emp3, emp4, emp5 };
                context.Employees.AddRange(emps);

                context.BulkUpdate(emps);

            }
        }
    }
}
