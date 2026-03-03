using LigthStringEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LSEGui
{
    public partial class LSEGUI : Form
    {
        DatTL Editor;
        string[] Strings;
        int malieLabelCount = 0;
        int filteredMalieLabelCount = 0;
        string currentFilePath = "";

        // 검색 상태
        int searchCurrentIndex = -1;
        string lastSearchQuery = "";

        public LSEGUI()
        {
            InitializeComponent();

            chkFilter.CheckedChanged += chkFilter_CheckedChanged;
            txtSearch.KeyDown += txtSearch_KeyDown;
            btnSearch.Click += btnSearch_Click;

            MessageBox.Show("LightStringEditor v2.0\n\nMALIE LABEL: 읽기 전용 (복사 가능)\nSTRING TABLE: 편집 가능", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void chkFilter_CheckedChanged(object sender, EventArgs e)
        {
            if (Editor != null && !string.IsNullOrEmpty(currentFilePath))
                LoadFile(currentFilePath);
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog filed = new OpenFileDialog();
            filed.Filter = "EXEC Files (*.dat;*.bin)|*.dat;*.bin|All Files (*.*)|*.*";
            filed.Title = "Open EXEC File";

            if (filed.ShowDialog() == DialogResult.OK)
            {
                currentFilePath = filed.FileName;
                LoadFile(currentFilePath);
            }
        }

        private void LoadFile(string filePath)
        {
            byte[] File = System.IO.File.ReadAllBytes(filePath);
            Editor = new DatTL(File);
            Editor.FilterEnabled = chkFilter.Checked;
            Strings = Editor.Import();

            malieLabelCount = Editor.MalieLabelCount;
            filteredMalieLabelCount = Editor.FilteredMalieLabelCount;

            listBox1.Items.Clear();
            if (filteredMalieLabelCount > 0)
            {
                string[] malieLabels = Strings.Take(filteredMalieLabelCount).ToArray();
                listBox1.Items.AddRange(malieLabels);
                if (listBox1.Items.Count > 0)
                    listBox1.SelectedIndex = 0;
            }

            listBox2.Items.Clear();
            if (Strings.Length > filteredMalieLabelCount)
            {
                string[] stringTable = Strings.Skip(filteredMalieLabelCount).ToArray();
                listBox2.Items.AddRange(stringTable);
                if (listBox2.Items.Count > 0)
                    listBox2.SelectedIndex = 0;
            }

            string filterStatus = chkFilter.Checked ? " (필터 ON)" : " (필터 OFF)";
            this.Text = $"LightStringEditor v2.0 - {Path.GetFileName(filePath)} (MALIE: {filteredMalieLabelCount}/{malieLabelCount}, STRINGS: {Strings.Length - filteredMalieLabelCount}){filterStatus}";

            // 검색 상태 초기화
            searchCurrentIndex = -1;
            lastSearchQuery = "";
        }

        // ========== MALIE LABEL (Tab 1) - 읽기 전용 ==========
        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                int i = listBox1.SelectedIndex;
                if (i >= 0 && i < listBox1.Items.Count)
                {
                    textBox1.Text = listBox1.Items[i].ToString();
                    textBox1.ReadOnly = true;
                    this.Text = $"MALIE LABEL (읽기 전용) - ID: {i}/{listBox1.Items.Count}";
                }
            }
            catch { }
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                if (!string.IsNullOrEmpty(textBox1.Text))
                    Clipboard.SetText(textBox1.Text);
                e.Handled = true;
            }
        }

        // ========== STRING TABLE (Tab 2) - 편집 가능 ==========
        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                int i = listBox2.SelectedIndex;
                if (i >= 0 && i < listBox2.Items.Count)
                {
                    textBox2.Text = listBox2.Items[i].ToString();
                    textBox2.ReadOnly = false;
                    this.Text = $"STRING TABLE (편집 가능) - ID: {i}/{listBox2.Items.Count}";
                }
            }
            catch { }
        }

        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\n' || e.KeyChar == '\r')
            {
                try
                {
                    int selectedIndex = listBox2.SelectedIndex;
                    if (selectedIndex >= 0 && selectedIndex < listBox2.Items.Count)
                    {
                        listBox2.Items[selectedIndex] = textBox2.Text;
                        Strings[filteredMalieLabelCount + selectedIndex] = textBox2.Text;

                        if (selectedIndex < listBox2.Items.Count - 1)
                            listBox2.SelectedIndex = selectedIndex + 1;
                    }
                }
                catch { }
                e.Handled = true;
            }
        }

        private void textBox2_KeyDown(object sender, KeyEventArgs e)
        {
            // ✅ 버그 수정: Ctrl+V 커스텀 핸들러 제거
            // 이전: textBox2.Text = Clipboard.GetText() 수동 설정 + TextBox 기본 붙여넣기가
            // 동시에 실행되어 텍스트가 중복으로 들어가는 버그가 있었음.
            // TextBox는 Ctrl+V를 자체 처리하므로 커스텀 핸들러 불필요.

            if (e.Control && e.KeyCode == Keys.C)
            {
                if (!string.IsNullOrEmpty(textBox2.Text))
                    Clipboard.SetText(textBox2.Text);
                e.SuppressKeyPress = true;
            }
        }

        // ========== 검색 기능 ==========
        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                DoSearch();
            }
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            DoSearch();
        }

        private void DoSearch()
        {
            string query = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(query)) return;

            // 현재 활성 탭 기준으로 검색 대상 결정
            ListBox targetList = (tabControl1.SelectedIndex == 0) ? listBox1 : listBox2;
            if (targetList.Items.Count == 0) return;

            // 검색어가 바뀌면 처음부터 다시 검색
            if (!query.Equals(lastSearchQuery, StringComparison.OrdinalIgnoreCase))
            {
                searchCurrentIndex = -1;
                lastSearchQuery = query;
            }

            int startFrom = searchCurrentIndex + 1;
            int found = -1;

            // 현재 위치 이후 검색
            for (int i = startFrom; i < targetList.Items.Count; i++)
            {
                if (targetList.Items[i].ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = i;
                    break;
                }
            }

            // 끝까지 못 찾으면 처음부터 랩어라운드
            if (found == -1 && startFrom > 0)
            {
                for (int i = 0; i < startFrom; i++)
                {
                    if (targetList.Items[i].ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        found = i;
                        break;
                    }
                }
            }

            if (found >= 0)
            {
                searchCurrentIndex = found;
                targetList.SelectedIndex = found;
                targetList.TopIndex = found;
            }
            else
            {
                searchCurrentIndex = -1;
                MessageBox.Show($"'{query}' 를 찾을 수 없습니다.", "검색 결과 없음",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ========== 저장 ==========
        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog filed = new SaveFileDialog();
            filed.Filter = "EXEC Files (*.dat;*.bin)|*.dat;*.bin|All Files (*.*)|*.*";
            filed.Title = "Save EXEC File";
            filed.DefaultExt = "dat";
            filed.AddExtension = true;

            if (filed.ShowDialog() == DialogResult.OK)
            {
                List<string> finalStrings = new List<string>();

                for (int i = 0; i < listBox1.Items.Count; i++)
                    finalStrings.Add(listBox1.Items[i].ToString());

                for (int i = 0; i < listBox2.Items.Count; i++)
                    finalStrings.Add(listBox2.Items[i].ToString());

                Strings = finalStrings.ToArray();

                byte[] Script = Editor.Export(Strings);
                System.IO.File.WriteAllBytes(filed.FileName, Script);
                MessageBox.Show($"File Saved: {Path.GetFileName(filed.FileName)}\n\nMALIE LABEL: {listBox1.Items.Count}\nSTRING TABLE: {listBox2.Items.Count}",
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}