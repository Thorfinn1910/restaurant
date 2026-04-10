using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using LichSuBan.Models;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Data;
using System.Configuration;
using Microsoft.Office.Interop.Excel;
using Microsoft.Win32;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using OfficeOpenXml.Table;
using LicenseContext = OfficeOpenXml.LicenseContext;
using System.IO;
using System.Windows;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;
using DataTable = System.Data.DataTable;
using System.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Security.Cryptography;
using MaterialDesignThemes.Wpf;
using System.Windows.Documents;
using System.Security.Permissions;

namespace QuanLyNhaHang.ViewModel
{
    public class LichSuBanViewModel : BaseViewModel
    {
        private bool isGettingSource;
        public bool IsGettingSource
        {
            get { return isGettingSource; }
            set { isGettingSource = value; OnPropertyChanged(); }
        }

        private DateTime _getCurrentDate;
        public DateTime GetCurrentDate
        {
            get { return _getCurrentDate; }
            set { _getCurrentDate = value; }
        }
        private string _setCurrentDate;
        public string SetCurrentDate
        {
            get { return _setCurrentDate; }
            set { _setCurrentDate = value; }
        }

        private DateTime selectedDate;
        public DateTime SelectedDate
        {
            get { return selectedDate; }
            set { selectedDate = value; OnPropertyChanged(); }
        }
        private ComboBoxItem _SelectedItemFilter;
        public ComboBoxItem SelectedItemFilter
        {
            get { return _SelectedItemFilter; }
            set { _SelectedItemFilter = value; OnPropertyChanged(); }
        }
        private ComboBoxItem _SelectedImportItemFilter;
        public ComboBoxItem SelectedImportItemFilter
        {
            get { return _SelectedImportItemFilter; }
            set { _SelectedImportItemFilter = value; OnPropertyChanged(); }
        }
        private int _SelectedMonth;
        public int SelectedMonth
        {
            get { return _SelectedMonth; }
            set { _SelectedMonth = value; OnPropertyChanged(); }
        }

        private int _SelectedImportMonth;
        public int SelectedImportMonth
        {
            get { return _SelectedImportMonth; }
            set { _SelectedImportMonth = value; OnPropertyChanged(); }
        }
        private System.Windows.Controls.Label _ResultName;
        public System.Windows.Controls.Label ResultName
        {
            get { return _ResultName; }
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
                string strQuery;
                OnPropertyChanged();
                if (!String.IsNullOrEmpty(Search))
                {
                    strQuery = "select ct.SoHD, mn.MaMon, TenMon, SoLuong, mn.Gia, NgayHD from HOADON hd join CTHD ct on hd.SoHD = ct.SoHD join MENU mn on ct.MaMon = mn.MaMon WHERE TenMon LIKE N'%" + Search + "%'";
                  
                }
                else
                    strQuery = "select ct.SoHD, mn.MaMon, TenMon, SoLuong, mn.Gia, NgayHD from HOADON hd join CTHD ct on hd.SoHD = ct.SoHD join MENU mn on ct.MaMon = mn.MaMon";
                ListViewDisplay(strQuery);
            }
        }
        private string strCon = ConfigurationManager.ConnectionStrings["QuanLyNhaHang"].ConnectionString;
        private SqlConnection sqlCon = null;


        
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


            ListViewDisplay("select ct.SoHD, mn.MaMon, TenMon, SoLuong, mn.Gia, NgayHD from HOADON hd join CTHD ct on hd.SoHD = ct.SoHD join MENU mn on ct.MaMon = mn.MaMon");
            OpenConnect();

            GetCurrentDate = DateTime.Today;
            SelectedDate = GetCurrentDate;
            SelectedMonth = DateTime.Now.Month - 1;
            SelectedImportMonth = DateTime.Now.Month - 1;
            SelectedDateExportListCM = new RelayCommand<System.Windows.Controls.DatePicker>((p) => { return true; }, (p) =>
            {
                CheckDateFilter();
            });
            
            SelectedMonthCM = new RelayCommand<System.Windows.Controls.ComboBox>((p) => { return true; }, (p) =>
            {
                  CheckMonthFilter();
            });
            CheckCM = new RelayCommand<object>((p) => { return true; }, (p) =>
            {
                MyMessageBox mess = new MyMessageBox("Kiểm tra");
                mess.ShowDialog();

            });
            CheckItemFilterCM = new RelayCommand<System.Windows.Controls.ComboBox>((p) => { return true; }, (p) =>
            {
                 CheckItemFilter();
            });
            ExportFileCM = new RelayCommand<object>((p) => { return true; }, (p) =>
            {
                ExportToFileFunc();
            });
            CloseConnect();
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
                    foreach (var item in ListProduct)
                    {

                        ws.Cells[i2, 1] = item.SoHD;
                        ws.Cells[i2, 2] = item.TenMon;
                        ws.Cells[i2, 3] = item.SoLuong;
                        ws.Cells[i2, 4] = item.TriGia;
                        ws.Cells[i2, 5] = item.ngayHD;


                        i2++;
                    }
                    ws.SaveAs(sfd.FileName, XlFileFormat.xlWorkbookDefault, Type.Missing, true, false, XlSaveAsAccessMode.xlNoChange, XlSaveConflictResolution.xlLocalSessionChanges, Type.Missing, Type.Missing);

                    app.Quit();

                    Mouse.OverrideCursor = System.Windows.Input.Cursors.Arrow;

                    MyMessageBox mb = new MyMessageBox("Xuất file thành công");
                    mb.ShowDialog();
                }
            }

        }
        private void OpenConnect()
        {
            if (sqlCon == null)
            {
                sqlCon = new SqlConnection(strCon);
            }

            if (sqlCon.State == ConnectionState.Closed)
            {
                sqlCon.Open();
            }
        }

        private void CloseConnect()
        {
            if (sqlCon.State == ConnectionState.Open)
            {
                sqlCon.Close();
            }
        }
        private void ListViewDisplay(string strQuery)
        {
            OpenConnect();

            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = strQuery;
            cmd.Connection = sqlCon;
            SqlDataReader reader = cmd.ExecuteReader();
            ListProduct.Clear();
            while (reader.Read())
            {
                int madon = reader.GetInt16(0);
                string mamon = reader.GetString(1);
                string ten = reader.GetString(2);
                int soluong = reader.GetInt16(3);
                string gia = (reader.GetSqlMoney(4) * soluong).ToString();
                string thoigian = reader.GetDateTime(5).ToShortDateString();

                ListProduct.Add(new LichSuBanModel(madon, mamon, ten, soluong, gia, thoigian));
            }

            CloseConnect();
        }
        
        public void  CheckMonthFilter()
        {
            ListProduct = new ObservableCollection<LichSuBanModel>();
           
            OpenConnect();
       


            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = "select ct.SoHD, mn.MaMon, TenMon, SoLuong, mn.Gia, NgayHD from HOADON hd join CTHD ct on hd.SoHD = ct.SoHD join MENU mn on ct.MaMon = mn.MaMon where MONTH(NgayHD) = '" + (SelectedMonth + 1 ) + "'";

            cmd.Connection = sqlCon;
            SqlDataReader reader = cmd.ExecuteReader();
            ListProduct.Clear();
            while (reader.Read())
            {
                int madon = reader.GetInt16(0);
                string mamon = reader.GetString(1);
                string ten = reader.GetString(2);
                int soluong = reader.GetInt16(3);
                string gia = (reader.GetSqlMoney(4) * soluong).ToString();
                string thoigian = reader.GetDateTime(5).ToShortDateString();

                ListProduct.Add(new LichSuBanModel(madon, mamon, ten, soluong, gia, thoigian));
            }

            CloseConnect();
            return;
        }
        public void CheckDateFilter()
        {
            ListProduct = new ObservableCollection<LichSuBanModel>();
            OpenConnect();

            DateTime dateToday = SelectedDate;

            string strDate = dateToday.ToString("yyyy-MM-dd");

            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = "select ct.SoHD, mn.MaMon, TenMon, SoLuong, mn.Gia, NgayHD from HOADON hd join CTHD ct on hd.SoHD = ct.SoHD join MENU mn on ct.MaMon = mn.MaMon where CONVERT(date, NgayHD, 103) = '" + strDate + "'";

            cmd.Connection = sqlCon;
            SqlDataReader reader = cmd.ExecuteReader();
            ListProduct.Clear();
            while (reader.Read())
            {
                int madon = reader.GetInt16(0);
                string mamon = reader.GetString(1);
                string ten = reader.GetString(2);
                int soluong = reader.GetInt16(3);
                string gia = (reader.GetSqlMoney(4) * soluong).ToString();
                string thoigian = reader.GetDateTime(5).ToShortDateString();

                ListProduct.Add(new LichSuBanModel(madon, mamon, ten, soluong, gia, thoigian));
            }

            CloseConnect();

        }
        public void CheckItemFilter()
        {
            
            ListProduct = new ObservableCollection<LichSuBanModel>();
            switch (SelectedItemFilter.Content.ToString())
            {
                case "Toàn bộ":
                    {
                        ListViewDisplay("select ct.SoHD, mn.MaMon, TenMon, SoLuong, mn.Gia, NgayHD from HOADON hd join CTHD ct on hd.SoHD = ct.SoHD join MENU mn on ct.MaMon = mn.MaMon");
                        OpenConnect();
                        CloseConnect();

                        return;
                    }
                case "Theo ngày":
                    {
                        OpenConnect();
                        DateTime dateToday = SelectedDate;

                        string strDate = dateToday.ToString("yyyy-MM-dd");

                     
                        SqlCommand cmd = new SqlCommand();
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandText = "select ct.SoHD, mn.MaMon, TenMon, SoLuong, mn.Gia, NgayHD from HOADON hd join CTHD ct on hd.SoHD = ct.SoHD join MENU mn on ct.MaMon = mn.MaMon where NgayHD = '" + strDate + "'";
                      
                        cmd.Connection = sqlCon;
                        SqlDataReader reader = cmd.ExecuteReader();
                        ListProduct.Clear();
                        while (reader.Read())
                        {
                            int madon = reader.GetInt32(0);
                            string mamon = reader.GetString(1);
                            string ten = reader.GetString(2);
                            int soluong = reader.GetInt32(3);
                            string gia = (reader.GetSqlMoney(4) * soluong).ToString();
                            string thoigian = reader.GetDateTime(5).ToShortDateString();

                            ListProduct.Add(new LichSuBanModel(madon, mamon, ten, soluong, gia, thoigian));
                        }

                        CloseConnect();
                        return;
                    }
                case "Theo tháng":
                    {

                        OpenConnect();
                        SqlCommand cmd = new SqlCommand();
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandText = "select ct.SoHD, mn.MaMon, TenMon, SoLuong, mn.Gia, NgayHD from HOADON hd join CTHD ct on hd.SoHD = ct.SoHD join MENU mn on ct.MaMon = mn.MaMon where MONTH(NgayHD) = '" + (SelectedMonth + 1) + "'";

                        cmd.Connection = sqlCon;
                        SqlDataReader reader = cmd.ExecuteReader();
                        ListProduct.Clear();
                        while (reader.Read())
                        {
                            int madon = reader.GetInt16(0);
                            string mamon = reader.GetString(1);
                            string ten = reader.GetString(2);
                            int soluong = reader.GetInt16(3);
                            string gia = (reader.GetSqlMoney(4) * soluong).ToString();
                            string thoigian = reader.GetDateTime(5).ToShortDateString();

                            ListProduct.Add(new LichSuBanModel(madon, mamon, ten, soluong, gia, thoigian));
                        }

                        CloseConnect();
                        return;
                    }
            }
        }




    }
}

