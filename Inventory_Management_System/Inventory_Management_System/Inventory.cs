using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SqlClient;

namespace Inventory_Management_System
{
    enum RowState
    {
        Existed,
        New,
        Modified,
        ModifiedNew,
        Deleted

    }
    public partial class NK_Form_Inventory : Form
    {
        private readonly checkPermission _permission;

        Database database = new Database();

        int selectedRow;
        private Label NK_lbl_GrandTotal;

        // ----- Inventory tab management -----
        private const string AddTabKey = "__add_inventory__";
        private int currentInventoryId = -1;

        public NK_Form_Inventory(checkPermission permission)
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterScreen;
            _permission = permission;

            NK_lbl_GrandTotal = new Label();
            NK_lbl_GrandTotal.BorderStyle = BorderStyle.FixedSingle;
            NK_lbl_GrandTotal.Font = new Font("Segoe UI Semibold", 11.25F, FontStyle.Bold, GraphicsUnit.Point, ((byte)(204)));
            NK_lbl_GrandTotal.Location = new Point(749, 33);
            NK_lbl_GrandTotal.Size = new Size(234, 33);
            NK_lbl_GrandTotal.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(NK_lbl_GrandTotal);

            SetupTabControl();
        }

        // ---------- Tab control setup ----------

        private void SetupTabControl()
        {
            // Remove the two old designer tabs; tabs are now built dynamically from the database
            NK_tabControl1.Controls.Clear();

            NK_tabControl1.MouseClick += NK_tabControl1_MouseClick;
            NK_tabControl1.Selected += NK_tabControl1_Selected;
        }

        private void LoadInventoryTabs(bool selectFirst)
        {
            NK_tabControl1.Selected -= NK_tabControl1_Selected;

            NK_tabControl1.Controls.Clear();

            database.openConnection();
            var command = new SqlCommand("SELECT id, name FROM Inventories ORDER BY id", database.GetSqlConnection());
            var reader = command.ExecuteReader();

            var ids = new List<int>();
            var names = new List<string>();
            while (reader.Read())
            {
                ids.Add(reader.GetInt32(0));
                names.Add(reader.GetString(1));
            }
            reader.Close();

            for (int i = 0; i < ids.Count; i++)
            {
                var page = new TabPage(names[i]);
                page.Tag = ids[i];
                NK_tabControl1.Controls.Add(page);
            }

            // The "+" tab to add a new inventory, always last
            // Only admins see the "+" tab to create new inventories
            if (_permission.IsAdmin)
            {
                var addPage = new TabPage("+");
                addPage.Tag = AddTabKey;
                NK_tabControl1.Controls.Add(addPage);
            }

            NK_tabControl1.Selected += NK_tabControl1_Selected;

            if (ids.Count > 0)
            {
                if (selectFirst || NK_tabControl1.SelectedIndex < 0 || NK_tabControl1.SelectedIndex >= ids.Count)
                {
                    NK_tabControl1.SelectedIndex = 0;
                }
                currentInventoryId = ids[NK_tabControl1.SelectedIndex];
                ShowInventoryControlsOnSelectedTab();
                RefreshDataGrid(NK_dataGridView1);
                UpdateGrandTotal(NK_dataGridView1);
            }
            else
            {
                currentInventoryId = -1;
                NK_dataGridView1.Rows.Clear();
                UpdateGrandTotal(NK_dataGridView1);
            }
        }

        private void ShowInventoryControlsOnSelectedTab()
        {
            var page = NK_tabControl1.SelectedTab;
            if (page == null || !(page.Tag is int)) return;

            NK_tabControl1.Dock = DockStyle.Fill;

            // Top header - fixed height at top
            NK_pnl_Registration.Dock = DockStyle.Top;
            NK_pnl_Registration.Height = 66;

            // Bottom panels - fixed height, anchored to bottom
            int bottomHeight = 260;

            NK_panel1.Size = new Size(566, 232);
            NK_panel1.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            NK_panel1.Location = new Point(15, page.Height - bottomHeight);

            NK_lbl_Management.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            NK_lbl_Management.Location = new Point(510, page.Height - bottomHeight + 5);

            NK_lbl_Management.Location = new Point(600, page.Height - bottomHeight + 5);

            NK_panel2.Size = new Size(213, 218);
            NK_panel2.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            NK_panel2.Location = new Point(600, page.Height - bottomHeight + 25);

            // DataGridView fills everything between top and bottom
            NK_dataGridView1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                                    | AnchorStyles.Left | AnchorStyles.Right;
            NK_dataGridView1.Location = new Point(9, 69);
            NK_dataGridView1.Size = new Size(page.Width - 18, page.Height - bottomHeight - 75);
            NK_dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            page.Controls.Add(NK_panel2);
            page.Controls.Add(NK_lbl_Management);
            page.Controls.Add(NK_panel1);
            page.Controls.Add(NK_dataGridView1);
            page.Controls.Add(NK_pnl_Registration);
        }

        private void NK_tabControl1_Selected(object sender, TabControlEventArgs e)
        {
            var page = e.TabPage;
            if (page == null) return;

            if (page.Tag is string tag && tag == AddTabKey)
            {
                PromptCreateInventory();
                return;
            }

            if (page.Tag is int invId)
            {
                currentInventoryId = invId;
                ShowInventoryControlsOnSelectedTab();
                ClearFields();
                RefreshDataGrid(NK_dataGridView1);
                UpdateGrandTotal(NK_dataGridView1);
            }
        }

        private void PromptCreateInventory()
        {
            string name = PromptForText("New Inventory", "Enter a name for the new inventory:", "");
            if (string.IsNullOrWhiteSpace(name))
            {
                // Nothing typed; go back to the first real tab instead of staying on "+"
                if (NK_tabControl1.TabPages.Count > 1)
                    NK_tabControl1.SelectedIndex = 0;
                return;
            }

            database.openConnection();
            var insert = new SqlCommand("INSERT INTO Inventories (name) VALUES (@name)", database.GetSqlConnection());
            insert.Parameters.AddWithValue("@name", name.Trim());
            insert.ExecuteNonQuery();

            LoadInventoryTabs(false);

            // Select the newly created tab (it will be the last real tab, just before "+")
            int newIndex = NK_tabControl1.TabPages.Count - 2;
            if (newIndex >= 0)
            {
                NK_tabControl1.SelectedIndex = newIndex;
            }
        }

        private void NK_tabControl1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            if (!_permission.IsAdmin) return; // non-admins cannot rename or delete tabs

            for (int i = 0; i < NK_tabControl1.TabCount; i++)
            {
                if (NK_tabControl1.GetTabRect(i).Contains(e.Location))
                {
                    var page = NK_tabControl1.TabPages[i];
                    if (page.Tag is int invId)
                    {
                        ShowTabContextMenu(page, invId, e.Location);
                    }
                    return;
                }
            }
        }

        private void ShowTabContextMenu(TabPage page, int inventoryId, Point location)
        {
            var menu = new ContextMenuStrip();

            menu.Items.Add("Rename", null, (s, e) =>
            {
                string newName = PromptForText("Rename Inventory", "Enter a new name:", page.Text);
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    database.openConnection();
                    var update = new SqlCommand("UPDATE Inventories SET name = @name WHERE id = @id", database.GetSqlConnection());
                    update.Parameters.AddWithValue("@name", newName.Trim());
                    update.Parameters.AddWithValue("@id", inventoryId);
                    update.ExecuteNonQuery();

                    page.Text = newName.Trim();
                }
            });

            menu.Items.Add("Delete", null, (s, e) =>
            {
                var confirm = MessageBox.Show(
                    $"Deleting '{page.Text}' will permanently delete ALL items inside it. This cannot be undone. Continue?",
                    "Delete Inventory",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm == DialogResult.Yes)
                {
                    database.openConnection();

                    var deleteItems = new SqlCommand("DELETE FROM Inventory WHERE inventory_id = @id", database.GetSqlConnection());
                    deleteItems.Parameters.AddWithValue("@id", inventoryId);
                    deleteItems.ExecuteNonQuery();

                    var deleteInv = new SqlCommand("DELETE FROM Inventories WHERE id = @id", database.GetSqlConnection());
                    deleteInv.Parameters.AddWithValue("@id", inventoryId);
                    deleteInv.ExecuteNonQuery();

                    LoadInventoryTabs(true);
                }
            });

            menu.Show(NK_tabControl1, location);
        }

        private string PromptForText(string title, string label, string defaultValue)
        {
            using (var form = new Form())
            {
                form.Text = title;
                form.StartPosition = FormStartPosition.CenterParent;
                form.Width = 360;
                form.Height = 160;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                var lbl = new Label() { Left = 12, Top = 15, Width = 320, Text = label };
                var txt = new TextBox() { Left = 12, Top = 40, Width = 320, Text = defaultValue };
                var btnOk = new Button() { Text = "OK", Left = 170, Width = 75, Top = 75, DialogResult = DialogResult.OK };
                var btnCancel = new Button() { Text = "Cancel", Left = 255, Width = 75, Top = 75, DialogResult = DialogResult.Cancel };

                form.Controls.Add(lbl);
                form.Controls.Add(txt);
                form.Controls.Add(btnOk);
                form.Controls.Add(btnCancel);
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                return form.ShowDialog() == DialogResult.OK ? txt.Text : null;
            }
        }

        // ---------- Existing inventory grid logic, now scoped to currentInventoryId ----------

        private void UpdateGrandTotal(DataGridView dgw)
        {
            int grandTotal = 0;
            foreach (DataGridViewRow row in dgw.Rows)
            {
                if (!row.Visible) continue;
                if (row.Cells["total_price"].Value != null)
                {
                    grandTotal += Convert.ToInt32(row.Cells["total_price"].Value);
                }
            }
            NK_lbl_GrandTotal.Text = $"Total: {grandTotal}";
        }

        private void IsAdmin()
        {
            NK_administrationToolStripMenuItem.Enabled = _permission.IsAdmin;
            NK_btn_Delete.Enabled = _permission.IsAdmin;
        }

        private void CreateColumns()
        {
            NK_dataGridView1.Columns.Add("id", "ID");
            NK_dataGridView1.Columns.Add("type_of", "Type");
            NK_dataGridView1.Columns.Add("count_of", "Quantity");
            NK_dataGridView1.Columns.Add("supplier", "Supplier");
            NK_dataGridView1.Columns.Add("price", "Price");
            NK_dataGridView1.Columns.Add("total_price", "Total Price");
            NK_dataGridView1.Columns.Add("IsNew", String.Empty);

            NK_dataGridView1.Columns["IsNew"].Visible = false;
            NK_dataGridView1.Columns["total_price"].ReadOnly = true;
        }

        private void ReadSingleRow(DataGridView dgw, IDataRecord record)
        {
            int quantity = record.GetInt32(2);
            int price = record.GetInt32(4);
            int totalPrice = quantity * price;

            dgw.Rows.Add(record.GetInt32(0), record.GetString(1), quantity, record.GetString(3), price, totalPrice, RowState.ModifiedNew);
        }

        private void RefreshDataGrid(DataGridView dgw)
        {
            dgw.Rows.Clear();

            if (currentInventoryId < 0) return;

            string queryString = "SELECT * FROM Inventory WHERE inventory_id = @invId";

            SqlCommand command = new SqlCommand(queryString, database.GetSqlConnection());
            command.Parameters.AddWithValue("@invId", currentInventoryId);

            database.openConnection();
            SqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                ReadSingleRow(dgw, reader);
            }
            reader.Close();
        }

        private void NK_Form_Inventory_Load(object sender, EventArgs e)
        {
            CreateColumns();
            NK_lbl_Permission.Text = $"{_permission.Login}: {_permission.Status}";
            IsAdmin();
            LoadInventoryTabs(true);
        }

        private void NK_dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            selectedRow = e.RowIndex;
            if (e.RowIndex >= 0)
            {
                DataGridViewRow row = NK_dataGridView1.Rows[selectedRow];

                NK_txt_ID.Text = row.Cells[0].Value.ToString();
                NK_txt_Type.Text = row.Cells[1].Value.ToString();
                NK_txt_Quantity.Text = row.Cells[2].Value.ToString();
                NK_txt_Supplier.Text = row.Cells[3].Value.ToString();
                NK_txt_Price.Text = row.Cells[4].Value.ToString();
            }
        }

        private void Search(DataGridView dgw)
        {
            dgw.Rows.Clear();

            if (currentInventoryId < 0) return;

            string searchString = "SELECT * FROM Inventory WHERE inventory_id = @invId AND CONCAT (id, type_of, count_of, supplier, price) LIKE @search";
            SqlCommand com = new SqlCommand(searchString, database.GetSqlConnection());
            com.Parameters.AddWithValue("@invId", currentInventoryId);
            com.Parameters.AddWithValue("@search", "%" + NK_txt_Search.Text + "%");

            database.openConnection();
            SqlDataReader read = com.ExecuteReader();

            while (read.Read())
            {
                ReadSingleRow(dgw, read);
            }
            read.Close();
        }

        private void NK_txt_Search_TextChanged(object sender, EventArgs e)
        {
            Search(NK_dataGridView1);
            UpdateGrandTotal(NK_dataGridView1);
        }

        private void deleteRow()
        {
            int index = NK_dataGridView1.CurrentCell.RowIndex;

            NK_dataGridView1.Rows[index].Visible = false;

            if (NK_dataGridView1.Rows[index].Cells[0].Value.ToString() == string.Empty)
            {
                NK_dataGridView1.Rows[index].Cells[6].Value = RowState.Deleted;
                return;
            }
            NK_dataGridView1.Rows[index].Cells[6].Value = RowState.Deleted;

            UpdateGrandTotal(NK_dataGridView1);
        }
        private void NK_btn_Delete_Click(object sender, EventArgs e)
        {
            deleteRow();
            ClearFields();
        }

        private void Update()
        {
            database.openConnection();

            for (int i = 0; i < NK_dataGridView1.Rows.Count; i++)
            {
                var rowState = (RowState)NK_dataGridView1.Rows[i].Cells[6].Value;
                if (rowState == RowState.Existed)
                    continue;

                if (rowState == RowState.Deleted)
                {
                    var id = Convert.ToInt32(NK_dataGridView1.Rows[i].Cells[0].Value);
                    var deleteQuery = "DELETE FROM Inventory WHERE id = @id";

                    var command = new SqlCommand(deleteQuery, database.GetSqlConnection());
                    command.Parameters.AddWithValue("@id", id);
                    command.ExecuteNonQuery();
                }

                if (rowState == RowState.Modified)
                {
                    var id = NK_dataGridView1.Rows[i].Cells[0].Value.ToString();
                    var type = NK_dataGridView1.Rows[i].Cells[1].Value.ToString();
                    var quantity = NK_dataGridView1.Rows[i].Cells[2].Value.ToString();
                    var supplier = NK_dataGridView1.Rows[i].Cells[3].Value.ToString();
                    var price = NK_dataGridView1.Rows[i].Cells[4].Value.ToString();

                    var changeQuery = "UPDATE Inventory SET type_of = @type, count_of = @quantity, supplier = @supplier, price = @price WHERE id = @id";
                    var command = new SqlCommand(changeQuery, database.GetSqlConnection());
                    command.Parameters.AddWithValue("@type", type);
                    command.Parameters.AddWithValue("@quantity", quantity);
                    command.Parameters.AddWithValue("@supplier", supplier);
                    command.Parameters.AddWithValue("@price", price);
                    command.Parameters.AddWithValue("@id", id);
                    command.ExecuteNonQuery();
                }
            }
            database.closeConnection();
        }
        private void NK_pic_Reload_Click(object sender, EventArgs e)
        {
            RefreshDataGrid(NK_dataGridView1);
            ClearFields();
            UpdateGrandTotal(NK_dataGridView1);
        }

        private void NK_btn_NewEntry_Click(object sender, EventArgs e)
        {
            NK_Form_Add form_Add = new NK_Form_Add(currentInventoryId);
            form_Add.Show();
        }

        private void NK_btn_Save_Click(object sender, EventArgs e)
        {
            Update();
            RefreshDataGrid(NK_dataGridView1);
            ClearFields();
            UpdateGrandTotal(NK_dataGridView1);
        }

        private void Change()
        {
            var selectedRowIndex = NK_dataGridView1.CurrentCell.RowIndex;

            var id = NK_txt_ID.Text;
            var type = NK_txt_Type.Text;
            var quantity = NK_txt_Quantity.Text;
            var supplier = NK_txt_Supplier.Text;
            int price;

            if (NK_dataGridView1.Rows[selectedRowIndex].Cells[0].Value.ToString() != string.Empty)
            {
                if (int.TryParse(NK_txt_Price.Text, out price))
                {
                    int totalPrice = Convert.ToInt32(quantity) * price;
                    NK_dataGridView1.Rows[selectedRowIndex].SetValues(id, type, quantity, supplier, price, totalPrice);
                    NK_dataGridView1.Rows[selectedRowIndex].Cells[6].Value = RowState.Modified;
                    UpdateGrandTotal(NK_dataGridView1);
                }
                else
                {
                    MessageBox.Show("Price must be in numeric format !", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

        }

        private void NK_btn_Change_Click(object sender, EventArgs e)
        {
            Change();

        }
        private void ClearFields()
        {
            NK_txt_ID.Text = "";
            NK_txt_Type.Text = "";
            NK_txt_Quantity.Text = "";
            NK_txt_Supplier.Text = "";
            NK_txt_Price.Text = "";
        }

        private void NK_administrationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NK_Form_Administration form_Administration = new NK_Form_Administration();
            form_Administration.Show();

        }
        private void NK_pic_Clear_Click(object sender, EventArgs e)
        {
            ClearFields();
        }
        private void NK_lbl_Price_Click(object sender, EventArgs e)
        {

        }

        private void NK_tabPage1_Click(object sender, EventArgs e)
        {

        }


    }
}