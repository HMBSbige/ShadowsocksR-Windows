using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Shadowsocks.Controller;
using Shadowsocks.Model;
using Shadowsocks.Properties;

namespace Shadowsocks.View
{
    public partial class SubscribeForm : Form
    {
        private ShadowsocksController controller;
        // this is a copy of configuration that we are working on
        private Configuration _modifiedConfiguration;

        public SubscribeForm(ShadowsocksController controller)
        {
            this.Font = System.Drawing.SystemFonts.MessageBoxFont;
            InitializeComponent();

            this.Icon = Icon.FromHandle(Resources.ssw128.GetHicon());
            this.controller = controller;

            UpdateTexts();
            controller.ConfigChanged += controller_ConfigChanged;

            LoadCurrentConfiguration();
        }

        private void UpdateTexts()
        {
            this.Text = I18N.GetString("Subscribe Settings");
            label1.Text = I18N.GetString("URL");
            label2.Text = I18N.GetString("Group name");
            checkBoxAutoUpdate.Text = I18N.GetString("Auto update");
            buttonOK.Text = I18N.GetString("OK");
            buttonCancel.Text = I18N.GetString("Cancel");
        }

        private void SubscribeForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            controller.ConfigChanged -= controller_ConfigChanged;
        }

        private void controller_ConfigChanged(object sender, EventArgs e)
        {
            LoadCurrentConfiguration();
        }

        private void LoadCurrentConfiguration()
        {
            _modifiedConfiguration = controller.GetConfiguration();
            LoadAllSettings();
        }

        private void LoadAllSettings()
        {
            textBoxURL.Text = _modifiedConfiguration.nodeFeedURL;
            textBoxGroup.Text = _modifiedConfiguration.nodeFeedGroup;
            checkBoxAutoUpdate.Checked = _modifiedConfiguration.nodeFeedAutoUpdate;
        }

        private int SaveAllSettings()
        {
            _modifiedConfiguration.nodeFeedURL = textBoxURL.Text;
            _modifiedConfiguration.nodeFeedGroup = textBoxGroup.Text;
            _modifiedConfiguration.nodeFeedAutoUpdate = checkBoxAutoUpdate.Checked;
            return 0;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            if (SaveAllSettings() == -1)
            {
                return;
            }
            controller.SaveServersConfig(_modifiedConfiguration);
            this.Close();
        }

        private void textBoxURL_TextChanged(object sender, EventArgs e)
        {
            textBoxGroup.Text = "";
        }
    }
}
