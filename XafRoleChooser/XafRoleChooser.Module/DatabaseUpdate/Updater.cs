using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Security;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Updating;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.EF;
using DevExpress.Persistent.BaseImpl.EF.PermissionPolicy;
using Microsoft.Extensions.DependencyInjection;
using XafRoleChooser.Module.BusinessObjects;

namespace XafRoleChooser.Module.DatabaseUpdate
{
    public class Updater : ModuleUpdater
    {
        public Updater(IObjectSpace objectSpace, Version currentDBVersion) :
            base(objectSpace, currentDBVersion)
        {
        }

        public override void UpdateDatabaseAfterUpdateSchema()
        {
            base.UpdateDatabaseAfterUpdateSchema();

#if !RELEASE
            var defaultRole = CreateDefaultRole();
            var adminRole = CreateAdminRole();
            var hrManagerRole = CreateHRManagerRole();
            var projectManagerRole = CreateProjectManagerRole();
            var salesRole = CreateSalesRole();
            var financeRole = CreateFinanceRole();

            ObjectSpace.CommitChanges();

            UserManager userManager = ObjectSpace.ServiceProvider.GetRequiredService<UserManager>();

            if (userManager.FindUserByName<ApplicationUser>(ObjectSpace, "User") == null)
            {
                string EmptyPassword = "";
                _ = userManager.CreateUser<ApplicationUser>(ObjectSpace, "User", EmptyPassword, (user) =>
                {
                    user.Roles.Add(defaultRole);
                });
            }

            if (userManager.FindUserByName<ApplicationUser>(ObjectSpace, "Admin") == null)
            {
                string EmptyPassword = "";
                _ = userManager.CreateUser<ApplicationUser>(ObjectSpace, "Admin", EmptyPassword, (user) =>
                {
                    user.Roles.Add(adminRole);
                    user.Roles.Add(hrManagerRole);
                    user.Roles.Add(projectManagerRole);
                    user.Roles.Add(salesRole);
                    user.Roles.Add(financeRole);
                });
            }

            if (userManager.FindUserByName<ApplicationUser>(ObjectSpace, "MultiRole") == null)
            {
                string EmptyPassword = "";
                _ = userManager.CreateUser<ApplicationUser>(ObjectSpace, "MultiRole", EmptyPassword, (user) =>
                {
                    user.Roles.Add(defaultRole);
                    user.Roles.Add(adminRole);
                    user.Roles.Add(hrManagerRole);
                    user.Roles.Add(projectManagerRole);
                    user.Roles.Add(salesRole);
                    user.Roles.Add(financeRole);
                });
            }

            ObjectSpace.CommitChanges();

            SeedSampleData();

            ObjectSpace.CommitChanges();
#endif
        }

        public override void UpdateDatabaseBeforeUpdateSchema()
        {
            base.UpdateDatabaseBeforeUpdateSchema();
        }

        #region Roles

        PermissionPolicyRole CreateAdminRole()
        {
            PermissionPolicyRole role = ObjectSpace.FirstOrDefault<PermissionPolicyRole>(r => r.Name == "Administrators");
            if (role == null)
            {
                role = ObjectSpace.CreateObject<PermissionPolicyRole>();
                role.Name = "Administrators";
                role.IsAdministrative = true;
            }
            return role;
        }

        PermissionPolicyRole CreateHRManagerRole()
        {
            PermissionPolicyRole role = ObjectSpace.FirstOrDefault<PermissionPolicyRole>(r => r.Name == "HR Manager");
            if (role == null)
            {
                role = ObjectSpace.CreateObject<PermissionPolicyRole>();
                role.Name = "HR Manager";

                // Full CRUD on Employees
                role.AddTypePermissionsRecursively<Employee>(SecurityOperations.FullAccess, SecurityPermissionState.Allow);
                // Read Companies
                role.AddTypePermissionsRecursively<Company>(SecurityOperations.Read, SecurityPermissionState.Allow);
                // Navigation
                role.AddNavigationPermission(@"Application/NavigationItems/Items/HR/Items/Employee_ListView", SecurityPermissionState.Allow);
                role.AddNavigationPermission(@"Application/NavigationItems/Items/Company/Items/Company_ListView", SecurityPermissionState.Allow);
            }
            return role;
        }

        PermissionPolicyRole CreateProjectManagerRole()
        {
            PermissionPolicyRole role = ObjectSpace.FirstOrDefault<PermissionPolicyRole>(r => r.Name == "Project Manager");
            if (role == null)
            {
                role = ObjectSpace.CreateObject<PermissionPolicyRole>();
                role.Name = "Project Manager";

                // Full CRUD on Projects
                role.AddTypePermissionsRecursively<Project>(SecurityOperations.FullAccess, SecurityPermissionState.Allow);
                // Read Companies, Employees (for manager lookups), Orders (project-linked)
                role.AddTypePermissionsRecursively<Company>(SecurityOperations.Read, SecurityPermissionState.Allow);
                role.AddTypePermissionsRecursively<Employee>(SecurityOperations.Read, SecurityPermissionState.Allow);
                role.AddTypePermissionsRecursively<Order>(SecurityOperations.Read, SecurityPermissionState.Allow);
                role.AddTypePermissionsRecursively<OrderLine>(SecurityOperations.Read, SecurityPermissionState.Allow);
                // Navigation
                role.AddNavigationPermission(@"Application/NavigationItems/Items/Projects/Items/Project_ListView", SecurityPermissionState.Allow);
                role.AddNavigationPermission(@"Application/NavigationItems/Items/Company/Items/Company_ListView", SecurityPermissionState.Allow);
                role.AddNavigationPermission(@"Application/NavigationItems/Items/Sales/Items/Order_ListView", SecurityPermissionState.Allow);
            }
            return role;
        }

        PermissionPolicyRole CreateSalesRole()
        {
            PermissionPolicyRole role = ObjectSpace.FirstOrDefault<PermissionPolicyRole>(r => r.Name == "Sales");
            if (role == null)
            {
                role = ObjectSpace.CreateObject<PermissionPolicyRole>();
                role.Name = "Sales";

                // Full CRUD on Orders and OrderLines
                role.AddTypePermissionsRecursively<Order>(SecurityOperations.FullAccess, SecurityPermissionState.Allow);
                role.AddTypePermissionsRecursively<OrderLine>(SecurityOperations.FullAccess, SecurityPermissionState.Allow);
                // Read Companies, Projects (for order linking)
                role.AddTypePermissionsRecursively<Company>(SecurityOperations.Read, SecurityPermissionState.Allow);
                role.AddTypePermissionsRecursively<Project>(SecurityOperations.Read, SecurityPermissionState.Allow);
                // Navigation
                role.AddNavigationPermission(@"Application/NavigationItems/Items/Sales/Items/Order_ListView", SecurityPermissionState.Allow);
                role.AddNavigationPermission(@"Application/NavigationItems/Items/Sales/Items/OrderLine_ListView", SecurityPermissionState.Allow);
                role.AddNavigationPermission(@"Application/NavigationItems/Items/Company/Items/Company_ListView", SecurityPermissionState.Allow);
                role.AddNavigationPermission(@"Application/NavigationItems/Items/Projects/Items/Project_ListView", SecurityPermissionState.Allow);
            }
            return role;
        }

        PermissionPolicyRole CreateFinanceRole()
        {
            PermissionPolicyRole role = ObjectSpace.FirstOrDefault<PermissionPolicyRole>(r => r.Name == "Finance");
            if (role == null)
            {
                role = ObjectSpace.CreateObject<PermissionPolicyRole>();
                role.Name = "Finance";

                // Full CRUD on Invoices
                role.AddTypePermissionsRecursively<Invoice>(SecurityOperations.FullAccess, SecurityPermissionState.Allow);
                // Read Orders, OrderLines, Companies
                role.AddTypePermissionsRecursively<Order>(SecurityOperations.Read, SecurityPermissionState.Allow);
                role.AddTypePermissionsRecursively<OrderLine>(SecurityOperations.Read, SecurityPermissionState.Allow);
                role.AddTypePermissionsRecursively<Company>(SecurityOperations.Read, SecurityPermissionState.Allow);
                // Navigation
                role.AddNavigationPermission(@"Application/NavigationItems/Items/Finance/Items/Invoice_ListView", SecurityPermissionState.Allow);
                role.AddNavigationPermission(@"Application/NavigationItems/Items/Sales/Items/Order_ListView", SecurityPermissionState.Allow);
                role.AddNavigationPermission(@"Application/NavigationItems/Items/Company/Items/Company_ListView", SecurityPermissionState.Allow);
            }
            return role;
        }

        PermissionPolicyRole CreateDefaultRole()
        {
            PermissionPolicyRole defaultRole = ObjectSpace.FirstOrDefault<PermissionPolicyRole>(role => role.Name == "Default");
            if (defaultRole == null)
            {
                defaultRole = ObjectSpace.CreateObject<PermissionPolicyRole>();
                defaultRole.Name = "Default";

                defaultRole.AddObjectPermissionFromLambda<ApplicationUser>(SecurityOperations.Read, cm => cm.ID == (Guid)CurrentUserIdOperator.CurrentUserId(), SecurityPermissionState.Allow);
                defaultRole.AddNavigationPermission(@"Application/NavigationItems/Items/Default/Items/MyDetails", SecurityPermissionState.Allow);
                defaultRole.AddMemberPermissionFromLambda<ApplicationUser>(SecurityOperations.Write, "ChangePasswordOnFirstLogon", cm => cm.ID == (Guid)CurrentUserIdOperator.CurrentUserId(), SecurityPermissionState.Allow);
                defaultRole.AddMemberPermissionFromLambda<ApplicationUser>(SecurityOperations.Write, "StoredPassword", cm => cm.ID == (Guid)CurrentUserIdOperator.CurrentUserId(), SecurityPermissionState.Allow);
                defaultRole.AddTypePermissionsRecursively<PermissionPolicyRole>(SecurityOperations.Read, SecurityPermissionState.Deny);
                defaultRole.AddObjectPermission<ModelDifference>(SecurityOperations.ReadWriteAccess, "UserId = ToStr(CurrentUserId())", SecurityPermissionState.Allow);
                defaultRole.AddObjectPermission<ModelDifferenceAspect>(SecurityOperations.ReadWriteAccess, "Owner.UserId = ToStr(CurrentUserId())", SecurityPermissionState.Allow);
                defaultRole.AddTypePermissionsRecursively<ModelDifference>(SecurityOperations.Create, SecurityPermissionState.Allow);
                defaultRole.AddTypePermissionsRecursively<ModelDifferenceAspect>(SecurityOperations.Create, SecurityPermissionState.Allow);
            }
            return defaultRole;
        }

        #endregion

        #region Seed Data

        void SeedSampleData()
        {
            if (ObjectSpace.FirstOrDefault<Company>(c => c.Name == "Contoso Ltd.") != null)
                return; // Already seeded

            // Companies
            var contoso = CreateCompany("Contoso Ltd.", "123 Main Street", "Seattle", "USA", "+1-206-555-0100", "info@contoso.com", "US-12-3456789");
            var northwind = CreateCompany("Northwind Traders", "456 Oak Avenue", "Portland", "USA", "+1-503-555-0200", "info@northwind.com", "US-98-7654321");
            var fabrikam = CreateCompany("Fabrikam Inc.", "789 Pine Road", "San Francisco", "USA", "+1-415-555-0300", "info@fabrikam.com", "US-55-1234567");

            // Employees - Contoso
            var alice = CreateEmployee("Alice", "Johnson", "alice@contoso.com", "+1-206-555-0101", "VP of Engineering", Department.Engineering, new DateTime(2020, 3, 15), 145000m, contoso);
            var bob = CreateEmployee("Bob", "Smith", "bob@contoso.com", "+1-206-555-0102", "Sales Director", Department.Sales, new DateTime(2019, 7, 1), 125000m, contoso);
            var carol = CreateEmployee("Carol", "Williams", "carol@contoso.com", "+1-206-555-0103", "HR Manager", Department.HumanResources, new DateTime(2021, 1, 10), 110000m, contoso);
            var dave = CreateEmployee("Dave", "Brown", "dave@contoso.com", "+1-206-555-0104", "CFO", Department.Finance, new DateTime(2018, 9, 1), 160000m, contoso);
            var eve = CreateEmployee("Eve", "Davis", "eve@contoso.com", "+1-206-555-0105", "Software Engineer", Department.Engineering, new DateTime(2022, 6, 15), 95000m, contoso);

            // Employees - Northwind
            var frank = CreateEmployee("Frank", "Miller", "frank@northwind.com", "+1-503-555-0201", "Operations Manager", Department.Operations, new DateTime(2020, 11, 1), 105000m, northwind);
            var grace = CreateEmployee("Grace", "Wilson", "grace@northwind.com", "+1-503-555-0202", "Account Executive", Department.Sales, new DateTime(2021, 4, 15), 85000m, northwind);

            // Employees - Fabrikam
            var hank = CreateEmployee("Hank", "Taylor", "hank@fabrikam.com", "+1-415-555-0301", "CTO", Department.Engineering, new DateTime(2019, 2, 1), 175000m, fabrikam);
            var iris = CreateEmployee("Iris", "Anderson", "iris@fabrikam.com", "+1-415-555-0302", "Project Manager", Department.Management, new DateTime(2020, 8, 15), 115000m, fabrikam);

            // Projects
            var webRedesign = CreateProject("Website Redesign", "Complete overhaul of the corporate website with modern UX.", ProjectStatus.InProgress, new DateTime(2025, 6, 1), null, 250000m, contoso, alice);
            var erpMigration = CreateProject("ERP Migration", "Migrate from legacy ERP to cloud-based solution.", ProjectStatus.Planning, new DateTime(2025, 9, 1), null, 500000m, contoso, alice);
            var mobileApp = CreateProject("Mobile App v2", "Second version of the customer-facing mobile app.", ProjectStatus.InProgress, new DateTime(2025, 3, 1), new DateTime(2025, 12, 31), 180000m, northwind, frank);
            var dataWarehouse = CreateProject("Data Warehouse", "Build centralized analytics data warehouse.", ProjectStatus.Completed, new DateTime(2024, 1, 15), new DateTime(2025, 4, 30), 320000m, fabrikam, hank);
            var securityAudit = CreateProject("Security Audit 2025", "Annual security audit and penetration testing.", ProjectStatus.OnHold, new DateTime(2025, 7, 1), null, 75000m, fabrikam, iris);

            // Orders
            var ord001 = CreateOrder("ORD-2025-001", new DateTime(2025, 1, 15), new DateTime(2025, 2, 1), OrderStatus.Delivered, contoso, webRedesign);
            AddOrderLine(ord001, "UX Design Services", 1, 45000m);
            AddOrderLine(ord001, "Frontend Development", 3, 12000m);

            var ord002 = CreateOrder("ORD-2025-002", new DateTime(2025, 2, 10), null, OrderStatus.Approved, contoso, erpMigration);
            AddOrderLine(ord002, "ERP Licenses (50 seats)", 50, 1200m);
            AddOrderLine(ord002, "Implementation Consulting", 1, 80000m);
            AddOrderLine(ord002, "Data Migration Tools", 1, 15000m);

            var ord003 = CreateOrder("ORD-2025-003", new DateTime(2025, 3, 5), new DateTime(2025, 3, 20), OrderStatus.Shipped, northwind, mobileApp);
            AddOrderLine(ord003, "Mobile Dev Framework License", 5, 3500m);
            AddOrderLine(ord003, "Cloud Hosting (Annual)", 1, 24000m);

            var ord004 = CreateOrder("ORD-2025-004", new DateTime(2025, 4, 1), null, OrderStatus.Draft, fabrikam, null);
            AddOrderLine(ord004, "Server Hardware", 4, 8500m);
            AddOrderLine(ord004, "Network Switches", 2, 3200m);

            var ord005 = CreateOrder("ORD-2025-005", new DateTime(2025, 5, 12), null, OrderStatus.Submitted, contoso, null);
            AddOrderLine(ord005, "Office Furniture Package", 20, 850m);

            var ord006 = CreateOrder("ORD-2025-006", new DateTime(2024, 11, 1), new DateTime(2024, 11, 15), OrderStatus.Delivered, fabrikam, dataWarehouse);
            AddOrderLine(ord006, "Database Licenses", 2, 45000m);
            AddOrderLine(ord006, "ETL Platform Subscription", 1, 36000m);

            // Invoices
            CreateInvoice("INV-2025-001", new DateTime(2025, 2, 5), new DateTime(2025, 3, 5), InvoiceStatus.Paid, 81000m, 17010m, contoso, ord001);
            CreateInvoice("INV-2025-002", new DateTime(2025, 3, 1), new DateTime(2025, 4, 1), InvoiceStatus.Sent, 155000m, 32550m, contoso, ord002);
            CreateInvoice("INV-2025-003", new DateTime(2025, 3, 25), new DateTime(2025, 4, 25), InvoiceStatus.Overdue, 41500m, 8715m, northwind, ord003);
            CreateInvoice("INV-2025-004", new DateTime(2024, 12, 1), new DateTime(2025, 1, 1), InvoiceStatus.Paid, 126000m, 26460m, fabrikam, ord006);
            CreateInvoice("INV-2025-005", new DateTime(2025, 5, 15), new DateTime(2025, 6, 15), InvoiceStatus.Draft, 17000m, 3570m, contoso, ord005);
        }

        Company CreateCompany(string name, string address, string city, string country, string phone, string email, string taxId)
        {
            var c = ObjectSpace.CreateObject<Company>();
            c.Name = name;
            c.Address = address;
            c.City = city;
            c.Country = country;
            c.Phone = phone;
            c.Email = email;
            c.TaxId = taxId;
            return c;
        }

        Employee CreateEmployee(string firstName, string lastName, string email, string phone, string jobTitle, Department dept, DateTime hireDate, decimal salary, Company company)
        {
            var e = ObjectSpace.CreateObject<Employee>();
            e.FirstName = firstName;
            e.LastName = lastName;
            e.Email = email;
            e.Phone = phone;
            e.JobTitle = jobTitle;
            e.Department = dept;
            e.HireDate = hireDate;
            e.Salary = salary;
            e.Company = company;
            return e;
        }

        Project CreateProject(string name, string desc, ProjectStatus status, DateTime startDate, DateTime? endDate, decimal budget, Company company, Employee manager)
        {
            var p = ObjectSpace.CreateObject<Project>();
            p.Name = name;
            p.Description = desc;
            p.Status = status;
            p.StartDate = startDate;
            p.EndDate = endDate;
            p.Budget = budget;
            p.Company = company;
            p.Manager = manager;
            return p;
        }

        Order CreateOrder(string orderNumber, DateTime orderDate, DateTime? shipDate, OrderStatus status, Company company, Project project)
        {
            var o = ObjectSpace.CreateObject<Order>();
            o.OrderNumber = orderNumber;
            o.OrderDate = orderDate;
            o.ShipDate = shipDate;
            o.Status = status;
            o.Company = company;
            o.Project = project;
            return o;
        }

        void AddOrderLine(Order order, string productName, int qty, decimal unitPrice)
        {
            var line = ObjectSpace.CreateObject<OrderLine>();
            line.ProductName = productName;
            line.Quantity = qty;
            line.UnitPrice = unitPrice;
            line.Order = order;
        }

        Invoice CreateInvoice(string invoiceNumber, DateTime invoiceDate, DateTime dueDate, InvoiceStatus status, decimal amount, decimal taxAmount, Company company, Order order)
        {
            var inv = ObjectSpace.CreateObject<Invoice>();
            inv.InvoiceNumber = invoiceNumber;
            inv.InvoiceDate = invoiceDate;
            inv.DueDate = dueDate;
            inv.Status = status;
            inv.Amount = amount;
            inv.TaxAmount = taxAmount;
            inv.Company = company;
            inv.Order = order;
            return inv;
        }

        #endregion
    }
}
