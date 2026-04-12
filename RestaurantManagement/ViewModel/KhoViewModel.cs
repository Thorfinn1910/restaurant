using Document = iTextSharp.text.Document;
using iTextSharp.text;
using iTextSharp.text.pdf;
using QuanLyNhaHang.DataProvider;
using QuanLyNhaHang.Models;
using QuanLyNhaHang.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Windows.Input;

namespace QuanLyNhaHang.ViewModel
{
    public class KhoViewModel : BaseViewModel
    {
        public enum StockFormMode
        {
            NewIngredient,
            StockIn,
            EditStockIn
        }

        private ObservableCollection<Kho> _listWareHouse;
        public ObservableCollection<Kho> ListWareHouse
        {
            get => _listWareHouse;
            set
            {
                _listWareHouse = value;
                OnPropertyChanged();
            }
        }

        private Kho? _selected;
        public Kho? Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                OnPropertyChanged();

                if (_selected == null)
                {
                    ListIn.Clear();
                    SelectedInputHistory = null;
                    if (CurrentStockFormMode == StockFormMode.EditStockIn)
                    {
                        SetFormMode(StockFormMode.StockIn);
                    }
                    ResetStockForm(false, true);
                    return;
                }

                GetInputInfo(_selected.TenSanPham);
                if (CurrentStockFormMode == StockFormMode.NewIngredient)
                {
                    SetFormMode(StockFormMode.StockIn);
                }
                if (CurrentStockFormMode == StockFormMode.EditStockIn)
                {
                    SetFormMode(StockFormMode.StockIn);
                }
                ResetStockForm(CurrentStockFormMode != StockFormMode.NewIngredient, true);
            }
        }

        private ObservableCollection<NhapKho> _listIn;
        public ObservableCollection<NhapKho> ListIn
        {
            get => _listIn;
            set
            {
                _listIn = value;
                OnPropertyChanged();
            }
        }

        private NhapKho? _selectedInputHistory;
        public NhapKho? SelectedInputHistory
        {
            get => _selectedInputHistory;
            set
            {
                if (_selectedInputHistory == value)
                {
                    return;
                }

                _selectedInputHistory = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanEditHistory));

                if (_selectedInputHistory != null)
                {
                    if (!CanSelectHistory)
                    {
                        return;
                    }
                    SetFormMode(StockFormMode.EditStockIn);
                    FillFormFromHistory(_selectedInputHistory);
                }
                else if (CurrentStockFormMode == StockFormMode.EditStockIn)
                {
                    SetFormMode(StockFormMode.StockIn);
                }
            }
        }

        private StockFormMode _currentStockFormMode;
        public StockFormMode CurrentStockFormMode
        {
            get => _currentStockFormMode;
            private set
            {
                _currentStockFormMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentStockFormModeKey));
                OnPropertyChanged(nameof(CurrentStockFormModeDescription));
                OnPropertyChanged(nameof(IsNewIngredientMode));
                OnPropertyChanged(nameof(CanSelectHistory));
                OnPropertyChanged(nameof(CanEditHistory));
            }
        }

        public string CurrentStockFormModeKey => CurrentStockFormMode switch
        {
            StockFormMode.NewIngredient => "NEW_INGREDIENT_MODE",
            StockFormMode.EditStockIn => "EDIT_STOCK_IN_MODE",
            _ => "STOCK_IN_MODE"
        };

        public string CurrentStockFormModeDescription => CurrentStockFormMode switch
        {
            StockFormMode.NewIngredient => "Đang tạo nguyên liệu mới. Chỉ tạo nguyên liệu chưa tồn tại.",
            StockFormMode.EditStockIn => "Đang sửa phiếu nhập đã chọn.",
            _ => "Đang nhập bổ sung kho. Hỗ trợ nguyên liệu mới hoặc đã có."
        };

        public bool IsNewIngredientMode => CurrentStockFormMode == StockFormMode.NewIngredient;
        public bool CanSelectHistory => !IsNewIngredientMode;
        public bool CanEditHistory => !IsNewIngredientMode && SelectedInputHistory != null;

        private string _id;
        public string ID { get => _id; set { _id = value; OnPropertyChanged(); } }

        private string _name;
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        private string _count;
        public string Count { get => _count; set { _count = value; OnPropertyChanged(); } }

        private string _unit;
        public string Unit { get => _unit; set { _unit = value; OnPropertyChanged(); } }

        private string _value;
        public string Value { get => _value; set { _value = value; OnPropertyChanged(); } }

        private string _dateIn;
        public string DateIn { get => _dateIn; set { _dateIn = value; OnPropertyChanged(); } }

        private string _suplier;
        public string Suplier { get => _suplier; set { _suplier = value; OnPropertyChanged(); } }

        private string _suplierInfo;
        public string SuplierInfo { get => _suplierInfo; set { _suplierInfo = value; OnPropertyChanged(); } }

        private string _search;
        public string Search
        {
            get => _search;
            set
            {
                _search = value;
                OnPropertyChanged();
                RefreshWarehouseList();
            }
        }

        public ICommand CreateNewIngredientCM { get; set; }
        public ICommand AddCM { get; set; }
        public ICommand EditCM { get; set; }
        public ICommand DeleteCM { get; set; }
        public ICommand CheckCM { get; set; }

        private readonly string strCon = ConfigurationManager.ConnectionStrings["QuanLyNhaHang"].ConnectionString;
        private SqlConnection sqlCon = null;

        public KhoViewModel()
        {
            ListWareHouse = new ObservableCollection<Kho>();
            ListIn = new ObservableCollection<NhapKho>();

            SetFormMode(StockFormMode.StockIn);
            ResetStockForm(false, true);
            RefreshWarehouseList();

            CreateNewIngredientCM = new RelayCommand<object>((p) => true, (p) => EnterNewIngredientMode());
            AddCM = new RelayCommand<object>((p) => CanSubmitStockIn(), (p) => AddStockInEntry());
            EditCM = new RelayCommand<object>((p) => CanEditStockIn(), (p) => EditStockInEntry());
            DeleteCM = new RelayCommand<object>((p) => Selected != null, (p) => DeleteWarehouseItem());
            CheckCM = new RelayCommand<object>((p) => ListWareHouse != null, (p) => CheckLowStockAndExportPdf());
        }

        private void SetFormMode(StockFormMode mode)
        {
            CurrentStockFormMode = mode;
        }

        private void EnterNewIngredientMode()
        {
            SetFormMode(StockFormMode.NewIngredient);
            ResetStockForm(false, true);
        }

        private bool CanSubmitStockIn()
        {
            return !string.IsNullOrWhiteSpace(ID)
                && !string.IsNullOrWhiteSpace(Name)
                && !string.IsNullOrWhiteSpace(Count)
                && !string.IsNullOrWhiteSpace(Unit)
                && !string.IsNullOrWhiteSpace(Value)
                && !string.IsNullOrWhiteSpace(DateIn);
        }

        private bool CanEditStockIn()
        {
            return CanEditHistory && CanSubmitStockIn();
        }

        private void AddStockInEntry()
        {
            bool isNewIngredientMode = IsNewIngredientMode;

            if (!TryBuildInput(out StockInEntryInput input, out string errorMessage))
            {
                ShowMessage(errorMessage);
                return;
            }

            if (KhoDP.Flag.ExistsStockInId(input.StockInId))
            {
                ShowMessage("Mã nhập đã tồn tại!");
                return;
            }

            if (isNewIngredientMode && KhoDP.Flag.ExistsWarehouseProduct(input.ProductName))
            {
                ShowMessage("Nguyên liệu đã có trong kho, vui lòng dùng 'THÊM PHIẾU NHẬP'.");
                return;
            }

            try
            {
                KhoDP.Flag.CreateStockInEntry(input);
                ShowMessage("Nhập thành công!");

                RefreshWarehouseList();
                GetInputInfo(input.ProductName);

                if (isNewIngredientMode)
                {
                    SetFormMode(StockFormMode.NewIngredient);
                    ResetStockForm(false, true);
                }
                else
                {
                    SetFormMode(StockFormMode.StockIn);
                    ResetStockForm(true, true);
                }
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(ex.Message);
            }
            catch (SqlException)
            {
                ShowMessage("Lỗi cơ sở dữ liệu khi nhập kho.");
            }
        }

        private void EditStockInEntry()
        {
            if (SelectedInputHistory == null)
            {
                ShowMessage("Vui lòng chọn một phiếu nhập gần đây để sửa.");
                return;
            }

            if (!TryBuildInput(out StockInEntryInput input, out string errorMessage))
            {
                ShowMessage(errorMessage);
                return;
            }

            if (!string.Equals(input.StockInId, SelectedInputHistory.MaNhap, StringComparison.OrdinalIgnoreCase))
            {
                ShowMessage("Không được sửa Mã nhập!");
                return;
            }

            if (!string.Equals(input.ProductName, SelectedInputHistory.TenSP, StringComparison.OrdinalIgnoreCase))
            {
                ShowMessage("Không được sửa Tên sản phẩm!");
                return;
            }

            try
            {
                KhoDP.Flag.UpdateStockInEntry(SelectedInputHistory.MaNhap, input);
                ShowMessage("Sửa thành công!");

                RefreshWarehouseList();
                GetInputInfo(input.ProductName);
                SetFormMode(StockFormMode.StockIn);
                ResetStockForm(true, true);
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(ex.Message);
            }
            catch (SqlException)
            {
                ShowMessage("Lỗi cơ sở dữ liệu khi sửa phiếu nhập.");
            }
        }

        private bool TryBuildInput(out StockInEntryInput input, out string errorMessage)
        {
            input = new StockInEntryInput();
            errorMessage = string.Empty;

            string stockInId = (ID ?? string.Empty).Trim();
            string productName = (Name ?? string.Empty).Trim();
            string quantityText = (Count ?? string.Empty).Trim();
            string unitText = (Unit ?? string.Empty).Trim();
            string unitPriceText = (Value ?? string.Empty).Trim();
            string dateText = (DateIn ?? string.Empty).Trim();
            string supplierText = (Suplier ?? string.Empty).Trim();
            string supplierContactText = (SuplierInfo ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(stockInId)
                || string.IsNullOrWhiteSpace(productName)
                || string.IsNullOrWhiteSpace(quantityText)
                || string.IsNullOrWhiteSpace(unitText)
                || string.IsNullOrWhiteSpace(unitPriceText)
                || string.IsNullOrWhiteSpace(dateText))
            {
                errorMessage = "Vui lòng nhập đầy đủ thông tin bắt buộc.";
                return false;
            }

            if (!TryParsePositiveDouble(quantityText, out double quantity))
            {
                errorMessage = "Số lượng phải là số lớn hơn 0.";
                return false;
            }

            if (!TryParsePositiveDecimal(unitPriceText, out decimal unitPrice))
            {
                errorMessage = "Đơn giá phải là số lớn hơn 0.";
                return false;
            }

            if (!DateTime.TryParse(dateText, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime inputDate)
                && !DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out inputDate))
            {
                errorMessage = "Ngày nhập không hợp lệ.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(supplierContactText) && !isNumber(supplierContactText))
            {
                errorMessage = "Liên lạc chỉ được chứa chữ số.";
                return false;
            }

            input.StockInId = stockInId;
            input.ProductName = productName;
            input.Quantity = quantity;
            input.Unit = unitText;
            input.UnitPrice = unitPrice;
            input.DateIn = inputDate;
            input.Supplier = supplierText;
            input.SupplierContact = supplierContactText;

            return true;
        }

        private bool TryParsePositiveDouble(string text, out double value)
        {
            bool parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

            if (!parsed)
            {
                return false;
            }

            return value > 0;
        }

        private bool TryParsePositiveDecimal(string text, out decimal value)
        {
            bool parsed = decimal.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                || decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

            if (!parsed)
            {
                return false;
            }

            return value > 0;
        }

        private void DeleteWarehouseItem()
        {
            Kho? selected = Selected;
            if (selected == null)
            {
                return;
            }

            bool shouldDelete = false;

            if (selected.TonDu > 0)
            {
                MyMessageBox confirmation = new MyMessageBox("Sản phẩm này đang còn trong kho!\n   Bạn có chắc chắn xóa?", true);
                confirmation.ShowDialog();
                if (confirmation.ACCEPT())
                {
                    shouldDelete = true;
                }
            }
            else
            {
                shouldDelete = true;
            }

            if (!shouldDelete)
            {
                return;
            }

            try
            {
                OpenConnect();

                using SqlCommand cmd = new SqlCommand("UPDATE KHO SET Xoa = 1 WHERE TenSanPham = @tenSanPham", sqlCon);
                cmd.Parameters.AddWithValue("@tenSanPham", selected.TenSanPham);

                int result = cmd.ExecuteNonQuery();
                if (result > 0)
                {
                    ShowMessage("Xóa thành công!");
                    SetFormMode(StockFormMode.StockIn);
                    ResetStockForm(false, true);
                }
                else
                {
                    ShowMessage("Xóa không thành công!");
                }

                RefreshWarehouseList();
            }
            finally
            {
                CloseConnect();
            }
        }

        private void CheckLowStockAndExportPdf()
        {
            try
            {
                OpenConnect();

                const string lowStockQuery = "SELECT TenSanPham, TonDu, DonVi FROM KHO WHERE Xoa = 0 AND ((DonVi = N'Kg' AND TonDu <= 1) OR (DonVi <> N'Kg' AND TonDu <= 5))";
                using SqlCommand cmd = new SqlCommand(lowStockQuery, sqlCon);
                using SqlDataReader reader = cmd.ExecuteReader();

                List<string> ten = new List<string>();
                List<string> soluong = new List<string>();
                List<string> donvi = new List<string>();

                while (reader.Read())
                {
                    ten.Add(reader.GetString(0));
                    soluong.Add(Convert.ToDouble(reader[1]).ToString(CultureInfo.CurrentCulture));
                    donvi.Add(reader.GetString(2));
                }

                if (ten.Count == 0)
                {
                    ShowMessage("Chưa có sản phẩm nào \n      cần nhập thêm!");
                    return;
                }

                MyMessageBox yesno = new MyMessageBox("Bạn có muốn in danh sách?", true);
                yesno.ShowDialog();
                if (!yesno.ACCEPT())
                {
                    return;
                }

                SaveFileDialog sfd = new SaveFileDialog
                {
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = "Danh sách cần nhập " + DateTime.Now.Day + "-" + DateTime.Now.Month + "-" + DateTime.Now.Year
                };

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
                        ShowMessage("Đã có lỗi xảy ra!");
                        return;
                    }
                }

                PdfPTable pdfTable = new PdfPTable(3)
                {
                    WidthPercentage = 100,
                    HorizontalAlignment = Element.ALIGN_LEFT
                };
                pdfTable.DefaultCell.Padding = 3;

                BaseFont bf = BaseFont.CreateFont(Environment.GetEnvironmentVariable("windir") + @"\fonts\TIMES.TTF", BaseFont.IDENTITY_H, true);
                Font f = new Font(bf, 16, Font.NORMAL);

                pdfTable.AddCell(new PdfPCell(new Phrase("Tên sản phẩm", f)));
                pdfTable.AddCell(new PdfPCell(new Phrase("Tồn dư", f)));
                pdfTable.AddCell(new PdfPCell(new Phrase("Đơn vị", f)));

                for (int i = 0; i < ten.Count; i++)
                {
                    pdfTable.AddCell(new Phrase(ten[i], f));
                    pdfTable.AddCell(new Phrase(soluong[i], f));
                    pdfTable.AddCell(new Phrase(donvi[i], f));
                }

                using FileStream stream = new FileStream(sfd.FileName, FileMode.Create);
                Document pdfDoc = new Document(PageSize.A4, 50f, 50f, 40f, 40f);
                PdfWriter.GetInstance(pdfDoc, stream);
                pdfDoc.Open();
                pdfDoc.Add(new Paragraph("              DANH SÁCH SẢN PHẨM CẦN NHẬP THÊM " + DateTime.Now.ToShortDateString(), f));
                pdfDoc.Add(new Paragraph("    "));
                pdfDoc.Add(pdfTable);
                pdfDoc.Close();

                ShowMessage("In thành công!");
            }
            catch
            {
                ShowMessage("Đã có lỗi xảy ra!");
            }
            finally
            {
                CloseConnect();
            }
        }

        private void RefreshWarehouseList()
        {
            try
            {
                OpenConnect();

                string keyword = (Search ?? string.Empty).Trim();
                string query = "SELECT TenSanPham, TonDu, DonVi, DonGia FROM KHO WHERE Xoa = 0";

                using SqlCommand cmd = new SqlCommand();
                cmd.Connection = sqlCon;
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    query += " AND TenSanPham LIKE @keyword";
                    cmd.Parameters.AddWithValue("@keyword", "%" + keyword + "%");
                }
                cmd.CommandText = query;

                using SqlDataReader reader = cmd.ExecuteReader();
                ListWareHouse.Clear();

                while (reader.Read())
                {
                    string ten = reader.GetString(0);
                    float tondu = (float)Convert.ToDouble(reader[1]);
                    string donvi = reader.GetString(2);
                    string dongia = MoneyFormatter.FormatVnd(Convert.ToDecimal(reader[3]));
                    ListWareHouse.Add(new Kho(ten, tondu, donvi, dongia));
                }
            }
            finally
            {
                CloseConnect();
            }
        }

        private void GetInputInfo(string productName)
        {
            try
            {
                OpenConnect();

                using SqlCommand cmd = new SqlCommand(
                    "SELECT TOP 10 MaNhap, TenSanPham, DonVi, DonGia, SoLuong, NgayNhap, NguonNhap, LienLac " +
                    "FROM CHITIETNHAP WHERE TenSanPham = @tenSanPham ORDER BY NgayNhap DESC, MaNhap DESC",
                    sqlCon);
                cmd.Parameters.AddWithValue("@tenSanPham", productName);

                using SqlDataReader reader = cmd.ExecuteReader();
                ListIn.Clear();
                SelectedInputHistory = null;

                while (reader.Read())
                {
                    string ma = reader.GetString(0);
                    string ten = reader.GetString(1);
                    string donvi = reader.GetString(2);
                    string dongia = MoneyFormatter.FormatPlainAmount(Convert.ToDecimal(reader[3]));
                    string soluong = Convert.ToDouble(reader[4]).ToString(CultureInfo.CurrentCulture);
                    string date = reader.GetDateTime(5).ToShortDateString();
                    string nguon = reader.GetString(6);
                    string lienlac = reader.GetString(7);
                    ListIn.Add(new NhapKho(ma, ten, donvi, dongia, soluong, date, nguon, lienlac));
                }
            }
            finally
            {
                CloseConnect();
            }
        }

        private void FillFormFromHistory(NhapKho inputHistory)
        {
            ID = inputHistory.MaNhap;
            Name = inputHistory.TenSP;
            Count = inputHistory.SoLuong;
            Unit = inputHistory.DonVi;
            Value = inputHistory.DonGia;
            DateIn = inputHistory.NgayNhap;
            Suplier = inputHistory.NguonNhap;
            SuplierInfo = inputHistory.LienLac;
        }

        private void ResetStockForm(bool keepSelectedProduct, bool clearHistorySelection)
        {
            ID = string.Empty;
            Count = string.Empty;
            Unit = string.Empty;
            Value = string.Empty;
            Suplier = string.Empty;
            SuplierInfo = string.Empty;
            DateIn = DateTime.Now.ToShortDateString();

            if (IsNewIngredientMode)
            {
                Name = string.Empty;
            }
            else if (keepSelectedProduct && Selected != null)
            {
                Name = Selected.TenSanPham;
            }
            else
            {
                Name = string.Empty;
            }

            if (clearHistorySelection)
            {
                SelectedInputHistory = null;
            }
        }

        private void OpenConnect()
        {
            sqlCon ??= new SqlConnection(strCon);
            if (sqlCon.State == ConnectionState.Closed)
            {
                sqlCon.Open();
            }
        }

        private void CloseConnect()
        {
            if (sqlCon != null && sqlCon.State == ConnectionState.Open)
            {
                sqlCon.Close();
            }
        }

        private bool isNumber(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return false;
            }

            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] < '0' || s[i] > '9')
                {
                    return false;
                }
            }

            return true;
        }

        private void ShowMessage(string message)
        {
            MyMessageBox mess = new MyMessageBox(message);
            mess.ShowDialog();
        }
    }
}
