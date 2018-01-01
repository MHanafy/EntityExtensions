using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityExtensions.SqlServer;

namespace EntityExtensions.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var context = new CompanyContext())
            {
                var emps = context.Employees.ToList();
                context.BulkUpdate(null, emps);

                var emp1 = new Employee {Name = "Emp 01"};
                var emp2 = new Employee {Name = "Emp 02"};
                var emp3 = new Employee {Name = "Emp 03"};
                var emp4 = new Employee {Name = "Emp 04"};
                var emp5 = new Employee {Name = "Emp 05"};

                emp2.Manager = emp1;
                emp3.Manager = emp2;
                emp4.Manager = emp3;
                emp5.Manager = emp4;


                emps = new List<Employee>{emp1, emp2, emp3, emp4, emp5};
                context.Employees.AddRange(emps);

                context.BulkUpdate(emps);

                //var keys = context.GetDependentTypes(typeof(Employee));

                Console.WriteLine(context.Employees.Count());
                Console.ReadLine();
            }
        }
    }
}
