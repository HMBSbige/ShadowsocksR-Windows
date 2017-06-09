namespace Shadowsocks.View
{
    partial class ServerLogForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle10 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle11 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle12 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle13 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle14 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle15 = new System.Windows.Forms.DataGridViewCellStyle();
            this.ServerDataGrid = new Shadowsocks.View.ServerLogForm.DoubleBufferListView();
            this.timer = new System.Windows.Forms.Timer(this.components);
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.ID = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Group = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Server = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Enable = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.TotalConnect = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Connecting = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.AvgLatency = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.AvgDownSpeed = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.MaxDownSpeed = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.AvgUpSpeed = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.MaxUpSpeed = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Download = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Upload = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DownloadRaw = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ErrorPercent = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ConnectError = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ConnectTimeout = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ConnectEmpty = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Continuous = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.ServerDataGrid)).BeginInit();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // ServerDataGrid
            // 
            this.ServerDataGrid.AllowUserToAddRows = false;
            this.ServerDataGrid.AllowUserToDeleteRows = false;
            this.ServerDataGrid.AllowUserToResizeRows = false;
            this.ServerDataGrid.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.ServerDataGrid.ColumnHeadersHeight = 46;
            this.ServerDataGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ID,
            this.Group,
            this.Server,
            this.Enable,
            this.TotalConnect,
            this.Connecting,
            this.AvgLatency,
            this.AvgDownSpeed,
            this.MaxDownSpeed,
            this.AvgUpSpeed,
            this.MaxUpSpeed,
            this.Download,
            this.Upload,
            this.DownloadRaw,
            this.ErrorPercent,
            this.ConnectError,
            this.ConnectTimeout,
            this.ConnectEmpty,
            this.Continuous});
            this.ServerDataGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ServerDataGrid.Location = new System.Drawing.Point(0, 0);
            this.ServerDataGrid.Margin = new System.Windows.Forms.Padding(0);
            this.ServerDataGrid.MinimumSize = new System.Drawing.Size(1, 1);
            this.ServerDataGrid.MultiSelect = false;
            this.ServerDataGrid.Name = "ServerDataGrid";
            this.ServerDataGrid.ReadOnly = true;
            this.ServerDataGrid.RowHeadersVisible = false;
            this.ServerDataGrid.RowTemplate.Height = 23;
            this.ServerDataGrid.Size = new System.Drawing.Size(132, 34);
            this.ServerDataGrid.TabIndex = 0;
            this.ServerDataGrid.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.ServerDataGrid_CellClick);
            this.ServerDataGrid.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.ServerDataGrid_CellDoubleClick);
            this.ServerDataGrid.ColumnWidthChanged += new System.Windows.Forms.DataGridViewColumnEventHandler(this.ServerDataGrid_ColumnWidthChanged);
            this.ServerDataGrid.SortCompare += new System.Windows.Forms.DataGridViewSortCompareEventHandler(this.ServerDataGrid_SortCompare);
            this.ServerDataGrid.MouseUp += new System.Windows.Forms.MouseEventHandler(this.ServerDataGrid_MouseUp);
            // 
            // timer
            // 
            this.timer.Enabled = true;
            this.timer.Interval = 250;
            this.timer.Tick += new System.EventHandler(this.timer_Tick);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.Controls.Add(this.ServerDataGrid, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.FixedSize;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.Size = new System.Drawing.Size(132, 22);
            this.tableLayoutPanel1.TabIndex = 1;
            // 
            // ID
            // 
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.ID.DefaultCellStyle = dataGridViewCellStyle1;
            this.ID.HeaderText = "ID";
            this.ID.MinimumWidth = 2;
            this.ID.Name = "ID";
            this.ID.ReadOnly = true;
            this.ID.Width = 36;
            // 
            // Group
            // 
            this.Group.HeaderText = "Group";
            this.Group.Name = "Group";
            this.Group.ReadOnly = true;
            this.Group.Width = 60;
            // 
            // Server
            // 
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            this.Server.DefaultCellStyle = dataGridViewCellStyle2;
            this.Server.HeaderText = "Server";
            this.Server.MinimumWidth = 2;
            this.Server.Name = "Server";
            this.Server.ReadOnly = true;
            this.Server.Width = 88;
            // 
            // Enable
            // 
            this.Enable.HeaderText = "Enable";
            this.Enable.MinimumWidth = 8;
            this.Enable.Name = "Enable";
            this.Enable.ReadOnly = true;
            this.Enable.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.Enable.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.Enable.Width = 24;
            // 
            // TotalConnect
            // 
            this.TotalConnect.HeaderText = "Total Connect";
            this.TotalConnect.MinimumWidth = 2;
            this.TotalConnect.Name = "TotalConnect";
            this.TotalConnect.ReadOnly = true;
            this.TotalConnect.Visible = false;
            this.TotalConnect.Width = 48;
            // 
            // Connecting
            // 
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.Connecting.DefaultCellStyle = dataGridViewCellStyle3;
            this.Connecting.HeaderText = "Connecting";
            this.Connecting.MinimumWidth = 16;
            this.Connecting.Name = "Connecting";
            this.Connecting.ReadOnly = true;
            this.Connecting.Width = 28;
            // 
            // AvgLatency
            // 
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.AvgLatency.DefaultCellStyle = dataGridViewCellStyle4;
            this.AvgLatency.HeaderText = "Latency";
            this.AvgLatency.MinimumWidth = 36;
            this.AvgLatency.Name = "AvgLatency";
            this.AvgLatency.ReadOnly = true;
            this.AvgLatency.Width = 48;
            // 
            // AvgDownSpeed
            // 
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.AvgDownSpeed.DefaultCellStyle = dataGridViewCellStyle5;
            this.AvgDownSpeed.HeaderText = "Avg DSpeed";
            this.AvgDownSpeed.MinimumWidth = 60;
            this.AvgDownSpeed.Name = "AvgDownSpeed";
            this.AvgDownSpeed.ReadOnly = true;
            this.AvgDownSpeed.Width = 60;
            // 
            // MaxDownSpeed
            // 
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.MaxDownSpeed.DefaultCellStyle = dataGridViewCellStyle6;
            this.MaxDownSpeed.HeaderText = "Max DSpeed";
            this.MaxDownSpeed.MinimumWidth = 2;
            this.MaxDownSpeed.Name = "MaxDownSpeed";
            this.MaxDownSpeed.ReadOnly = true;
            this.MaxDownSpeed.Width = 60;
            // 
            // AvgUpSpeed
            // 
            dataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.AvgUpSpeed.DefaultCellStyle = dataGridViewCellStyle7;
            this.AvgUpSpeed.HeaderText = "Avg UpSpeed";
            this.AvgUpSpeed.MinimumWidth = 60;
            this.AvgUpSpeed.Name = "AvgUpSpeed";
            this.AvgUpSpeed.ReadOnly = true;
            this.AvgUpSpeed.Width = 60;
            // 
            // MaxUpSpeed
            // 
            dataGridViewCellStyle8.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.MaxUpSpeed.DefaultCellStyle = dataGridViewCellStyle8;
            this.MaxUpSpeed.HeaderText = "Max UpSpeed";
            this.MaxUpSpeed.MinimumWidth = 2;
            this.MaxUpSpeed.Name = "MaxUpSpeed";
            this.MaxUpSpeed.ReadOnly = true;
            this.MaxUpSpeed.Width = 60;
            // 
            // Download
            // 
            dataGridViewCellStyle9.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.Download.DefaultCellStyle = dataGridViewCellStyle9;
            this.Download.HeaderText = "Dload";
            this.Download.MinimumWidth = 2;
            this.Download.Name = "Download";
            this.Download.ReadOnly = true;
            this.Download.Width = 60;
            // 
            // Upload
            // 
            dataGridViewCellStyle10.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.Upload.DefaultCellStyle = dataGridViewCellStyle10;
            this.Upload.HeaderText = "Upload";
            this.Upload.MinimumWidth = 2;
            this.Upload.Name = "Upload";
            this.Upload.ReadOnly = true;
            this.Upload.Width = 60;
            // 
            // DownloadRaw
            // 
            dataGridViewCellStyle11.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.DownloadRaw.DefaultCellStyle = dataGridViewCellStyle11;
            this.DownloadRaw.HeaderText = "DloadRaw";
            this.DownloadRaw.MinimumWidth = 2;
            this.DownloadRaw.Name = "DownloadRaw";
            this.DownloadRaw.ReadOnly = true;
            this.DownloadRaw.Width = 60;
            // 
            // ErrorPercent
            // 
            this.ErrorPercent.HeaderText = "Error Percent";
            this.ErrorPercent.MinimumWidth = 2;
            this.ErrorPercent.Name = "ErrorPercent";
            this.ErrorPercent.ReadOnly = true;
            this.ErrorPercent.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.ErrorPercent.Visible = false;
            this.ErrorPercent.Width = 48;
            // 
            // ConnectError
            // 
            dataGridViewCellStyle12.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.ConnectError.DefaultCellStyle = dataGridViewCellStyle12;
            this.ConnectError.HeaderText = "Error";
            this.ConnectError.MinimumWidth = 2;
            this.ConnectError.Name = "ConnectError";
            this.ConnectError.ReadOnly = true;
            this.ConnectError.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.ConnectError.Width = 28;
            // 
            // ConnectTimeout
            // 
            dataGridViewCellStyle13.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.ConnectTimeout.DefaultCellStyle = dataGridViewCellStyle13;
            this.ConnectTimeout.HeaderText = "Timeout";
            this.ConnectTimeout.MinimumWidth = 2;
            this.ConnectTimeout.Name = "ConnectTimeout";
            this.ConnectTimeout.ReadOnly = true;
            this.ConnectTimeout.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.ConnectTimeout.Width = 28;
            // 
            // ConnectEmpty
            // 
            dataGridViewCellStyle14.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.ConnectEmpty.DefaultCellStyle = dataGridViewCellStyle14;
            this.ConnectEmpty.HeaderText = "Empty Response";
            this.ConnectEmpty.MinimumWidth = 2;
            this.ConnectEmpty.Name = "ConnectEmpty";
            this.ConnectEmpty.ReadOnly = true;
            this.ConnectEmpty.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.ConnectEmpty.Width = 28;
            // 
            // Continuous
            // 
            dataGridViewCellStyle15.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.Continuous.DefaultCellStyle = dataGridViewCellStyle15;
            this.Continuous.HeaderText = "Continuous";
            this.Continuous.Name = "Continuous";
            this.Continuous.ReadOnly = true;
            this.Continuous.Visible = false;
            this.Continuous.Width = 28;
            // 
            // ServerLogForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(132, 22);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "ServerLogForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ServerLog";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.ServerLogForm_FormClosed);
            this.ResizeEnd += new System.EventHandler(this.ServerLogForm_ResizeEnd);
            this.Move += new System.EventHandler(this.ServerLogForm_Move);
            ((System.ComponentModel.ISupportInitialize)(this.ServerDataGrid)).EndInit();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Timer timer;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private DoubleBufferListView ServerDataGrid;
        private System.Windows.Forms.DataGridViewTextBoxColumn ID;
        private System.Windows.Forms.DataGridViewTextBoxColumn Group;
        private System.Windows.Forms.DataGridViewTextBoxColumn Server;
        private System.Windows.Forms.DataGridViewTextBoxColumn Enable;
        private System.Windows.Forms.DataGridViewTextBoxColumn TotalConnect;
        private System.Windows.Forms.DataGridViewTextBoxColumn Connecting;
        private System.Windows.Forms.DataGridViewTextBoxColumn AvgLatency;
        private System.Windows.Forms.DataGridViewTextBoxColumn AvgDownSpeed;
        private System.Windows.Forms.DataGridViewTextBoxColumn MaxDownSpeed;
        private System.Windows.Forms.DataGridViewTextBoxColumn AvgUpSpeed;
        private System.Windows.Forms.DataGridViewTextBoxColumn MaxUpSpeed;
        private System.Windows.Forms.DataGridViewTextBoxColumn Download;
        private System.Windows.Forms.DataGridViewTextBoxColumn Upload;
        private System.Windows.Forms.DataGridViewTextBoxColumn DownloadRaw;
        private System.Windows.Forms.DataGridViewTextBoxColumn ErrorPercent;
        private System.Windows.Forms.DataGridViewTextBoxColumn ConnectError;
        private System.Windows.Forms.DataGridViewTextBoxColumn ConnectTimeout;
        private System.Windows.Forms.DataGridViewTextBoxColumn ConnectEmpty;
        private System.Windows.Forms.DataGridViewTextBoxColumn Continuous;
    }
}