using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Security;
using DevExpress.Persistent.Base;
using RoleChooser.Security;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace XafRoleChooser.Module.BusinessObjects
{
    [DefaultClassOptions]
    [DefaultProperty(nameof(UserName))]
    public class ApplicationUser : RoleChooserUserBase, ISecurityUserWithLoginInfo, ISecurityUserLockout
    {
        [Browsable(false)]
        public virtual int AccessFailedCount { get; set; }

        [Browsable(false)]
        public virtual DateTime LockoutEnd { get; set; }

        [Browsable(false)]
        [NonCloneable]
        [DevExpress.ExpressApp.DC.Aggregated]
        public virtual IList<ApplicationUserLoginInfo> UserLogins { get; set; } = new ObservableCollection<ApplicationUserLoginInfo>();

        IEnumerable<ISecurityUserLoginInfo> IOAuthSecurityUser.UserLogins => UserLogins.OfType<ISecurityUserLoginInfo>();

        ISecurityUserLoginInfo ISecurityUserWithLoginInfo.CreateUserLoginInfo(string loginProviderName, string providerUserKey)
        {
            ApplicationUserLoginInfo result = ((IObjectSpaceLink)this).ObjectSpace.CreateObject<ApplicationUserLoginInfo>();
            result.LoginProviderName = loginProviderName;
            result.ProviderUserKey = providerUserKey;
            result.User = this;
            return result;
        }
    }
}
