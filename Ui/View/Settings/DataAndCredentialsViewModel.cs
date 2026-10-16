using _1RM.Utils;
using _1RM.View.Settings.CredentialVault;
using _1RM.View.Settings.DataSource;

namespace _1RM.View.Settings;

public class DataAndCredentialsViewModel : NotifyPropertyChangedBaseScreen
{
    public DataSourceViewModel DataSources => IoC.Get<DataSourceViewModel>();
    public CredentialVaultViewModel Credentials => IoC.Get<CredentialVaultViewModel>();
}
