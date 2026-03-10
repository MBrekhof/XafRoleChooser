namespace XafRoleChooser.Playwright.Infrastructure;

public static class TestConstants
{
    public const string BaseUrl = "https://localhost:5001";
    public const string AdminUser = "Admin";
    public const string MultiRoleUser = "MultiRole";
    public const string RegularUser = "User";
    public const string EmptyPassword = "";
    public const int DefaultTimeout = 30000;

    // Role names (as shown in the role chooser popup)
    public const string RoleAdministrators = "Administrators";
    public const string RoleHRManager = "HR Manager";
    public const string RoleProjectManager = "Project Manager";
    public const string RoleSales = "Sales";
    public const string RoleFinance = "Finance";

    // Navigation groups (text visible in sidebar)
    public const string NavCompany = "Company";
    public const string NavHR = "HR";
    public const string NavProjects = "Projects";
    public const string NavSales = "Sales";
    public const string NavFinance = "Finance";
}
