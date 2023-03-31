using System;
using System.IO;
using System.Windows.Forms;
using System.Linq;

namespace FileReName;

public partial class MainForm : Form
{
    const string AVAILABLE = "可重命名";
    const string NOT_AVAILABLE = "不可重命名";
    const string SUCCEED = "成功";
    const string FAILED = "错误";

    public MainForm()
    {
        InitializeComponent();
    }

    private void fileSelect_Click(object sender, EventArgs e)
    {
        if (DialogResult.OK == fileDialog.ShowDialog())
        {
            AddFiles(fileDialog.FileNames);
        }
    }

    private void folderSelect_Click(object sender, EventArgs e)
    {
        if (DialogResult.OK == folderDialog.ShowDialog())
        {
            AddFiles(Directory.GetFiles(folderDialog.SelectedPath));
        }
    }

    private void AddFiles(string[] files)
    {
        foreach (var file in files)
        {
            if (!fileList.Items.Cast<ListViewItem>().Any(item => item.Tag.ToString() == file))
            {
                var item = fileList.Items.Add(new ListViewItem(new[] { Path.GetFileName(file), string.Empty, string.Empty }));
                item.Tag = file;
                item.Checked = true;
            }
        }

    }

    private void removeMenu_Click(object sender, EventArgs e)
    {
        foreach (ListViewItem item in fileList.SelectedItems)
        {
            fileList.Items.Remove(item);
        }

    }

    private void fileList_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effect = DragDropEffects.Link;
        }
        else
        {
            e.Effect = DragDropEffects.None;
        }
    }

    private void fileList_DragDrop(object sender, DragEventArgs e)
    {
        var names = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (names != null)
        {
            foreach (var str in names)
            {
                if (Directory.Exists(str))
                {
                    AddFiles(Directory.GetFiles(str));
                }
                else if (File.Exists(str))
                {
                    AddFiles(new[] { str });
                }
            }
        }
    }

    private void allSelect_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        foreach (ListViewItem item in fileList.Items)
        {
            item.Checked = true;
        }
    }

    private void reverseSelect_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        foreach (ListViewItem item in fileList.Items)
        {
            item.Checked = !item.Checked;
        }
    }

    private void startButton_Click(object sender, EventArgs e)
    {
        if (!Preview())
        {
            return;
        }
        if (DialogResult.Yes == MessageBox.Show("确认重命名?", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question))
        {
            foreach (ListViewItem item in fileList.CheckedItems)
            {
                if (item.SubItems[1].Text == AVAILABLE && item.SubItems[0].Text != item.SubItems[2].Text)
                {
                    try
                    {
                        FileName.Rename(item.Tag.ToString(), item.SubItems[2].Text);
                        item.SubItems[1].Text = SUCCEED;
                    }
                    catch (Exception ex)
                    {
                        item.SubItems[1].Text = FAILED;
                        item.SubItems[2].Text = ex.Message;
                    }
                }
            }
        }
    }

    private bool Preview()
    {
        if (sepInput.Text.Trim().Length == 0 || ruleInput.Text.Trim().Length == 0)
        {
            MessageBox.Show("规则不完整", "提示");
            return false;
        }
        foreach (ListViewItem item in fileList.CheckedItems)
        {
            if (item.SubItems[1].Text == SUCCEED)
            {
                continue;
            }
            var fileName = FileName.PreviewRename(item.SubItems[0].Text, sepInput.Text, ruleInput.Text);
            if (!string.IsNullOrEmpty(fileName))
            {
                item.SubItems[1].Text = AVAILABLE;
                item.SubItems[2].Text = fileName;
            }
            else
            {
                item.SubItems[1].Text = NOT_AVAILABLE;
                item.SubItems[2].Text = fileName;
            }
        }
        return true;
    }
}
