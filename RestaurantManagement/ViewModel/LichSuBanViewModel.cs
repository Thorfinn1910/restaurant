using LichSuBan.Models;
using Microsoft.Office.Interop.Excel;
using QuanLyNhaHang.DataProvider;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;

namespace QuanLyNhaHang.ViewModel
{
    public class LichSuBanViewModel : BaseViewModel
    {
        private bool isGettingSource;
        public bool IsGettingSource
        {
            get => isGettingSource;
            set { isGettingSource = value; OnPropertyChanged(); }
        }

        private DateTime _getCurrentDate;
        public DateTime GetCurrentDate
        {
            get => _getCurrentDate;
            set { _getCurrentDate = value; }
        }

        private string _setCurrentDate;
        public string SetCurrentDate
        {
            get => _setCurrentDate;
            set { _setCurrentDate = value; }
        }

        private DateTime selectedDate;
        public DateTime SelectedDate
        {
            get => selectedDate;
            set { selectedDate = value; OnPropertyChanged(); }
        }

        private ComboBoxItem _SelectedItemFilter;
        public ComboBoxItem SelectedItemFilter
        {
            get => _SelectedItemFilter;
            set { _SelectedItemFilter = value; OnPropertyChanged(); }
        }

        private ComboBoxItem _SelectedImportItemFilter;
        public ComboBoxItem SelectedImportItemFilter
        {
            get => _SelectedImportItemFilter;
            set { _SelectedImportItemFilter = value; OnPropertyChanged(); }
        }

        private int _SelectedMonth;
        public int SelectedMonth
        {
            get => _SelectedMonth;
            set { _SelectedMonth = value; OnPropertyChanged(); }
        }

        private int _SelectedImportMonth;
        public int SelectedImportMonth
        {
            get => _SelectedImportMonth;
            set { _SelectedImportMonth = value; OnPropertyChanged(); }
        }

        private System.Windows.Controls.Label _ResultName;
        public System.Windows.Controls.Label ResultName
        {
            get => _ResultName;
            set { _ResultName = value; OnPropertyChanged(); }
        }

        private ObservableCollection<LichSuBanModel> _ListProduct;
        public ObservableCollection<LichSuBanModel> ListProduct { get => _ListProduct; set { _ListProduct = value; OnPropertyChanged(); } }

        private string _Search;
        public string Search
        {
            get => _Search;
            set
            {
                _Search = value;
                OnPropertyChanged();
                if (string.IsNullOrWhiteSpace(Search))
                {
                    LoadAllHistory();
                }
                else
                {
                    ListProduct = LichSuBanDP.Flag.SearchSalesHistoryByDishName(Search);
                }
            }
        }

        public ICommand ExportFileCM { get; set; }
        public ICommand CheckImportItemFilterCM { get; set; }
        public ICommand SelectedImportMonthCM { get; set; }
        public ICommand SelectedMonthCM { get; set; }
        public ICommand CheckCM { get; set; }
        public ICommand SelectedDateExportListCM { get; set; }
        public ICommand CheckItemFilterCM { get; set; }

        public LichSuBanViewModel()
        {
            ListProduct = new ObservableCollection<LichSuBanModel>();
            LoadAllHistory();

            GetCurrentDate = DateTime.Today;
            SelectedDate = GetCurrentDate;
            SelectedMonth = DateTime.Now.Month - 1;
            SelectedImportMonth = DateTime.Now.Month - 1;

            SelectedDateExportListCM = new RelayCommand<System.Windows.Controls.DatePicker>((p) => true, (p) =>
            {
                CheckDateFilter();
            });

            SelectedMonthCM = new RelayCommand<System.Windows.Controls.ComboBox>((p) => true, (p) =>
            {
                CheckMonthFilter();
            });

            CheckCM = new RelayCommand<object>((p) => true, (p) =>
            {
                MyMessageBox mess = new MyMessageBox("Kiểm tra");
                mess.ShowDialog();
            });

            CheckItemFilterCM = new RelayCommand<System.Windows.Controls.ComboBox>((p) => true, (p) =>
            {
                CheckItemFilter();
            });

            ExportFileCM = new RelayCommand<object>((p) => true, (p) =>
            {
                ExportToFileFunc();
            });
        }

        public void ExportToFileFunc()
        {
            using (SaveFileDialog sfd = new SaveFileDialog() { Filter = "Excel Workbook|*.xlsx", ValidateNames = true })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                    Microsoft.Office.Interop.Excel.Application app = new Microsoft.Office.Interop.Excel.Application();
                    app.Visible = false;
                    Workbook wb = app.Workbooks.Add(XlSheetType.xlWorksheet);
                    Worksheet ws = (Worksheet)app.ActiveSheet;

                    ws.Cells[1, 1] = "Số hóa đơn";
                    ws.Cells[1, 2] = "Tên sản phẩm";
                    ws.Cells[1, 3] = "Số lượng";
                    ws.Cells[1, 4] = "Thành tiền(VNĐ)";
                    ws.Cells[1, 5] = "Ngày nhập";

                    int i2 = 2;
                    foreach (LichSuBanModel item in ListProduct)
                    {
                        ws.Cells[i2, 1] = item.SoHD;
                        ws.Cells[i2, 2] = item.TenMon;
                        ws.Cells[i2, 3] = item.SoLuong;
                        ws.Cells[i2, 4] = item.TriGia;
                        ws.Cells[i2, 5] = item.ngayHD;
                        i2++;
                    }

                    ws.SaveAs(
                        sfd.FileName,
                        XlFileFormat.xlWorkbookDefault,
                        Type.Missing,
                        true,
                        false,
                        XlSaveAsAccessMode.xlNoChange,
                        XlSaveConflictResolution.xlLocalSessionChanges,
                        Type.Missing,
                        Type.Missing);

                    app.Quit();
                    Mouse.OverrideCursor = System.Windows.Input.Cursors.Arrow;

                    MyMessageBox mb = new MyMessageBox("Xuất file thành công");
                    mb.ShowDialog();
                }
            }
        }

        public void CheckMonthFilter()
        {
            ListProduct = LichSuBanDP.Flag.GetSalesHistoryByMonth(SelectedMonth + 1);
        }

        public void CheckDateFilter()
        {
            ListProduct = LichSuBanDP.Flag.GetSalesHistoryByDate(SelectedDate);
        }

        public void CheckItemFilter()
        {
            string? filter = SelectedItemFilter?.Content?.ToString();
            switch (filter)
            {
                case "Toàn bộ":
                    LoadAllHistory();
                    return;
                case "Theo ngày":
                    CheckDateFilter();
                    return;
                case "Theo tháng":
                    CheckMonthFilter();
                    return;
                default:
                    LoadAllHistory();
                    return;
            }
        }

        private void LoadAllHistory()
        {
            ListProduct = LichSuBanDP.Flag.GetAllSalesHistory();
        }
    }
}
