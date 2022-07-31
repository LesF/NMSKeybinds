using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using NMSKeybinds.Properties;
using System.Diagnostics;

namespace NMSKeybinds
{
    /// <summary>
    /// Various filtering and sorting features for reviewing:
    /// ...Program Files...\Steam\steamapps\common\No Man's Sky\Binaries\SETTINGS\TKGAMESETTINGS.MXML
    /// 
    /// Reason: I have a lifetime habit of using ESDF instead of WASD in games.
    /// When I started remapping NMS key bindings there were some sub-menu options which cannot
    /// be changed in game settings. This is a tool to enable assessment and planning alternative
    /// layouts. Will decide later if it is wise to include rewriting the config file.
    /// 
    /// Currently: E:\Programs_x86\Steam\steamapps\common\No Man's Sky\Binaries\SETTINGS
    /// </summary>
    public partial class MainForm : Form
    {
        private string mCurrentFilePath;
        private XmlDocument mCurrentSettings;
        private List<NMSKeyBinding> mBindings;
        private string mLastNMSSettingsDir;
        private ListViewColumnSorter lvColumnSorter;

        struct NMSKeyBinding 
        { 
            public string ActionSet; 
            public string Action;
            public string Button;
            public ListViewItem ToListViewItem()
                { return new ListViewItem(new string[] { this.ActionSet, this.Action, this.Button }); }
        }

        public MainForm()
        {
            InitializeComponent();

            lvColumnSorter = new ListViewColumnSorter();
            LVSettings.ListViewItemSorter = lvColumnSorter;

            /* Check 'last NSM settings dir' in user settings.
             * If preset & valid, enable menu option for viewing the directory.
             */
            mLastNMSSettingsDir = Settings.Default.NMSSettingsDirectory;
            viewNMSSettingsDirectoryToolStripMenuItem.Enabled = !string.IsNullOrEmpty(mLastNMSSettingsDir) && Directory.Exists(mLastNMSSettingsDir);
            if (!string.IsNullOrEmpty(mLastNMSSettingsDir))
            {
                string configFile = Path.Combine(mLastNMSSettingsDir, "TKGAMESETTINGS.MXML");
                if (File.Exists(configFile))
                    LoadSettingsFile(configFile);
            }
        }

        #region EventHandlers

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectSettingsFile();
        }
        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveSettingsFile("");
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
            Application.Exit();
        }

        private void BtnClear_Click(object sender, EventArgs e)
        {
            if (ComboFilterSet.Items.Count > 0)
                ComboFilterSet.SelectedIndex = 0;
            TxFilterAction.Text = "";
            TxFilterButton.Text = "";
            RefreshList();
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            RefreshList();
        }

        private void viewNMSSettingsDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(mLastNMSSettingsDir) && Directory.Exists(mLastNMSSettingsDir))
            {
                // TODO shell-execute directory and let the system default handle it
                Process ViewProcess = new Process();
                try
                {
                    ViewProcess.StartInfo.UseShellExecute = true;
                    ViewProcess.StartInfo.FileName = mLastNMSSettingsDir;
                    ViewProcess.StartInfo.WorkingDirectory = mLastNMSSettingsDir;
                    ViewProcess.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                    ViewProcess.StartInfo.CreateNoWindow = true;
                    ViewProcess.Start();
                }
                catch (Exception exc)
                {
                    MessageBox.Show(this, $"Process creation failed\r\n{exc.Message}", "Directory View Failed");
                }
            }
        }

        private void LVSettings_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Determine if clicked column is already the column that is being sorted.
            if (e.Column == lvColumnSorter.SortColumn)
            {
                // Reverse the current sort direction for this column.
                if (lvColumnSorter.Order == SortOrder.Ascending)
                {
                    lvColumnSorter.Order = SortOrder.Descending;
                }
                else
                {
                    lvColumnSorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                lvColumnSorter.SortColumn = e.Column;
                lvColumnSorter.Order = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            LVSettings.Sort();
        }
        #endregion

        private void SelectSettingsFile()
        {
            openFileDialog1 = new OpenFileDialog();
            openFileDialog1.AddExtension = true;
            openFileDialog1.CheckFileExists = true;
            openFileDialog1.Filter = "MXML Files|*.MXML|All Files|*.*";
            openFileDialog1.FilterIndex = 0;
            openFileDialog1.DefaultExt = ".MXML";
            openFileDialog1.Title = "TKGAMESETTINGS Key Bindings";

            if (Settings.Default.NMSSettingsDirectory.Length > 0 && Directory.Exists(Settings.Default.NMSSettingsDirectory))
                openFileDialog1.InitialDirectory = Settings.Default.NMSSettingsDirectory;
            else
                openFileDialog1.RestoreDirectory = true;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string getFile = openFileDialog1.FileName;
                if (File.Exists(getFile))
                    LoadSettingsFile(getFile);
            }
        }

        /// <summary>
        /// Open the settings XML file.
        /// Pull the bits we are interested in into a data collection.
        /// TODO manage references between internal data collection and original
        /// XML elements, for updating the XML with changes (if implemented).
        /// </summary>
        /// <param name="getFile"></param>
        private void LoadSettingsFile(string getFile)
        {
            mCurrentSettings = new XmlDocument();
            viewNMSSettingsDirectoryToolStripMenuItem.Enabled = false;
            try
            {
                /* Save directory to users 'last NMS settings directory' config value
                 */
                FileInfo fileInfo = new FileInfo(getFile);
                DirectoryInfo dir = fileInfo.Directory;
                if (dir.Exists)
                {
                    Settings.Default.NMSSettingsDirectory = dir.FullName;
                    Settings.Default.Save();
                    viewNMSSettingsDirectoryToolStripMenuItem.Enabled = true;
                }
                mCurrentSettings.Load(getFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Something went wrong there\r\n" + ex.Message, "We had a small problem opening that file");
                return;
            }

            LVSettings.Items.Clear();
            ComboFilterSet.Items.Clear();
            ComboFilterSet.Items.Add("All action sets");
            mBindings = new List<NMSKeyBinding>();
            XmlNodeList level1Props = mCurrentSettings.SelectNodes("/Data/Property");
            XmlNode keyMapping = null;
            string propName = "";
            /* Not sure why my config has an empty "KeyMapping" node, and a populated "KeyMapping2" node.
             * 	<Property name="KeyMapping" />
             * 	<Property name="KeyMapping2">
             * 	    <Property name="KeyMapping2" value="GcInputActionMapping2.xml">
             *          <Property name="ActionSet" value="FRONTEND" />
             *          <Property name="Action" value="BaseBuilding_ToggleWiring" />
             *          <Property name="Button" value="KeyQ" />
             *          <Property name="Axis" value="None" />
             *      </Property>
             *      ...etc...
             *  </Property>
             *  
             * Look for first 'KeyMap...' node which has child nodes, assume that is the main config set.
             */
            foreach (XmlNode level1Prop in level1Props)
            {
                propName = level1Prop.Attributes["name"].Value;
                if (propName.StartsWith("KeyMap") && level1Prop.ChildNodes.Count > 0)
                {
                    keyMapping = level1Prop;
                    break;
                }
            }

            if (keyMapping != null && keyMapping.ChildNodes.Count > 0)
            {
                /* TODO save path to keyMapping node, so the full xpath to each of the key-binding
                 * properties in the collection can be referenced later, when updates need to be
                 * made to the original XML document.  Maybe formulate the xpath and add it to the
                 * NMSKeyBinding struct, so each property has its own reference for updating the XML.
                 */
                List<string> allActionSets = new List<string>();
                foreach (XmlNode level2Prop in keyMapping.ChildNodes)
                {
                    NMSKeyBinding keyBinding = new NMSKeyBinding();
                    XmlAttribute attr;
                    XmlNode nodeValue = level2Prop.SelectSingleNode("Property[@name='ActionSet']");
                    if (nodeValue != null)
                    {
                        attr = nodeValue.Attributes["value"];
                        if (attr != null)
                        { 
                            keyBinding.ActionSet = attr.Value;
                            if (!allActionSets.Contains(attr.Value))
                            {
                                // Add the ActionSet to the dropdown list, for filtering the listview
                                allActionSets.Add(attr.Value);
                                ComboFilterSet.Items.Add(attr.Value);
                            }
                        }
                    }
                    nodeValue = level2Prop.SelectSingleNode("Property[@name='Action']");
                    if (nodeValue != null)
                    {
                        attr = nodeValue.Attributes["value"];
                        if (attr != null)
                            keyBinding.Action = attr.Value;

                    }
                    nodeValue = level2Prop.SelectSingleNode("Property[@name='Button']");
                    if (nodeValue != null)
                    {
                        attr = nodeValue.Attributes["value"];
                        if (attr != null)
                            keyBinding.Button = attr.Value;
                    }

                    if (keyBinding.Button.Length > 0)
                        mBindings.Add(keyBinding);
               }
            }
            ComboFilterSet.SelectedIndex = 0;
            RefreshList();
        }

        private void SaveSettingsFile(string newFileName = "")
        {
            /* TODO
             * - zip current file to a timestamped backup
             * - overwrite active config with updated XML
             * - think about displaying existing backup copies and providing a quick-swap feature for reverting to any previous save
             */
            MessageBox.Show("Not implemented yet", "Save Config Changes", MessageBoxButtons.OK);
        }

        /// <summary>
        /// Reload the listview from the stored config data, applying the current filters.
        /// Uses simple OR filter, including any row which matches any of the filter values.
        /// </summary>
        private void RefreshList()
        {
            LVSettings.Items.Clear();
            LVSettings.SuspendLayout();

            string filterValue = TxFilterAction.Text.Trim();
            string[] actionList = filterValue.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

            filterValue = TxFilterButton.Text.Trim();
            string[] buttonList = filterValue.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

            string filterSet = "";
            if (ComboFilterSet.SelectedIndex > 0)
                filterSet = ComboFilterSet.SelectedItem.ToString();

            foreach (NMSKeyBinding keyBinding in mBindings)
            {
                if (filterSet.Length > 0 && !keyBinding.ActionSet.Equals(filterSet, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (actionList.Length > 0)
                {
                    bool actionMatch = false;
                    foreach(string value in actionList)
                        if (value.Length > 0 && keyBinding.Action.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            actionMatch = true;
                            break;
                        }
                    if (!actionMatch)
                        continue;
                }

                if (buttonList.Length > 0)
                {
                    bool buttonMatch = false;
                    foreach (string value in buttonList)
                        if (value.Length > 0 && keyBinding.Button.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            buttonMatch = true;
                            break;
                        }
                    if (!buttonMatch)
                        continue;
                }

                LVSettings.Items.Add(keyBinding.ToListViewItem());
            }
            LVSettings.ResumeLayout();
        }

    }
}
