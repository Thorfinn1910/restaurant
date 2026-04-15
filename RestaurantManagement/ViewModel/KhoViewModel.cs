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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Windows.Input;

namespace QuanLyNhaHang.ViewModel
{
    public class KhoViewModel : BaseViewModel
    {
        public enum StockFormMode
        {
            NewIngredient = 0,
            StockIn = 1,
            EditStockIn = 2
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
                if (_selected == value)
                {
                    return;
                }

                _selected = value;
                OnPropertyChanged();

                if (_selected == null)
                {
                    ListIn.Clear();
                    SelectedInputHistory = null;
                    if (CurrentStockFormMode != StockFormMode.NewIngredient)
                    {
                        ResetStockForm(false, true);
                    }
                    CommandManager.InvalidateRequerySuggested();
                    return;
                }

                GetInputInfo(_selected.TenSanPham);
                if (CurrentStockFormMode == StockFormMode.StockIn)
                {
                    ResetStockForm(true, true);
                    PrefillStockInFromLatestHistory();
                }
                else if (CurrentStockFormMode == StockFormMode.EditStockIn)
                {
                    ResetStockForm(true, true);
                }

                CommandManager.InvalidateRequerySuggested();
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
                    FillFormFromHistory(_selectedInputHistory);
                }

                CommandManager.InvalidateRequerySuggested();
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
                OnPropertyChanged(nameof(CurrentStockTabIndex));
                OnPropertyChanged(nameof(IsNewIngredientMode));
                OnPropertyChanged(nameof(IsStockInMode));
                OnPropertyChanged(nameof(IsEditStockInMode));
                OnPropertyChanged(nameof(IsNameReadOnly));
                OnPropertyChanged(nameof(IsIdReadOnly));
                OnPropertyChanged(nameof(CanSelectHistory));
                OnPropertyChanged(nameof(CanEditHistory));
            }
        }

        public int CurrentStockTabIndex
        {
            get => (int)CurrentStockFormMode;
            set
            {
                if (!Enum.IsDefined(typeof(StockFormMode), value))
                {
                    return;
                }

                SetFormMode((StockFormMode)value);
            }
        }

        public bool IsNewIngredientMode => CurrentStockFormMode == StockFormMode.NewIngredient;
        public bool IsStockInMode => CurrentStockFormMode == StockFormMode.StockIn;
        public bool IsEditStockInMode => CurrentStockFormMode == StockFormMode.EditStockIn;
        public bool IsNameReadOnly => !IsNewIngredientMode;
        public bool IsIdReadOnly => IsEditStockInMode;
        public bool CanSelectHistory => IsEditStockInMode;
        public bool CanEditHistory => IsEditStockInMode && SelectedInputHistory != null;

        private string _id;
        public string ID
        {
            get => _id;
            set => SetFormField(ref _id, value);
        }

        private string _name;
        public string Name
        {
            get => _name;
            set => SetFormField(ref _name, value);
        }

        private string _count;
        public string Count
        {
            get => _count;
            set => SetFormField(ref _count, value);
        }

        private string _unit;
        public string Unit
        {
            get => _unit;
            set => SetFormField(ref _unit, value);
        }

        private string _value;
        public string Value
        {
            get => _value;
            set => SetFormField(ref _value, value);
        }

        private string _dateIn;
        public string DateIn
        {
            get => _dateIn;
            set => SetFormField(ref _dateIn, value);
        }

        private string _suplier;
        public string Suplier
        {
            get => _suplier;
            set => SetFormField(ref _suplier, value);
        }

        private string _suplierInfo;
        public string SuplierInfo
        {
            get => _suplierInfo;
            set => SetFormField(ref _suplierInfo, value);
        }

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

            SetFormMode(StockFormMode.StockIn, false);
            ResetStockForm(false, true);
            RefreshWarehouseList();

            CreateNewIngredientCM = new RelayCommand<object>((p) => true, (p) => SetFormMode(StockFormMode.NewIngredient));
            AddCM = new RelayCommand<object>((p) => CanCreateStockEntry(), (p) => AddStockInEntry());
            EditCM = new RelayCommand<object>((p) => CanUpdateStockEntry(), (p) => EditStockInEntry());
            DeleteCM = new RelayCommand<object>((p) => Selected != null, (p) => DeleteWarehouseItem());
            CheckCM = new RelayCommand<object>((p) => ListWareHouse != null, (p) => CheckLowStockAndExportPdf());
        }

        private void SetFormField(ref string backingField, string value, [CallerMemberName] string propertyName = "")
        {
            if (string.Equals(backingField, value, StringComparison.Ordinal))
            {
                return;
            }

            backingField = value;
            OnPropertyChanged(propertyName);
            CommandManager.InvalidateRequerySuggested();
        }

        private void SetFormMode(StockFormMode mode, bool resetByMode = true)
        {
            bool modeChanged = CurrentStockFormMode != mode;
            if (modeChanged)
            {
                CurrentStockFormMode = mode;
            }

            if (!resetByMode)
            {
                CommandManager.InvalidateRequerySuggested();
                return;
            }

            if (mode == StockFormMode.NewIngredient)
            {
                SelectedInputHistory = null;
                ResetStockForm(false, true);
            }
            else if (mode == StockFormMode.StockIn)
            {
                SelectedInputHistory = null;
                ResetStockForm(Selected != null, true);
                PrefillStockInFromLatestHistory();
            }
            else
            {
                ResetStockForm(Selected != null, false);
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private void PrefillStockInFromLatestHistory()
        {
            if (!IsStockInMode || ListIn.Count == 0)
            {
                return;
            }

            NhapKho latestInput = ListIn[0];
            Unit = latestInput.DonVi;
            Value = latestInput.DonGia;
            Suplier = latestInput.NguonNhap;
            SuplierInfo = latestInput.LienLac;
        }

        private bool WarehouseContainsProduct(string productName)
        {
            return ListWareHouse.Any(x => string.Equals(x.TenSanPham, productName, StringComparison.OrdinalIgnoreCase));
        }

        private List<string> CollectCommonInputErrors()
        {
            List<string> errors = new List<string>();

            string stockInId = (ID ?? string.Empty).Trim();
            string productName = (Name ?? string.Empty).Trim();
            string quantityText = (Count ?? string.Empty).Trim();
            string unitText = (Unit ?? string.Empty).Trim();
            string unitPriceText = (Value ?? string.Empty).Trim();
            string dateText = (DateIn ?? string.Empty).Trim();
            string supplierContactText = (SuplierInfo ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(stockInId))
            {
                errors.Add("Mã nhập là bắt buộc.");
            }

            if (string.IsNullOrWhiteSpace(productName))
            {
                errors.Add("Tên sản phẩm là bắt buộc.");
            }

            if (string.IsNullOrWhiteSpace(quantityText))
            {
                errors.Add("Số lượng là bắt buộc.");
            }
            else if (!TryParsePositiveDouble(quantityText, out _))
            {
                errors.Add("Số lượng phải lớn hơn 0.");
            }

            if (string.IsNullOrWhiteSpace(unitText))
            {
                errors.Add("Đơn vị là bắt buộc.");
            }

            if (string.IsNullOrWhiteSpace(unitPriceText))
            {
                errors.Add("Đơn giá là bắt buộc.");
            }
            else if (!TryParsePositiveDecimal(unitPriceText, out _))
            {
                errors.Add("Đơn giá phải lớn hơn 0.");
            }

            if (string.IsNullOrWhiteSpace(dateText))
            {
                errors.Add("Ngày nhập là bắt buộc.");
            }
            else if (!DateTime.TryParse(dateText, CultureInfo.CurrentCulture, DateTimeStyles.None, out _)
                     && !DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                errors.Add("Ngày nhập không hợp lệ.");
            }

            if (!string.IsNullOrWhiteSpace(supplierContactText) && !isNumber(supplierContactText))
            {
                errors.Add("Liên lạc chỉ được chứa chữ số.");
            }

            return errors;
        }

        private List<string> CollectCreateErrors()
        {
            List<string> errors = CollectCommonInputErrors();
            string stockInId = (ID ?? string.Empty).Trim();
            string productName = (Name ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(stockInId) == false && KhoDP.Flag.ExistsStockInId(stockInId))
            {
                errors.Add("Mã nhập đã tồn tại.");
            }

            if (IsNewIngredientMode)
            {
                if (!string.IsNullOrWhiteSpace(productName)
                    && (WarehouseContainsProduct(productName) || KhoDP.Flag.ExistsWarehouseProduct(productName)))
                {
                    errors.Add("Nguyên liệu đã tồn tại. Hãy dùng tab 'Nhập bổ sung'.");
                }
            }
            else if (IsStockInMode)
            {
                if (Selected == null)
                {
                    errors.Add("Chọn nguyên liệu ở danh sách bên trái trước khi nhập bổ sung.");
                }
                else if (!string.Equals(productName, Selected.TenSanPham, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Tên sản phẩm phải trùng nguyên liệu đã chọn.");
                }
            }
            else
            {
                errors.Add("Chỉ có thể tạo phiếu mới trong tab Nguyên liệu mới hoặc Nhập bổ sung.");
            }

            return errors;
        }

        private List<string> CollectEditErrors()
        {
            List<string> errors = CollectCommonInputErrors();
            string stockInId = (ID ?? string.Empty).Trim();
            string productName = (Name ?? string.Empty).Trim();

            if (!IsEditStockInMode)
            {
                errors.Add("Chỉ được sửa phiếu trong tab Sửa phiếu.");
                return errors;
            }

            if (SelectedInputHistory == null)
            {
                errors.Add("Vui lòng chọn phiếu nhập cần sửa.");
                return errors;
            }

            if (!string.Equals(stockInId, SelectedInputHistory.MaNhap, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Không được sửa mã nhập.");
            }

            if (!string.Equals(productName, SelectedInputHistory.TenSP, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Không được sửa tên sản phẩm.");
            }

            return errors;
        }

        private void ShowValidationSummary(IEnumerable<string> errors, string title)
        {
            List<string> lines = errors
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            if (lines.Count == 0)
            {
                return;
            }

            ShowMessage(title + "\n- " + string.Join("\n- ", lines));
        }

        private bool CanCreateStockEntry()
        {
            return IsNewIngredientMode || IsStockInMode;
        }

        private bool CanUpdateStockEntry()
        {
            return IsEditStockInMode;
        }

        private void AddStockInEntry()
        {
            List<string> errors = CollectCreateErrors();
            if (errors.Count > 0)
            {
                ShowValidationSummary(errors, "Không thể lưu phiếu nhập. Vui lòng sửa các lỗi:");
                return;
            }

            if (!TryBuildInput(out StockInEntryInput input))
            {
                ShowMessage("Dữ liệu nhập không hợp lệ.");
                return;
            }

            try
            {
                KhoDP.Flag.CreateStockInEntry(input);
                ShowMessage("Nhập thành công!");

                string? selectedName = IsNewIngredientMode ? null : Selected?.TenSanPham;
                RefreshWarehouseList();
                RestoreSelectedByName(selectedName ?? input.ProductName);

                if (IsNewIngredientMode)
                {
                    ResetStockForm(false, true);
                }
                else
                {
                    GetInputInfo(input.ProductName);
                    ResetStockForm(true, true);
                    PrefillStockInFromLatestHistory();
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
            List<string> errors = CollectEditErrors();
            if (errors.Count > 0)
            {
                ShowValidationSummary(errors, "Không thể sửa phiếu nhập. Vui lòng sửa các lỗi:");
                return;
            }

            if (!TryBuildInput(out StockInEntryInput input))
            {
                ShowMessage("Dữ liệu nhập không hợp lệ.");
                return;
            }

            try
            {
                KhoDP.Flag.UpdateStockInEntry(SelectedInputHistory!.MaNhap, input);
                ShowMessage("Sửa thành công!");

                string selectedName = input.ProductName;
                RefreshWarehouseList();
                RestoreSelectedByName(selectedName);
                GetInputInfo(selectedName);
                SelectedInputHistory = ListIn.FirstOrDefault(x => string.Equals(x.MaNhap, input.StockInId, StringComparison.OrdinalIgnoreCase));
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

        private bool TryBuildInput(out StockInEntryInput input)
        {
            input = new StockInEntryInput();

            string stockInId = (ID ?? string.Empty).Trim();
            string productName = (Name ?? string.Empty).Trim();
            string quantityText = (Count ?? string.Empty).Trim();
            string unitText = (Unit ?? string.Empty).Trim();
            string unitPriceText = (Value ?? string.Empty).Trim();
            string dateText = (DateIn ?? string.Empty).Trim();
            string supplierText = (Suplier ?? string.Empty).Trim();
            string supplierContactText = (SuplierInfo ?? string.Empty).Trim();

            if (!TryParsePositiveDouble(quantityText, out double quantity)
                || !TryParsePositiveDecimal(unitPriceText, out decimal unitPrice)
                || (!DateTime.TryParse(dateText, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime inputDate)
                    && !DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out inputDate)))
            {
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

        private void RestoreSelectedByName(string? productName)
        {
            if (string.IsNullOrWhiteSpace(productName))
            {
                return;
            }

            Selected = ListWareHouse.FirstOrDefault(x => string.Equals(x.TenSanPham, productName, StringComparison.OrdinalIgnoreCase));
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
                    Selected = null;
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
                    ShowMessage("Chưa có sản phẩm nào cần nhập thêm!");
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