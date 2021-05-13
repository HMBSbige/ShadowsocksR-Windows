using Shadowsocks.Enums;
using Shadowsocks.ViewModel;
using System;

namespace Shadowsocks.Model
{
    [Serializable]
    public class PortMapConfig : ViewModelBase
    {
        #region private

        private bool _enable;
        private PortMapType _type;
        private string _id;
        private string _serverAddr;
        private int _serverPort;
        private string _remarks;

        #endregion

        #region public

        public bool Enable
        {
            get => _enable;
            set => SetField(ref _enable, value);
        }

        public PortMapType Type
        {
            get => _type;
            set => SetField(ref _type, value);
        }

        public string Id
        {
            get => _id;
            set => SetField(ref _id, value);
        }

        public string Server_addr
        {
            get => _serverAddr;
            set => SetField(ref _serverAddr, value);
        }

        public int Server_port
        {
            get => _serverPort;
            set => SetField(ref _serverPort, value);
        }

        public string Remarks
        {
            get => _remarks;
            set => SetField(ref _remarks, value);
        }

        #endregion
    }
}
