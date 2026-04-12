using QuanLyNhaHang.DataProvider;
using QuanLyNhaHang.Models;
using RestaurantManagement.View;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Globalization;
using System.Windows.Forms;
using System.Windows.Input;

namespace QuanLyNhaHang.ViewModel
{
    public class NhanVienViewModel : BaseViewModel
    {
        public enum StaffFormMode
        {
            Create,
            Edit
        }

        private ObservableCollection<NhanVien> _listStaff;
        public ObservableCollection<NhanVien> ListStaff
        {
            get => _listStaff;
            set
            {
                _listStaff = value;
                OnPropertyChanged();
            }
        }

        private NhanVien? _selected;
        public NhanVien? Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                OnPropertyChanged();

                if (_selected == null)
                {
                    EnterCreateMode();
                    return;
                }

                ID = _selected.MaNV;
                Name = _selected.HoTen;
                Position = _selected.ChucVu;
                Fulltime = _selected.Fulltime ? "Full-time" : "Part-time";
                Address = _selected.DiaChi;
                Phone = _selected.SDT;
                DateBorn = _selected.NgaySinh;
                DateStartWork = _selected.NgayVaoLam;
                Account = _selected.TaiKhoan;
                Password = _selected.MatKhau;

                originalEmployeeId = ID;
                originalAccountId = Account;
                originalPosition = Position;

                SetMode(StaffFormMode.Edit);
            }
        }

        private StaffFormMode _currentStaffFormMode;
        public StaffFormMode CurrentStaffFormMode
        {
            get => _currentStaffFormMode;
            private set
            {
                _currentStaffFormMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentStaffFormModeKey));
                OnPropertyChanged(nameof(CurrentStaffFormModeDescription));
            }
        }

        public string CurrentStaffFormModeKey => CurrentStaffFormMode == StaffFormMode.Create ? "CREATE_STAFF_MODE" : "EDIT_STAFF_MODE";

        public string CurrentStaffFormModeDescription =>
            CurrentStaffFormMode == StaffFormMode.Create
                ? "Đang thêm nhân viên mới. Bắt buộc có tài khoản đăng nhập."
                : "Đang cập nhật hồ sơ nhân viên đã chọn.";

        private string _id = string.Empty;
        public string ID { get => _id; set { _id = value; OnPropertyChanged(); } }

        private string _name = string.Empty;
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        private string _position = string.Empty;
        public string Position { get => _position; set { _position = value; OnPropertyChanged(); } }

        private string _fulltime = string.Empty;
        public string Fulltime { get => _fulltime; set { _fulltime = value; OnPropertyChanged(); } }

        private string _address = string.Empty;
        public string Address { get => _address; set { _address = value; OnPropertyChanged(); } }

        private string _phone = string.Empty;
        public string Phone { get => _phone; set { _phone = value; OnPropertyChanged(); } }

        private string _dateBorn = string.Empty;
        public string DateBorn { get => _dateBorn; set { _dateBorn = value; OnPropertyChanged(); } }

        private string _dateStartWork = string.Empty;
        public string DateStartWork { get => _dateStartWork; set { _dateStartWork = value; OnPropertyChanged(); } }

        private string _account = string.Empty;
        public string Account { get => _account; set { _account = value; OnPropertyChanged(); } }

        private string _password = string.Empty;
        public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }

        private string _search = string.Empty;
        public string Search
        {
            get => _search;
            set
            {
                _search = value;
                OnPropertyChanged();
                LoadEmployees();
            }
        }

        private string originalEmployeeId = string.Empty;
        private string originalAccountId = string.Empty;
        private string originalPosition = string.Empty;

        public ICommand AddCM { get; set; }
        public ICommand EditCM { get; set; }
        public ICommand DeleteCM { get; set; }
        public ICommand CheckCM { get; set; }
        public ICommand NewStaffCM { get; set; }

        public NhanVienViewModel()
        {
            ListStaff = new ObservableCollection<NhanVien>();

            EnterCreateMode();
            LoadEmployees();

            AddCM = new RelayCommand<object>((p) => CanAddEmployee(), (p) => AddEmployee());
            EditCM = new RelayCommand<object>((p) => CanEditEmployee(), (p) => EditEmployee());
            DeleteCM = new RelayCommand<object>((p) => Selected != null && Selected.ChucVu != "Quản lý", (p) => DeleteEmployee());
            CheckCM = new RelayCommand<object>((p) => true, (p) =>
            {
                RestaurantManagement.View.ChamCong chamCong = new RestaurantManagement.View.ChamCong();
                chamCong.Show();
            });
            NewStaffCM = new RelayCommand<object>((p) => true, (p) => EnterCreateMode());
        }

        private void SetMode(StaffFormMode mode)
        {
            CurrentStaffFormMode = mode;
        }

        private void EnterCreateMode()
        {
            SetMode(StaffFormMode.Create);
            originalEmployeeId = string.Empty;
            originalAccountId = string.Empty;
            originalPosition = string.Empty;
            RefreshForm();
            DateBorn = DateTime.Now.ToShortDateString();
            DateStartWork = DateTime.Now.ToShortDateString();
            _selected = null;
            OnPropertyChanged(nameof(Selected));
        }

        private bool CanAddEmployee()
        {
            return CurrentStaffFormMode == StaffFormMode.Create;
        }

        private bool CanEditEmployee()
        {
            return CurrentStaffFormMode == StaffFormMode.Edit && Selected != null;
        }

        private void AddEmployee()
        {
            if (!TryBuildEmployeeInput(out EmployeeUpsertInput input, out string message))
            {
                ShowMessage(message);
                return;
            }

            if (string.Equals(input.Position, "Quản lý", StringComparison.OrdinalIgnoreCase))
            {
                ShowMessage("Không được thêm nhân viên có chức vụ Quản lý từ màn này.");
                return;
            }

            try
            {
                NhanVienDP.Flag.CreateEmployeeWithAccount(input);
                ShowMessage("Thêm nhân viên thành công!");
                LoadEmployees();
                EnterCreateMode();
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(ex.Message);
            }
            catch (SqlException)
            {
                ShowMessage("Lỗi cơ sở dữ liệu khi thêm nhân viên.");
            }
        }

        private void EditEmployee()
        {
            if (Selected == null)
            {
                ShowMessage("Vui lòng chọn nhân viên cần cập nhật.");
                return;
            }

            if (!TryBuildEmployeeInput(out EmployeeUpsertInput input, out string message))
            {
                ShowMessage(message);
                return;
            }

            if (!string.Equals(input.EmployeeId, originalEmployeeId, StringComparison.OrdinalIgnoreCase))
            {
                ShowMessage("Không được sửa mã nhân viên.");
                ID = originalEmployeeId;
                return;
            }

            if (string.Equals(originalPosition, "Quản lý", StringComparison.OrdinalIgnoreCase) && !string.Equals(input.Position, "Quản lý", StringComparison.OrdinalIgnoreCase))
            {
                ShowMessage("Không được hạ chức vụ nhân viên Quản lý từ màn này.");
                Position = originalPosition;
                return;
            }

            if (!string.Equals(originalPosition, "Quản lý", StringComparison.OrdinalIgnoreCase) && string.Equals(input.Position, "Quản lý", StringComparison.OrdinalIgnoreCase))
            {
                ShowMessage("Không được nâng chức vụ lên Quản lý từ màn này.");
                Position = originalPosition;
                return;
            }

            try
            {
                NhanVienDP.Flag.UpdateEmployeeWithAccount(input, originalEmployeeId, originalAccountId);
                ShowMessage("Cập nhật thành công!");
                LoadEmployees();
                EnterCreateMode();
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(ex.Message);
            }
            catch (SqlException)
            {
                ShowMessage("Lỗi cơ sở dữ liệu khi cập nhật nhân viên.");
            }
        }

        private void DeleteEmployee()
        {
            if (Selected == null)
            {
                return;
            }

            MyMessageBox confirm = new MyMessageBox("Bạn có chắc chắn xóa cứng nhân viên này và toàn bộ dữ liệu liên quan?", true);
            confirm.ShowDialog();
            if (!confirm.ACCEPT())
            {
                return;
            }

            try
            {
                NhanVienDP.Flag.HardDeleteEmployeeCascade(Selected.MaNV);
                ShowMessage("Xóa thành công!");
                LoadEmployees();
                EnterCreateMode();
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(ex.Message);
            }
            catch (SqlException)
            {
                ShowMessage("Lỗi cơ sở dữ liệu khi xóa cứng nhân viên.");
            }
        }

        private void LoadEmployees()
        {
            ListStaff = NhanVienDP.Flag.GetEmployees((Search ?? string.Empty).Trim());
        }

        private bool TryBuildEmployeeInput(out EmployeeUpsertInput input, out string errorMessage)
        {
            input = new EmployeeUpsertInput();
            errorMessage = string.Empty;

            string employeeId = (ID ?? string.Empty).Trim();
            string fullName = (Name ?? string.Empty).Trim();
            string position = (Position ?? string.Empty).Trim();
            string fulltimeText = (Fulltime ?? string.Empty).Trim();
            string address = (Address ?? string.Empty).Trim();
            string phone = (Phone ?? string.Empty).Trim();
            string dateBornText = (DateBorn ?? string.Empty).Trim();
            string dateStartWorkText = (DateStartWork ?? string.Empty).Trim();
            string account = (Account ?? string.Empty).Trim();
            string password = (Password ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(employeeId)
                || string.IsNullOrWhiteSpace(fullName)
                || string.IsNullOrWhiteSpace(position)
                || string.IsNullOrWhiteSpace(fulltimeText)
                || string.IsNullOrWhiteSpace(phone)
                || string.IsNullOrWhiteSpace(dateBornText)
                || string.IsNullOrWhiteSpace(dateStartWorkText)
                || string.IsNullOrWhiteSpace(account)
                || string.IsNullOrWhiteSpace(password))
            {
                errorMessage = "Vui lòng nhập đầy đủ thông tin bắt buộc.";
                return false;
            }

            if (!string.Equals(fulltimeText, "Full-time", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(fulltimeText, "Part-time", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Hình thức làm việc không hợp lệ.";
                return false;
            }

            if (!isNumber(phone) || phone.Length < 9 || phone.Length > 11)
            {
                errorMessage = "Số điện thoại chỉ được chứa chữ số và có độ dài 9-11 ký tự.";
                return false;
            }

            if (!DateTime.TryParse(dateBornText, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime dateBorn)
                && !DateTime.TryParse(dateBornText, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateBorn))
            {
                errorMessage = "Ngày sinh không hợp lệ.";
                return false;
            }

            if (!DateTime.TryParse(dateStartWorkText, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime dateStartWork)
                && !DateTime.TryParse(dateStartWorkText, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateStartWork))
            {
                errorMessage = "Ngày vào làm không hợp lệ.";
                return false;
            }

            if (dateStartWork.Date < dateBorn.Date)
            {
                errorMessage = "Ngày vào làm không được nhỏ hơn ngày sinh.";
                return false;
            }

            if (account.Contains(" "))
            {
                errorMessage = "Tài khoản đăng nhập không được chứa khoảng trắng.";
                return false;
            }

            if (password.Length < 6)
            {
                errorMessage = "Mật khẩu phải có ít nhất 6 ký tự.";
                return false;
            }

            input.EmployeeId = employeeId;
            input.FullName = fullName;
            input.Position = position;
            input.IsFulltime = string.Equals(fulltimeText, "Full-time", StringComparison.OrdinalIgnoreCase);
            input.Address = address;
            input.Phone = phone;
            input.DateOfBirth = dateBorn;
            input.DateStartWork = dateStartWork;
            input.AccountId = account;
            input.Password = password;

            return true;
        }

        private void RefreshForm()
        {
            ID = string.Empty;
            Name = string.Empty;
            Position = string.Empty;
            Fulltime = "Full-time";
            Address = string.Empty;
            Phone = string.Empty;
            DateBorn = string.Empty;
            DateStartWork = string.Empty;
            Account = string.Empty;
            Password = string.Empty;
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
