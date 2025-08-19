using System.Runtime.InteropServices;
using System.Net.Http;
using System.Text.Json;

namespace PluginMan
{
    public partial class Form1 : Form
    {
        private FlowLayoutPanel pluginPanel;
        public Form1()
        {
            InitializeComponent();
            SetupUserIdBox();
        }
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;
        private void Form1_Load(object sender, EventArgs e)
        {
            pluginPanel = new FlowLayoutPanel
            {
                Location = new Point(20, 60),
                Size = new Size(760, 370),
                AutoScroll = true,
                BackColor = Color.FromArgb(25, 25, 25),
                Visible = false
            };
            Controls.Add(pluginPanel);
        }

        private void SetupUserIdBox()
        {
            textBox2.Text = "Enter your Roblox UserId...";
            textBox2.ForeColor = Color.Gray;

            textBox2.GotFocus += (s, e) =>
            {
                if (textBox2.Text == "Enter your Roblox UserId...")
                {
                    textBox2.Text = "";
                    textBox2.ForeColor = Color.White;
                }
            };

            textBox2.KeyPress += (s, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                {
                    e.Handled = true;
                }
                if (e.KeyChar == (char)Keys.Enter)
                {
                    e.Handled = true;
                    LoadPlugins();
                }
            };
        }

        private async void LoadPlugins()
        {
            string userId = textBox2.Text.Trim();
            if (string.IsNullOrEmpty(userId) || !userId.All(char.IsDigit))
            {
                MessageBox.Show("Please enter a valid numeric Roblox UserId.", "Invalid UserId", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            button6.Visible = false;
            textBox1.Visible = false;
            pluginPanel.Visible = false;

            Application.DoEvents(); 

            string installedPluginsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Roblox",
                userId,
                "InstalledPlugins"
            );

            if (!Directory.Exists(installedPluginsPath))
            {
                MessageBox.Show($"InstalledPlugins folder not found for UserId {userId}", "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                button6.Visible = true;
                textBox1.Visible = true;
                return;
            }

            List<(string Name, string Owner)> pluginData = new List<(string, string)>();

            await Task.Run(async () =>
            {
                List<string> pluginAssetIds = Directory.GetDirectories(installedPluginsPath)
                    .Select(Path.GetFileName)
                    .Where(id => long.TryParse(id, out _))
                    .ToList();

                using (HttpClient client = new HttpClient())
                {
                    

                    foreach (var assetId in pluginAssetIds)
                    {
                        string pluginName = $"Unknown Plugin ({assetId})";
                        string ownerName = "";

                        if (assetId == "0")
                        {
                            // Local plugin
                            string localPluginsPath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "Roblox",
                                "Plugins"
                            );

                            if (Directory.Exists(localPluginsPath))
                            {
                                var localPlugins = Directory.GetFiles(localPluginsPath, "*.rbxm")
                                    .Concat(Directory.GetFiles(localPluginsPath, "*.rbxmx"))
                                    .Select(Path.GetFileNameWithoutExtension)
                                    .ToList();

                                if (localPlugins.Count > 0)
                                {
                                    pluginName = localPlugins[0];
                                    ownerName = "By Local";
                                }
                                else
                                {
                                    pluginName = "Local Plugin (No Name)";
                                    ownerName = "By Local";
                                }
                            }
                        }
                        else
                        {
                            try
                            {
                                string apiUrl = $"https://economy.roblox.com/v2/assets/{assetId}/details";
                                string json = await client.GetStringAsync(apiUrl);

                                using JsonDocument doc = JsonDocument.Parse(json);
                                pluginName = doc.RootElement.GetProperty("Name").GetString();
                                ownerName = "By " + doc.RootElement.GetProperty("Creator").GetProperty("Name").GetString();
                            }
                            catch { }
                        }

                        pluginData.Add((pluginName, ownerName));
                    }
                }
            });
            pluginPanel.Controls.Clear();
            foreach (var (pluginName, ownerName) in pluginData)
            {
                Panel pluginCard = new Panel
                {
                    Width = 220,
                    Height = 100,
                    BackColor = Color.FromArgb(35, 35, 35),
                    Margin = new Padding(10),
                    BorderStyle = BorderStyle.FixedSingle
                };

                Label nameLabel = new Label
                {
                    Text = pluginName,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    AutoSize = false,
                    Dock = DockStyle.Top,
                    Height = 25,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                Label ownerLabel = new Label
                {
                    Text = ownerName,
                    ForeColor = Color.Gray,
                    Font = new Font("Segoe UI", 8),
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.TopCenter
                };

                pluginCard.Controls.Add(ownerLabel);
                pluginCard.Controls.Add(nameLabel);
                pluginPanel.Controls.Add(pluginCard);
            }
            pluginPanel.Visible = true;
            button6.Visible = true;
            textBox1.Visible = true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ContextMenuStrip fileMenu = new ContextMenuStrip();

            ToolStripMenuItem uploadItem = new ToolStripMenuItem("Upload Plugin");
            uploadItem.Click += UploadPlugin_Click;
            fileMenu.Items.Add(uploadItem);

            ToolStripMenuItem refreshItem = new ToolStripMenuItem("Refresh");
            refreshItem.Click += RefreshUI_Click;
            fileMenu.Items.Add(refreshItem);

            fileMenu.Show(button1, new Point(0, button1.Height));
        }

        private void UploadPlugin_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Select a Roblox Plugin File";
                openFileDialog.Filter = "Roblox Plugin Files (*.rbxm;*.rbxmx)|*.rbxm;*.rbxmx";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    MessageBox.Show($"Plugin selected: {Path.GetFileName(filePath)}", "Upload Plugin", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void RefreshUI_Click(object sender, EventArgs e)
        {
            textBox1.Text = "Whoops! No plguin found. Ad one or check the console for errors.";
        }

        private void button3_Click(object sender, EventArgs e) { }

        private void button4_Click(object sender, EventArgs e)
        {
            ContextMenuStrip helpMenu = new ContextMenuStrip();

            ToolStripMenuItem helpItem = new ToolStripMenuItem("Help");
            helpItem.Click += (s, ev) =>
            {
                string helpFilePath = Path.Combine(Application.StartupPath, "Help.txt");
                if (File.Exists(helpFilePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                    {
                        FileName = helpFilePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("Help.txt not found in application folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            helpMenu.Items.Add(helpItem);

            ToolStripMenuItem discordItem = new ToolStripMenuItem("Discord");
            discordItem.Click += (s, ev) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = "https://discord.gg/tNV6FZRrcT",
                    UseShellExecute = true
                });
            };
            helpMenu.Items.Add(discordItem);

            helpMenu.Show(button4, new Point(0, button4.Height));
        }

        private void textBox2_TextChanged(object sender, EventArgs e) { }
        private void textBox1_TextChanged(object sender, EventArgs e) { }
        private void guna2ImageButton1_Click(object sender, EventArgs e) { }
        private void panel1_Paint(object sender, PaintEventArgs e) { }

        private void panel1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        private void button6_Click(object sender, EventArgs e) { }
    }
}
