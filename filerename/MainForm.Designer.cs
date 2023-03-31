namespace FileReName;

partial class MainForm
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
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

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.menuStrip1 = new System.Windows.Forms.MenuStrip();
        this.fileSelect = new System.Windows.Forms.ToolStripMenuItem();
        this.folderSelect = new System.Windows.Forms.ToolStripMenuItem();
        this.startButton = new System.Windows.Forms.ToolStripMenuItem();
        this.panel1 = new System.Windows.Forms.Panel();
        this.reverseSelect = new System.Windows.Forms.LinkLabel();
        this.allSelect = new System.Windows.Forms.LinkLabel();
        this.previewButton = new System.Windows.Forms.Button();
        this.ruleInput = new System.Windows.Forms.TextBox();
        this.label2 = new System.Windows.Forms.Label();
        this.splitInput = new System.Windows.Forms.TextBox();
        this.label1 = new System.Windows.Forms.Label();
        this.fileList = new System.Windows.Forms.ListView();
        this.columnHeader1 = new System.Windows.Forms.ColumnHeader();
        this.columnHeader2 = new System.Windows.Forms.ColumnHeader();
        this.columnHeader3 = new System.Windows.Forms.ColumnHeader();
        this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
        this.removeMenu = new System.Windows.Forms.ToolStripMenuItem();
        this.imageList1 = new System.Windows.Forms.ImageList(this.components);
        this.fileDialog = new System.Windows.Forms.OpenFileDialog();
        this.folderDialog = new System.Windows.Forms.FolderBrowserDialog();
        this.menuStrip1.SuspendLayout();
        this.panel1.SuspendLayout();
        this.contextMenuStrip1.SuspendLayout();
        this.SuspendLayout();
        // 
        // menuStrip1
        // 
        this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileSelect,
            this.folderSelect,
            this.startButton});
        this.menuStrip1.Location = new System.Drawing.Point(0, 0);
        this.menuStrip1.Name = "menuStrip1";
        this.menuStrip1.Size = new System.Drawing.Size(666, 25);
        this.menuStrip1.TabIndex = 0;
        this.menuStrip1.Text = "menuStrip1";
        // 
        // fileSelect
        // 
        this.fileSelect.Name = "fileSelect";
        this.fileSelect.Size = new System.Drawing.Size(68, 21);
        this.fileSelect.Text = "添加文件";
        this.fileSelect.Click += new System.EventHandler(this.fileSelect_Click);
        // 
        // folderSelect
        // 
        this.folderSelect.Name = "folderSelect";
        this.folderSelect.Size = new System.Drawing.Size(68, 21);
        this.folderSelect.Text = "添加目录";
        this.folderSelect.Click += new System.EventHandler(this.folderSelect_Click);
        // 
        // startButton
        // 
        this.startButton.Name = "startButton";
        this.startButton.Size = new System.Drawing.Size(44, 21);
        this.startButton.Text = "开始";
        this.startButton.Click += new System.EventHandler(this.startButton_Click);
        // 
        // panel1
        // 
        this.panel1.AutoSize = true;
        this.panel1.Controls.Add(this.reverseSelect);
        this.panel1.Controls.Add(this.allSelect);
        this.panel1.Controls.Add(this.previewButton);
        this.panel1.Controls.Add(this.ruleInput);
        this.panel1.Controls.Add(this.label2);
        this.panel1.Controls.Add(this.splitInput);
        this.panel1.Controls.Add(this.label1);
        this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
        this.panel1.Location = new System.Drawing.Point(0, 25);
        this.panel1.Name = "panel1";
        this.panel1.Size = new System.Drawing.Size(666, 40);
        this.panel1.TabIndex = 1;
        // 
        // reverseSelect
        // 
        this.reverseSelect.AutoSize = true;
        this.reverseSelect.LinkColor = System.Drawing.Color.Black;
        this.reverseSelect.Location = new System.Drawing.Point(46, 39);
        this.reverseSelect.Name = "reverseSelect";
        this.reverseSelect.Size = new System.Drawing.Size(29, 12);
        this.reverseSelect.TabIndex = 18;
        this.reverseSelect.TabStop = true;
        this.reverseSelect.Text = "反选";
        this.reverseSelect.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.reverseSelect_LinkClicked);
        // 
        // allSelect
        // 
        this.allSelect.AutoSize = true;
        this.allSelect.LinkColor = System.Drawing.Color.Black;
        this.allSelect.Location = new System.Drawing.Point(11, 39);
        this.allSelect.Name = "allSelect";
        this.allSelect.Size = new System.Drawing.Size(29, 12);
        this.allSelect.TabIndex = 17;
        this.allSelect.TabStop = true;
        this.allSelect.Text = "全选";
        this.allSelect.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.allSelect_LinkClicked);
        // 
        // previewButton
        // 
        this.previewButton.Location = new System.Drawing.Point(81, 34);
        this.previewButton.Name = "previewButton";
        this.previewButton.Size = new System.Drawing.Size(75, 23);
        this.previewButton.TabIndex = 15;
        this.previewButton.Text = "预览结果";
        this.previewButton.UseVisualStyleBackColor = true;
        this.previewButton.Click += new System.EventHandler((s,e) => Preview());
        // 
        // ruleInput
        // 
        this.ruleInput.Location = new System.Drawing.Point(197, 9);
        this.ruleInput.Name = "ruleInput";
        this.ruleInput.Size = new System.Drawing.Size(140, 21);
        this.ruleInput.TabIndex = 10;
        this.ruleInput.Text = "[{}][{}]";
        // 
        // label2
        // 
        this.label2.AutoSize = true;
        this.label2.Location = new System.Drawing.Point(160, 12);
        this.label2.Name = "label2";
        this.label2.Size = new System.Drawing.Size(65, 12);
        this.label2.TabIndex = 9;
        this.label2.Text = "规则";
        // 
        // splitInput
        // 
        this.splitInput.Location = new System.Drawing.Point(60, 9);
        this.splitInput.Name = "splitInput";
        this.splitInput.Size = new System.Drawing.Size(62, 21);
        this.splitInput.TabIndex = 8;
        this.splitInput.Text = "[]";
        // 
        // label1
        // 
        this.label1.AutoSize = true;
        this.label1.Location = new System.Drawing.Point(11, 12);
        this.label1.Name = "label1";
        this.label1.Size = new System.Drawing.Size(41, 12);
        this.label1.TabIndex = 7;
        this.label1.Text = "分隔符";
        // 
        // fileList
        // 
        this.fileList.AllowDrop = true;
        this.fileList.CheckBoxes = true;
        this.fileList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2,
            this.columnHeader3});
        this.fileList.ContextMenuStrip = this.contextMenuStrip1;
        this.fileList.Dock = System.Windows.Forms.DockStyle.Fill;
        this.fileList.FullRowSelect = true;
        this.fileList.GridLines = true;
        this.fileList.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
        this.fileList.Location = new System.Drawing.Point(0, 100);
        this.fileList.Name = "fileList";
        this.fileList.Size = new System.Drawing.Size(666, 402);
        this.fileList.SmallImageList = this.imageList1;
        this.fileList.TabIndex = 2;
        this.fileList.UseCompatibleStateImageBehavior = false;
        this.fileList.View = System.Windows.Forms.View.Details;
        this.fileList.DragDrop += new System.Windows.Forms.DragEventHandler(this.fileList_DragDrop);
        this.fileList.DragEnter += new System.Windows.Forms.DragEventHandler(this.fileList_DragEnter);
        // 
        // columnHeader1
        // 
        this.columnHeader1.Text = "文件名";
        this.columnHeader1.Width = 348;
        // 
        // columnHeader2
        // 
        this.columnHeader2.Text = "状态";
        this.columnHeader2.Width = 86;
        // 
        // columnHeader3
        // 
        this.columnHeader3.Text = "预览";
        this.columnHeader3.Width = 223;
        // 
        // contextMenuStrip1
        // 
        this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.removeMenu});
        this.contextMenuStrip1.Name = "contextMenuStrip1";
        this.contextMenuStrip1.Size = new System.Drawing.Size(125, 26);
        // 
        // removeMenu
        // 
        this.removeMenu.Name = "removeMenu";
        this.removeMenu.Size = new System.Drawing.Size(124, 22);
        this.removeMenu.Text = "移除文件";
        this.removeMenu.Click += new System.EventHandler(this.removeMenu_Click);
        // 
        // imageList1
        // 
        this.imageList1.ColorDepth = System.Windows.Forms.ColorDepth.Depth8Bit;
        this.imageList1.ImageSize = new System.Drawing.Size(1, 18);
        this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
        // 
        // fileDialog
        // 
        this.fileDialog.Multiselect = true;
        // 
        // MainForm
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(666, 502);
        this.Controls.Add(this.fileList);
        this.Controls.Add(this.panel1);
        this.Controls.Add(this.menuStrip1);
        this.MainMenuStrip = this.menuStrip1;
        this.Name = "MainForm";
        this.Text = "文件重命名";
        this.menuStrip1.ResumeLayout(false);
        this.menuStrip1.PerformLayout();
        this.panel1.ResumeLayout(false);
        this.panel1.PerformLayout();
        this.contextMenuStrip1.ResumeLayout(false);
        this.ResumeLayout(false);
        this.PerformLayout();

    }

    private System.Windows.Forms.MenuStrip menuStrip1;
    private System.Windows.Forms.Panel panel1;
    private System.Windows.Forms.TextBox ruleInput;
    private System.Windows.Forms.Label label2;
    private System.Windows.Forms.TextBox splitInput;
    private System.Windows.Forms.Label label1;
    private System.Windows.Forms.ListView fileList;
    private System.Windows.Forms.ColumnHeader columnHeader1;
    private System.Windows.Forms.ColumnHeader columnHeader2;
    private System.Windows.Forms.Button previewButton;
    private System.Windows.Forms.ImageList imageList1;
    private System.Windows.Forms.ToolStripMenuItem startButton;
    private System.Windows.Forms.ColumnHeader columnHeader3;
    private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
    private System.Windows.Forms.ToolStripMenuItem removeMenu;
    private System.Windows.Forms.OpenFileDialog fileDialog;
    private System.Windows.Forms.FolderBrowserDialog folderDialog;
    private System.Windows.Forms.LinkLabel allSelect;
    private System.Windows.Forms.LinkLabel reverseSelect;
    private System.Windows.Forms.ToolStripMenuItem fileSelect;
    private System.Windows.Forms.ToolStripMenuItem folderSelect;
}
