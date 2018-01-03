using System;
using System.Collections.Generic;
using System.Linq;
using EntityExtensions.Common;
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

                var emps = new List<Employee>
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
                Assert.AreEqual(0, emps.Count(x => x.Id == 0));

                //Clean up
                try
                {
                    context.BulkUpdate(null, null, emps);
                }
                catch
                {
                    //ignore any deletion related errors, since they're out of current test scope.
                }
            }
        }

        [TestMethod]
        public void BulkInsert_EfComputedDates_RefreshsDates()
        {
            using (var context = new CompanyContext())
            {
                //Arrange
                var date = DateTime.Now.AddDays(1);

                var emps = new List<Employee>
                {
                    new Employee {Name = "Emp 01", CreatedDate = date, UpdatedDate = date},
                    new Employee {Name = "Emp 02", CreatedDate = date, UpdatedDate = date},
                    new Employee {Name = "Emp 03", CreatedDate = date, UpdatedDate = date}
                };
                context.Employees.AddRange(emps);

                //Act
                context.BulkUpdate(emps);

                //Assert
                //All created/updated dates should be refreshed to DB dates.
                Assert.AreEqual(0, emps.Count(x => x.UpdatedDate == date || x.CreatedDate == date));

                //Clean up
                try
                {
                    context.BulkUpdate(null, null, emps);
                }
                catch
                {
                    //ignore any deletion related errors, since they're out of current test scope.
                }
            }
        }

        [TestMethod]
        public void BulkInsert_ManualCombinedInsertsAndUpdates_DoesNotRefreshData()
        {
            using (var context = new CompanyContext())
            {
                //Arrange
                var date = DateTime.Now.AddDays(1);

                var emps = new List<Employee>
                {
                    new Employee {Name = "Emp 01", CreatedDate = date, UpdatedDate = date},
                    new Employee {Name = "Emp 02", CreatedDate = date, UpdatedDate = date},
                    new Employee {Name = "Emp 03", CreatedDate = date, UpdatedDate = date}
                };

                //Act
#pragma warning disable 618
                context.BulkUpdate(emps, null);
#pragma warning restore 618

                //Assert
                //Updated dates won't be refreshed when using the deprecated overload.
                Assert.AreNotEqual(0, emps.Count(x => x.Id == 0));

                //Clean up
                try
                {
                    context.BulkUpdate(null, null, emps);
                }
                catch
                {
                    //ignore any deletion related errors, since they're out of current test scope.
                }
            }
        }

        [TestMethod]
        public void BulkInsert_ManualSingleIdentityRefreshAll_RefreshsIdentityAndCalculatedColumns()
        {
            using (var context = new CompanyContext())
            {
                //Arrange
                var date = DateTime.Now.AddDays(1);

                var emps = new List<Employee>
                {
                    new Employee {Name = "Emp 01", CreatedDate = date, UpdatedDate = date},
                    new Employee {Name = "Emp 02", CreatedDate = date, UpdatedDate = date},
                    new Employee {Name = "Emp 03", CreatedDate = date, UpdatedDate = date}
                };

                //Act
                context.BulkUpdate(emps, null, null, RefreshMode.All);

                //Assert
                //All identities are read from DB.
                Assert.AreEqual(0, emps.Count(x => x.Id == 0));

                //Created/Updated dates are frefreshed from DB
                Assert.AreEqual(0, emps.Count(x => x.CreatedDate == date || x.UpdatedDate == date));

                //Clean up
                try
                {
                    context.BulkUpdate(null, null, emps);
                }
                catch
                {
                    //ignore any deletion related errors, since they're out of current test scope.
                }
            }
        }

        [TestMethod]
        public void BulkInsert_ManualSingleIdentityRefreshIdentity_RefreshsIdentityOnly()
        {
            using (var context = new CompanyContext())
            {
                //Arrange
                var date = DateTime.Now.AddDays(1);

                var emps = new List<Employee>
                {
                    new Employee {Name = "Emp 01", CreatedDate = date, UpdatedDate = date},
                    new Employee {Name = "Emp 02", CreatedDate = date, UpdatedDate = date},
                    new Employee {Name = "Emp 03", CreatedDate = date, UpdatedDate = date}
                };

                //Act
                context.BulkUpdate(emps, null, null, RefreshMode.Identity);

                //Assert
                //All identities are read from DB.
                Assert.AreEqual(0, emps.Count(x => x.Id == 0));

                //Created/Updated dates aren't refreshed from DB
                Assert.AreEqual(3, emps.Count(x => x.CreatedDate == date || x.UpdatedDate == date));

                //Clean up
                try
                {
                    context.BulkUpdate(null, null, emps);
                }
                catch
                {
                    //ignore any deletion related errors, since they're out of current test scope.
                }
            }
        }

        [TestMethod]
        public void BulkInsert_ManualSingleIdentityRefreshNone_DoesNotRefreshAnything()
        {
            using (var context = new CompanyContext())
            {
                //Arrange
                var date = DateTime.Now.AddDays(1);

                var emps = new List<Employee>
                {
                    new Employee {Name = "Emp 01", CreatedDate = date, UpdatedDate = date},
                    new Employee {Name = "Emp 02", CreatedDate = date, UpdatedDate = date},
                    new Employee {Name = "Emp 03", CreatedDate = date, UpdatedDate = date}
                };

                //Act
                // ReSharper disable once RedundantArgumentDefaultValue
                context.BulkUpdate(emps, null, null, RefreshMode.None);

                //Assert
                //Nothing is refreshed from DB.
                Assert.AreEqual(3, emps.Count(x => x.Id == 0));
                Assert.AreEqual(3, emps.Count(x => x.CreatedDate == date || x.UpdatedDate == date));

                //Clean up
                try
                {
                    context.BulkUpdate(null, null, emps);
                }
                catch
                {
                    //ignore any deletion related errors, since they're out of current test scope.
                }
            }
        }

        [TestMethod]
        public void BulkUpdate_EfComputedDates_RefreshsDates()
        {
            using (var context = new CompanyContext())
            {
                //Arrange
                var date = DateTime.Now.AddDays(1);

                var emps = new List<Employee>
                {
                    new Employee {Name = "Emp 01", CreatedDate = date, UpdatedDate = date},
                    new Employee {Name = "Emp 02", CreatedDate = date, UpdatedDate = date},
                    new Employee {Name = "Emp 03", CreatedDate = date, UpdatedDate = date}
                };
                context.Employees.AddRange(emps);
                context.BulkUpdate(emps);

                //Update entities date.
                foreach (var emp in emps)
                {
                    emp.UpdatedDate = date;
                }
                context.BulkUpdate(emps);

                //Act

                //Assert
                //All updated dates should be refreshed to DB dates.
                Assert.AreEqual(0, emps.Count(x => x.UpdatedDate == date));

                //Clean up
                try
                {
                    context.BulkUpdate(null, null, emps);
                }
                catch
                {
                    //ignore any deletion related errors, since they're out of current test scope.
                }
            }
        }
    }
}
