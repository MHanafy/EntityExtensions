using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EntityExtensions.SqlServer;

namespace EntityExtensions.Tests
{
    [TestClass]
    public class SelfJoinTests
    {
        [TestMethod]
        public void BulkUpdate_OneLevel_NoExceptions()
        {
            //arrange
            var context = new CompanyContext();

            var emps = new List<EmpNoId>
            {
                new EmpNoId {Id = 1, Name = "Emp02", ManagerId = 2},
                new EmpNoId{Id = 5, Name = "Emp03", ManagerId = 1},
                new EmpNoId {Id = 2, Name = "Emp01"}
            };

            //act
            context.BulkUpdate(emps, null, null);

            //cleanup
            context.BulkUpdate(null, null, emps);

            //assert
            //no errors are shown.
        }

        [TestMethod]
        public void BulkUpdate_ThreeLevels_NoExceptions()
        {
            //arrange
            var context = new CompanyContext();

            var emps = new List<EmpNoId>
            {
                new EmpNoId {Id = 1, Name = "Emp02", ManagerId = 20, Level1Id = 8},
                new EmpNoId {Id = 8, Name = "Emp02", ManagerId = 10, Level1Id = 50},
                new EmpNoId {Id = 10, Name = "Emp02", ManagerId = 20},
                new EmpNoId{Id = 50, Name = "Emp03", ManagerId = 10},
                new EmpNoId {Id = 20, Name = "Emp01"}
            };

            //act
            context.BulkUpdate(emps, null, null);

            //cleanup
            context.BulkUpdate(null, null, emps);

            //assert
            //no errors are shown.
        }
    }
}
