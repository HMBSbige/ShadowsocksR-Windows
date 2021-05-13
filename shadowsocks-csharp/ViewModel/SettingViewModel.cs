using Shadowsocks.Model;

namespace Shadowsocks.ViewModel
{
    public class SettingViewModel : ViewModelBase
    {
        public SettingViewModel()
        {
            _modifiedConfiguration = new Configuration();
        }

        private Configuration _modifiedConfiguration;

        public Configuration ModifiedConfiguration
        {
            get => _modifiedConfiguration;
            set => SetField(ref _modifiedConfiguration, value);
        }

        public void ReadConfig()
        {
            ModifiedConfiguration = Global.Load();
        }
    }
}
