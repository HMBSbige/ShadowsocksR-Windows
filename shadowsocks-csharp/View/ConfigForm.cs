using Shadowsocks.Controller;
using Shadowsocks.Encryption;
using Shadowsocks.Model;
using Shadowsocks.Properties;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Windows.Forms;
using ZXing.QrCode.Internal;

namespace Shadowsocks.View
{
    public sealed partial class ConfigForm : Form
    {
        private readonly ShadowsocksController _controller;
        private readonly UpdateChecker _updateChecker;

        // this is a copy of configuration that we are working on
        private Configuration _modifiedConfiguration;
        private int _oldSelectedIndex = -1;
        private bool _allowSave = true;
        private bool _ignoreLoad;
        private readonly string _oldSelectedId;

        private string _SelectedID;

        public ConfigForm(ShadowsocksController controller, UpdateChecker updateChecker, int focusIndex)
        {
            Font = SystemFonts.MessageBoxFont;
            InitializeComponent();
            lstServers.Font = CreateFont();

            nudServerPort.Minimum = IPEndPoint.MinPort;
            nudServerPort.Maximum = IPEndPoint.MaxPort;
            nudUdpPort.Minimum = IPEndPoint.MinPort;
            nudUdpPort.Maximum = IPEndPoint.MaxPort;

            Icon = Icon.FromHandle(Resources.ssw128.GetHicon());
            _controller = controller;
            _updateChecker = updateChecker;
            if (updateChecker.LatestVersionURL == null)
                llbUpdate.Visible = false;

            foreach (string name in EncryptorFactory.GetEncryptor().Keys)
            {
                EncryptorInfo info = EncryptorFactory.GetEncryptorInfo(name);
                if (info.display)
                    cmbEncryption.Items.Add(name);
            }
            UpdateTexts();
            controller.ConfigChanged += controller_ConfigChanged;

            LoadCurrentConfiguration();
            if (_modifiedConfiguration.index >= 0 && _modifiedConfiguration.index < _modifiedConfiguration.configs.Count)
                _oldSelectedId = _modifiedConfiguration.configs[_modifiedConfiguration.index].id;
            if (focusIndex == -1)
            {
                int index = _modifiedConfiguration.index + 1;
                if (index < 0 || index > _modifiedConfiguration.configs.Count)
                    index = _modifiedConfiguration.configs.Count;

                focusIndex = index;
            }

            ShowWindow();

            if (focusIndex >= 0 && focusIndex < _modifiedConfiguration.configs.Count)
            {
                SetServerListSelectedIndex(focusIndex);
                LoadSelectedServer();
            }

            UpdateServersListBoxTopIndex();
        }

        private Font CreateFont()
        {
            try
            {
                return new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            }
            catch
            {
                try
                {
                    return new Font("新宋体", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
                }
                catch
                {
                    return new Font(FontFamily.GenericMonospace, 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
                }
            }
        }

        private void UpdateTexts()
        {
            Text = $@"{I18N.GetString("Edit Servers")}({(_controller.GetCurrentConfiguration().shareOverLan ? I18N.GetString("Any") : I18N.GetString("Local"))}:{_controller.GetCurrentConfiguration().localPort} {I18N.GetString("Version")}:{UpdateChecker.FullVersion})";

            btnAdd.Text = I18N.GetString("&Add");
            btnDelete.Text = I18N.GetString("&Delete");
            btnUp.Text = I18N.GetString("Up");
            btnDown.Text = I18N.GetString("Down");

            const string mark_str = "*";
            chkIP.Text = mark_str + I18N.GetString("Server IP");
            lblServerPort.Text = mark_str + I18N.GetString("Server Port");
            lblUDPPort.Text = I18N.GetString("UDP Port");
            chkPassword.Text = mark_str + I18N.GetString("Password");
            lblEncryption.Text = mark_str + I18N.GetString("Encryption");
            lblTCPProtocol.Text = mark_str + I18N.GetString(lblTCPProtocol.Text);
            lblObfs.Text = mark_str + I18N.GetString(lblObfs.Text);
            lblRemarks.Text = I18N.GetString("Remarks");
            lblGroup.Text = I18N.GetString("Group");

            chkAdvSetting.Text = I18N.GetString(chkAdvSetting.Text);
            lblTcpOverUdp.Text = I18N.GetString(lblTcpOverUdp.Text);
            lblUdpOverTcp.Text = I18N.GetString(lblUdpOverTcp.Text);
            lblProtocolParam.Text = I18N.GetString(lblProtocolParam.Text);
            lblObfsParam.Text = I18N.GetString(lblObfsParam.Text);
            lblObfsUdp.Text = I18N.GetString(lblObfsUdp.Text);
            LabelNote.Text = I18N.GetString(LabelNote.Text);
            chkTcpOverUdp.Text = I18N.GetString(chkTcpOverUdp.Text);
            chkUdpOverTcp.Text = I18N.GetString(chkUdpOverTcp.Text);
            chkObfsUDP.Text = I18N.GetString(chkObfsUDP.Text);
            chkSSRLink.Text = I18N.GetString(chkSSRLink.Text);
            for (int i = 0; i < cmbTcpProtocol.Items.Count; ++i)
            {
                cmbTcpProtocol.Items[i] = I18N.GetString(cmbTcpProtocol.Items[i].ToString());
            }

            ServerGroupBox.Text = I18N.GetString("Server");

            btnOK.Text = I18N.GetString("OK");
            btnCancel.Text = I18N.GetString("Cancel");
            llbUpdate.MaximumSize = new Size(lstServers.Width, lstServers.Height);
            llbUpdate.Text = string.Format(I18N.GetString("New version {0} {1} available"), UpdateChecker.Name, _updateChecker.LatestVersionNumber);
        }

        private void controller_ConfigChanged(object sender, EventArgs e)
        {
            LoadCurrentConfiguration();
        }

        private void ShowWindow()
        {
            Opacity = 1;
            Show();
            txtIP.Focus();
        }

        private int SaveOldSelectedServer()
        {
            try
            {
                if (_oldSelectedIndex == -1 || _oldSelectedIndex >= _modifiedConfiguration.configs.Count)
                {
                    return 0; // no changes
                }
                Server server = new Server
                {
                    server = txtIP.Text.Trim(),
                    server_port = Convert.ToUInt16(nudServerPort.Value),
                    server_udp_port = Convert.ToUInt16(nudUdpPort.Value),
                    password = txtPassword.Text,
                    method = cmbEncryption.Text,
                    protocol = cmbTcpProtocol.Text,
                    protocolparam = txtProtocolParam.Text,
                    obfs = cmbObfs.Text,
                    obfsparam = txtObfsParam.Text,
                    remarks = txtRemarks.Text,
                    group = txtGroup.Text.Trim(),
                    udp_over_tcp = chkUdpOverTcp.Checked,
                    //obfs_udp = CheckObfsUDP.Checked,
                    id = _SelectedID
                };
                Configuration.CheckServer(server);
                int ret = 0;
                if (_modifiedConfiguration.configs[_oldSelectedIndex].server != server.server
                    || _modifiedConfiguration.configs[_oldSelectedIndex].server_port != server.server_port
                    || _modifiedConfiguration.configs[_oldSelectedIndex].remarks_base64 != server.remarks_base64
                    || _modifiedConfiguration.configs[_oldSelectedIndex].group != server.group
                    )
                {
                    ret = 1; // display changed
                }
                Server oldServer = _modifiedConfiguration.configs[_oldSelectedIndex];
                if (oldServer.isMatchServer(server))
                {
                    server.setObfsData(oldServer.getObfsData());
                    server.setProtocolData(oldServer.getProtocolData());
                    server.enable = oldServer.enable;
                }
                _modifiedConfiguration.configs[_oldSelectedIndex] = server;

                return ret;
            }
            catch (FormatException)
            {
                MessageBox.Show(I18N.GetString("Illegal port number format"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            return -1; // ERROR
        }

        private void GenQr(string str)
        {
            var width = 350 * Util.Utils.GetDpiMul() / 4;
            if (txtLink.Focused || chkSSRLink.Focused)
            {
                var qrText = str;
                var code = Encoder.encode(qrText, ErrorCorrectionLevel.H);
                var m = code.Matrix;
                var blockSize = Math.Max(width / (m.Width + 2), 1);
                var drawArea = new Bitmap(((m.Width + 2) * blockSize), (m.Height + 2) * blockSize);
                using (var g = Graphics.FromImage(drawArea))
                {
                    g.Clear(Color.White);
                    using (Brush b = new SolidBrush(Color.Black))
                    {
                        for (var row = 0; row < m.Width; ++row)
                        {
                            for (var col = 0; col < m.Height; ++col)
                            {
                                if (m[row, col] != 0)
                                {
                                    g.FillRectangle(b, blockSize * (row + 1), blockSize * (col + 1), blockSize, blockSize);
                                }
                            }
                        }
                    }
                }
                picQRcode.Image = drawArea;
                picQRcode.Visible = true;
            }
            else
            {
                picQRcode.Visible = false;
            }
        }

        private void LoadSelectedServer()
        {
            if (lstServers.SelectedIndex >= 0 && lstServers.SelectedIndex < _modifiedConfiguration.configs.Count)
            {
                Server server = _modifiedConfiguration.configs[lstServers.SelectedIndex];

                txtIP.Text = server.server;
                nudServerPort.Value = server.server_port;
                nudUdpPort.Value = server.server_udp_port;
                txtPassword.Text = server.password;
                cmbEncryption.Text = server.method ?? "aes-256-cfb";
                if (string.IsNullOrEmpty(server.protocol))
                {
                    cmbTcpProtocol.Text = @"origin";
                }
                else
                {
                    cmbTcpProtocol.Text = server.protocol ?? "origin";
                }
                string obfs_text = server.obfs ?? "plain";
                cmbObfs.Text = obfs_text;
                txtProtocolParam.Text = server.protocolparam;
                txtObfsParam.Text = server.obfsparam;
                txtRemarks.Text = server.remarks;
                txtGroup.Text = server.group;
                chkUdpOverTcp.Checked = server.udp_over_tcp;
                //CheckObfsUDP.Checked = server.obfs_udp;
                _SelectedID = server.id;

                ServerGroupBox.Visible = true;

                if (cmbTcpProtocol.Text == @"origin"
                    && obfs_text == @"plain"
                    && !chkUdpOverTcp.Checked
                    )
                {
                    chkAdvSetting.Checked = false;
                }

                txtLink.Text = chkSSRLink.Checked ? server.GetSSRLinkForServer() : server.GetSSLinkForServer();

                if (chkTcpOverUdp.Checked || chkUdpOverTcp.Checked || server.server_udp_port != 0)
                {
                    chkAdvSetting.Checked = true;
                }

                //PasswordLabel.Checked = false;
                //IPLabel.Checked = false;
                Update_SSR_controls_Visible();
                UpdateObfsTextBox();
                txtLink.SelectAll();
                GenQr(txtLink.Text);
            }
            else
            {
                ServerGroupBox.Visible = false;
            }
        }

        private void LoadConfiguration()
        {
            if (lstServers.Items.Count != _modifiedConfiguration.configs.Count)
            {
                lstServers.Items.Clear();
                foreach (Server server in _modifiedConfiguration.configs)
                {
                    if (!string.IsNullOrEmpty(server.group))
                    {
                        lstServers.Items.Add(server.group + " - " + server.HiddenName());
                    }
                    else
                    {
                        lstServers.Items.Add("      " + server.HiddenName());
                    }
                }
            }
            else
            {
                for (int i = 0; i < _modifiedConfiguration.configs.Count; ++i)
                {
                    if (!string.IsNullOrEmpty(_modifiedConfiguration.configs[i].group))
                    {
                        lstServers.Items[i] = _modifiedConfiguration.configs[i].group + " - " + _modifiedConfiguration.configs[i].HiddenName();
                    }
                    else
                    {
                        lstServers.Items[i] = "      " + _modifiedConfiguration.configs[i].HiddenName();
                    }
                }
            }
        }

        public void SetServerListSelectedIndex(int index)
        {
            lstServers.ClearSelected();
            if (index < lstServers.Items.Count)
                lstServers.SelectedIndex = index;
            else
                _oldSelectedIndex = lstServers.SelectedIndex;
        }

        private void LoadCurrentConfiguration()
        {
            _modifiedConfiguration = _controller.GetConfiguration();
            LoadConfiguration();
            _allowSave = false;
            SetServerListSelectedIndex(_modifiedConfiguration.index);
            _allowSave = true;
            LoadSelectedServer();
        }

        private void ServersListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_oldSelectedIndex == lstServers.SelectedIndex || lstServers.SelectedIndex == -1)
            {
                // we are moving back to oldSelectedIndex or doing a force move
                return;
            }
            if (_allowSave)
            {
                int change = SaveOldSelectedServer();
                if (change == -1)
                {
                    lstServers.SelectedIndex = _oldSelectedIndex; // go back
                    return;
                }
                if (change == 1)
                {
                    LoadConfiguration();
                }
            }
            if (!_ignoreLoad) LoadSelectedServer();
            _oldSelectedIndex = lstServers.SelectedIndex;
        }

        private void UpdateServersListBoxTopIndex(int style = 0)
        {
            int visibleItems = lstServers.ClientSize.Height / lstServers.ItemHeight;
            int index;
            if (style == 0)
            {
                index = lstServers.SelectedIndex;
            }
            else
            {
                var items = lstServers.SelectedIndices;
                if (0 == items.Count)
                    index = 0;
                else
                    index = (style == 1 ? items[0] : items[items.Count - 1]);
            }
            int topIndex = Math.Max(index - visibleItems / 2, 0);
            lstServers.TopIndex = topIndex;
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            if (SaveOldSelectedServer() == -1)
            {
                return;
            }
            Server server = _oldSelectedIndex >= 0 && _oldSelectedIndex < _modifiedConfiguration.configs.Count
                ? Configuration.CopyServer(_modifiedConfiguration.configs[_oldSelectedIndex])
                : Configuration.GetDefaultServer();
            _modifiedConfiguration.configs.Insert(_oldSelectedIndex < 0 ? 0 : _oldSelectedIndex + 1, server);
            LoadConfiguration();
            _SelectedID = server.id;
            lstServers.SelectedIndex = _oldSelectedIndex + 1;
            _oldSelectedIndex = lstServers.SelectedIndex;
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            _oldSelectedIndex = lstServers.SelectedIndex;
            var items = lstServers.SelectedIndices;
            if (items.Count > 0)
            {
                int[] array = new int[items.Count];
                int i = 0;
                foreach (int index in items)
                {
                    array[i++] = index;
                }
                Array.Sort(array);
                for (--i; i >= 0; --i)
                {
                    int index = array[i];
                    if (index >= 0 && index < _modifiedConfiguration.configs.Count)
                    {
                        _modifiedConfiguration.configs.RemoveAt(index);
                    }
                }
            }
            if (_oldSelectedIndex >= _modifiedConfiguration.configs.Count)
            {
                _oldSelectedIndex = _modifiedConfiguration.configs.Count - 1;
            }
            if (_oldSelectedIndex < 0)
            {
                _oldSelectedIndex = 0;
            }
            if (_oldSelectedIndex < _modifiedConfiguration.configs.Count)
                lstServers.SelectedIndex = _oldSelectedIndex;
            LoadConfiguration();
            SetServerListSelectedIndex(_oldSelectedIndex);
            LoadSelectedServer();
            UpdateServersListBoxTopIndex();
        }

        private void OKButton_Click(object sender, EventArgs e)
        {
            if (SaveOldSelectedServer() == -1)
            {
                return;
            }
            if (_modifiedConfiguration.configs.Count == 0)
            {
                MessageBox.Show(I18N.GetString("Please add at least one server"));
                return;
            }
            if (_oldSelectedId != null)
            {
                for (int i = 0; i < _modifiedConfiguration.configs.Count; ++i)
                {
                    if (_modifiedConfiguration.configs[i].id == _oldSelectedId)
                    {
                        _modifiedConfiguration.index = i;
                        break;
                    }
                }
            }
            _controller.SaveServersConfig(_modifiedConfiguration);
            Close();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void ConfigForm_Shown(object sender, EventArgs e)
        {
            txtIP.Focus();
        }

        private void ConfigForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _controller.ConfigChanged -= controller_ConfigChanged;
        }

        private void UpButton_Click(object sender, EventArgs e)
        {
            _oldSelectedIndex = lstServers.SelectedIndex;
            int index = _oldSelectedIndex;
            SaveOldSelectedServer();
            var items = lstServers.SelectedIndices;
            if (items.Count == 1)
            {
                if (index > 0 && index < _modifiedConfiguration.configs.Count)
                {
                    _modifiedConfiguration.configs.Reverse(index - 1, 2);
                    lstServers.ClearSelected();
                    lstServers.SelectedIndex = _oldSelectedIndex = index - 1;
                    LoadConfiguration();
                    lstServers.ClearSelected();
                    lstServers.SelectedIndex = _oldSelectedIndex = index - 1;
                    LoadSelectedServer();
                }
            }
            else if (0 == items.Count)
            {
                // Handle when server list is empty.
                _oldSelectedIndex = -1;
                lstServers.ClearSelected();
                LoadSelectedServer();
            }
            else
            {
                List<int> all_items = new List<int>();
                foreach (int item in items)
                {
                    if (item == 0)
                        return;
                    all_items.Add(item);
                }
                foreach (int item in all_items)
                {
                    _modifiedConfiguration.configs.Reverse(item - 1, 2);
                }
                _allowSave = false;
                _ignoreLoad = true;

                lstServers.SelectedIndex = _oldSelectedIndex = index - 1;

                LoadConfiguration();
                lstServers.ClearSelected();
                foreach (int item in all_items)
                {
                    if (item != index)
                        lstServers.SelectedIndex = _oldSelectedIndex = item - 1;
                }

                lstServers.SelectedIndex = _oldSelectedIndex = index - 1;

                _ignoreLoad = false;
                _allowSave = true;
                LoadSelectedServer();
            }
            UpdateServersListBoxTopIndex(1);
        }

        private void DownButton_Click(object sender, EventArgs e)
        {
            _oldSelectedIndex = lstServers.SelectedIndex;
            int index = _oldSelectedIndex;
            SaveOldSelectedServer();
            var items = lstServers.SelectedIndices;
            if (items.Count == 1)
            {
                if (_oldSelectedIndex >= 0 && _oldSelectedIndex < _modifiedConfiguration.configs.Count - 1)
                {
                    _modifiedConfiguration.configs.Reverse(index, 2);
                    lstServers.ClearSelected();
                    lstServers.SelectedIndex = _oldSelectedIndex = index + 1;
                    LoadConfiguration();
                    lstServers.ClearSelected();
                    lstServers.SelectedIndex = _oldSelectedIndex = index + 1;
                    LoadSelectedServer();
                }
            }
            else if (0 == items.Count)
            {
                // Handle when server list is empty.
                _oldSelectedIndex = -1;
                lstServers.ClearSelected();
                LoadSelectedServer();
            }
            else
            {
                List<int> rev_items = new List<int>();
                int max_index = lstServers.Items.Count - 1;
                foreach (int item in items)
                {
                    if (item == max_index)
                        return;
                    rev_items.Insert(0, item);
                }
                foreach (int item in rev_items)
                {
                    _modifiedConfiguration.configs.Reverse(item, 2);
                }
                _allowSave = false;
                _ignoreLoad = true;

                lstServers.SelectedIndex = _oldSelectedIndex = index + 1;

                LoadConfiguration();
                lstServers.ClearSelected();
                foreach (int item in rev_items)
                {
                    if (item != index)
                        lstServers.SelectedIndex = _oldSelectedIndex = item + 1;
                }

                lstServers.SelectedIndex = _oldSelectedIndex = index + 1;

                _ignoreLoad = false;
                _allowSave = true;
                LoadSelectedServer();
            }
            UpdateServersListBoxTopIndex(2);
        }

        private void TextBox_Enter(object sender, EventArgs e)
        {
            int change = SaveOldSelectedServer();
            if (change == 1)
            {
                LoadConfiguration();
            }
            LoadSelectedServer();
            ((TextBox)sender).SelectAll();
        }

        private void TextBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ((TextBox)sender).SelectAll();
            }
        }

        private void LinkUpdate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Utils.OpenURL(_updateChecker.LatestVersionURL);
        }

        private void PasswordLabel_CheckedChanged(object sender, EventArgs e)
        {
            txtPassword.UseSystemPasswordChar = !chkPassword.Checked;
        }

        private void UpdateObfsTextBox()
        {
            try
            {
                Obfs.ObfsBase obfs = (Obfs.ObfsBase)Obfs.ObfsFactory.GetObfs(cmbObfs.Text);
                int[] properties = obfs.GetObfs()[cmbObfs.Text];
                txtObfsParam.Enabled = properties[2] > 0;
            }
            catch
            {
                txtObfsParam.Enabled = true;
            }
        }

        private void ObfsCombo_TextChanged(object sender, EventArgs e)
        {
            UpdateObfsTextBox();
        }

        private void checkSSRLink_CheckedChanged(object sender, EventArgs e)
        {
            int change = SaveOldSelectedServer();
            if (change == 1)
            {
                LoadConfiguration();
            }
            LoadSelectedServer();
        }

        private void checkAdvSetting_CheckedChanged(object sender, EventArgs e)
        {
            Update_SSR_controls_Visible();
        }

        private void Update_SSR_controls_Visible()
        {
            SuspendLayout();
            if (chkAdvSetting.Checked)
            {
                lblUDPPort.Visible = true;
                nudUdpPort.Visible = true;
            }
            else
            {
                lblUDPPort.Visible = false;
                nudUdpPort.Visible = false;
            }
            if (chkAdvSetting.Checked)
            {
                lblUdpOverTcp.Visible = true;
                chkUdpOverTcp.Visible = true;
            }
            else
            {
                lblUdpOverTcp.Visible = false;
                chkUdpOverTcp.Visible = false;
            }
            ResumeLayout();
        }

        private void IPLabel_CheckedChanged(object sender, EventArgs e)
        {
            txtIP.UseSystemPasswordChar = !chkIP.Checked;
        }
    }
}
