using iTextSharp.text;
using iTextSharp.text.pdf;
using QuanLyNhaHang.DataProvider;
using QuanLyNhaHang.Models;
using QuanLyNhaHang.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Forms;
using System.Windows.Input;
using TinhTrangBan.Models;

namespace QuanLyNhaHang.ViewModel
{
    public class TinhTrangBanViewModel : BaseViewModel
    {
        public TinhTrangBanViewModel()
        {
            StatusOfTableCommand = new RelayCommand<Table>((p) => true, (p) => GetStatusOfTable(p.ID));
            GetPaymentCommand = new RelayCommand<Table>((p) => true, (p) => Payment());
            GetSwitchTableCommand = new RelayCommand<string>((p) => true, (p) => SwitchTable());
            LoadTables();
            LoadTableStatus();
            LoadEmptyTables();
        }

        #region attributes
        private readonly ObservableCollection<Table> _tables = new ObservableCollection<Table>();
        private ObservableCollection<SelectedMenuItems> _selectedItems = new ObservableCollection<SelectedMenuItems>();
        private ObservableCollection<string> _emptytables = new ObservableCollection<string>();
        private string titleofbill = "";
        private decimal dec_sumofbill = 0;
        private string sumofbill = MoneyFormatter.FormatVnd(0);
        private string selectedtable = "";
        private int IDofPaidTable = 0;
        #endregion

        #region properties
        public ObservableCollection<Table> Tables { get { return _tables; } set { OnPropertyChanged(); } }
        public ObservableCollection<SelectedMenuItems> SelectedItems { get { return _selectedItems; } set { _selectedItems = value; } }
        public ObservableCollection<string> EmptyTables { get { return _emptytables; } set { _emptytables = value; OnPropertyChanged(); } }
        public string TitleOfBill
        {
            get { return titleofbill; }
            set { titleofbill = value; OnPropertyChanged(); }
        }
        public decimal Dec_sumofbill
        {
            get { return dec_sumofbill; }
            set { dec_sumofbill = value; OnPropertyChanged(); }
        }
        public string SumofBill
        {
            get { return sumofbill; }
            set { sumofbill = value; OnPropertyChanged(); }
        }
        public string SelectedTable
        {
            get { return selectedtable; }
            set { selectedtable = value; OnPropertyChanged(); }
        }
        #endregion

        #region commands
        public ICommand StatusOfTableCommand { get; set; }
        public ICommand GetPaymentCommand { get; set; }
        public ICommand GetSwitchTableCommand { get; set; }
        #endregion

        #region methods
        public void LoadTables()
        {
            _tables.Clear();
            for (int tableId = 1; tableId <= 15; tableId++)
            {
                _tables.Add(new Table { NumOfTable = "B\u00e0n " + tableId, ID = tableId });
            }
        }

        public void LoadEmptyTables()
        {
            EmptyTables = TinhTrangBanDP.Flag.GetEmptyTables();
        }

        public void LoadTableStatus()
        {
            foreach (Table table in _tables)
            {
                string tableStatus = TinhTrangBanDP.Flag.LoadEachTableStatus(table.ID);
                if (TinhTrangBanDP.Flag.IsTableAvailableStatus(tableStatus))
                {
                    table.Status = 0;
                    table.Coloroftable = "Green";
                }
                else
                {
                    table.Status = 1;
                    table.Coloroftable = "Red";
                }
            }
        }

        public void DisplayBill(int billID)
        {
            SelectedItems.Clear();
            Dec_sumofbill = 0;
            SumofBill = MoneyFormatter.FormatVnd(Dec_sumofbill);
            if (billID <= 0)
            {
                return;
            }

            List<TinhTrangBanDP.BillItemRow> billItems = TinhTrangBanDP.Flag.GetBillItems(billID);
            foreach (TinhTrangBanDP.BillItemRow item in billItems)
            {
                SelectedItems.Add(new SelectedMenuItems(item.TenMon, item.ThanhTien, item.SoLuong));
                Dec_sumofbill += item.ThanhTien;
            }

            SumofBill = MoneyFormatter.FormatVnd(Dec_sumofbill);
        }

        public void GetStatusOfTable(int ID)
        {
            foreach (Table table in _tables)
            {
                if (table.ID == ID)
                {
                    if (table.Status == 0)
                    {
                        table.Coloroftable = "Green";
                        table.Status = 0;
                    }
                    else
                    {
                        TitleOfBill = table.NumOfTable;
                        table.Bill_ID = TinhTrangBanDP.Flag.LoadBill(table.ID);
                        DisplayBill(table.Bill_ID);
                        IDofPaidTable = table.ID;
                    }
                    break;
                }
            }
        }

        public void PrintBill(int billID, int tableID)
        {
            List<TinhTrangBanDP.BillItemRow> billItems = TinhTrangBanDP.Flag.GetBillItems(billID);
            if (billItems.Count == 0)
            {
                MyMessageBox mess = new MyMessageBox("Kh\u00f4ng t\u1ed3n t\u1ea1i h\u00f3a \u0111\u01a1n!");
                mess.ShowDialog();
                return;
            }

            DisplayBill(billID);
            MyMessageBox yesno = new MyMessageBox("B\u1ea1n c\u00f3 mu\u1ed1n in h\u00f3a \u0111\u01a1n?", true);
            yesno.ShowDialog();
            if (!yesno.ACCEPT())
            {
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "PDF (*.pdf)|*.pdf";
            sfd.FileName = "Ma hoa don " + billID + " ngay " + DateTime.Now.Day + "-" + DateTime.Now.Month + "-" + DateTime.Now.Year;
            if (sfd.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            if (File.Exists(sfd.FileName))
            {
                try
                {
                    File.Delete(sfd.FileName);
                }
                catch (IOException)
                {
                    MyMessageBox msb = new MyMessageBox("\u0110\u00e3 c\u00f3 l\u1ed7i x\u1ea3y ra!");
                    msb.ShowDialog();
                    return;
                }
            }

            try
            {
                PdfPTable pdfTable = new PdfPTable(3);
                pdfTable.DefaultCell.Padding = 3;
                pdfTable.WidthPercentage = 100;
                pdfTable.HorizontalAlignment = Element.ALIGN_MIDDLE;

                BaseFont bf = BaseFont.CreateFont(Environment.GetEnvironmentVariable("windir") + @"\fonts\TIMES.TTF", BaseFont.IDENTITY_H, true);
                Font f = new Font(bf, 16, Font.NORMAL);

                pdfTable.AddCell(new PdfPCell(new Phrase("T\u00ean m\u00f3n", f)));
                pdfTable.AddCell(new PdfPCell(new Phrase("S\u1ed1 l\u01b0\u1ee3ng", f)));
                pdfTable.AddCell(new PdfPCell(new Phrase("Gi\u00e1", f)));
                foreach (TinhTrangBanDP.BillItemRow item in billItems)
                {
                    pdfTable.AddCell(new Phrase(item.TenMon, f));
                    pdfTable.AddCell(new Phrase(item.SoLuong.ToString(), f));
                    pdfTable.AddCell(new Phrase(MoneyFormatter.FormatVnd(item.ThanhTien), f));
                }

                using (FileStream stream = new FileStream(sfd.FileName, FileMode.Create))
                {
                    Document pdfDoc = new Document(PageSize.A4, 50f, 50f, 40f, 40f);
                    PdfWriter.GetInstance(pdfDoc, stream);
                    pdfDoc.Open();
                    pdfDoc.Add(new Paragraph("                                                 HOA DON ", f));
                    pdfDoc.Add(new Paragraph("    "));
                    pdfDoc.Add(new Paragraph("So ban: " + tableID + "                                                                    Ma hoa don: " + billID, f));
                    pdfDoc.Add(new Paragraph("Thoi gian: " + DateTime.Now.Day + "/" + DateTime.Now.Month + "/" + DateTime.Now.Year + " " + DateTime.Now.TimeOfDay, f));
                    pdfDoc.Add(new Paragraph("    "));
                    pdfDoc.Add(pdfTable);
                    pdfDoc.Add(new Paragraph("Tong cong:                                                                    " + SumofBill, f));
                    pdfDoc.Add(new Paragraph("    "));
                    pdfDoc.Add(new Paragraph("                                      HEN GAP LAI QUY KHACH", f));
                    pdfDoc.Close();
                }

                MyMessageBox mess = new MyMessageBox("In thanh cong!");
                mess.ShowDialog();
            }
            catch
            {
                MyMessageBox msb = new MyMessageBox("\u0110\u00e3 c\u00f3 l\u1ed7i x\u1ea3y ra!");
                msb.ShowDialog();
            }
        }

        public void Payment()
        {
            if (IDofPaidTable == 0)
            {
                MyMessageBox msb = new MyMessageBox("Vui long chon ban can thanh toan!");
                msb.Show();
                return;
            }

            foreach (Table table in _tables)
            {
                if (table.ID != IDofPaidTable)
                {
                    continue;
                }

                try
                {
                    if (table.Bill_ID <= 0)
                    {
                        table.Bill_ID = TinhTrangBanDP.Flag.LoadBill(table.ID);
                    }

                    TinhTrangBanDP.Flag.PayBillTransactional(table.ID, table.Bill_ID);
                    table.Coloroftable = "Green";
                    table.Status = 0;
                    PrintBill(table.Bill_ID, table.ID);
                    ResetSelectionState();
                    LoadTableStatus();
                    LoadEmptyTables();
                    MyMessageBox msb = new MyMessageBox("Da thanh toan thanh cong!");
                    msb.Show();
                }
                catch (Exception ex)
                {
                    MyMessageBox msb = new MyMessageBox(ex.Message);
                    msb.Show();
                }
                break;
            }
        }

        public void SwitchTable()
        {
            if (IDofPaidTable == 0)
            {
                MyMessageBox msb = new MyMessageBox("Vui long chon 1 ban can chuyen truoc!");
                msb.Show();
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedTable))
            {
                MyMessageBox msb = new MyMessageBox("Vui long chon ban de chuyen den trong danh sach ban trong!");
                msb.Show();
                return;
            }

            if (!int.TryParse(SelectedTable, out int targetTableId))
            {
                MyMessageBox msb = new MyMessageBox("Ban chuyen den khong hop le!");
                msb.Show();
                return;
            }

            foreach (Table table in _tables)
            {
                if (table.ID != IDofPaidTable)
                {
                    continue;
                }

                try
                {
                    if (table.Bill_ID <= 0)
                    {
                        table.Bill_ID = TinhTrangBanDP.Flag.LoadBill(table.ID);
                    }

                    TinhTrangBanDP.Flag.SwitchTableTransactional(table.ID, targetTableId, table.Bill_ID);
                    table.Coloroftable = "Green";
                    table.Status = 0;
                    ResetSelectionState();
                    LoadTableStatus();
                    LoadEmptyTables();
                    MyMessageBox msb = new MyMessageBox("Da chuyen ban thanh cong!");
                    msb.Show();
                }
                catch (Exception ex)
                {
                    MyMessageBox msb = new MyMessageBox(ex.Message);
                    msb.Show();
                }
                break;
            }
        }

        private void ResetSelectionState()
        {
            Dec_sumofbill = 0;
            SumofBill = MoneyFormatter.FormatVnd(Dec_sumofbill);
            SelectedItems.Clear();
            TitleOfBill = "";
            IDofPaidTable = 0;
        }
        #endregion
    }
}
