using OfficeOpenXml;
using OfficeOpenXml.Style;
using QuanLyNhaHang.DataProvider;
using QuanLyNhaHang.Models;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Input;

namespace QuanLyNhaHang.ViewModel
{
    public class ChamCongViewModel : BaseViewModel
    {
        private ObservableCollection<string> _ListMonth = new ObservableCollection<string>();
        public ObservableCollection<string> ListMonth { get => _ListMonth; set { _ListMonth = value; OnPropertyChanged(); } }

        private string _MonthSelected = string.Empty;
        public string MonthSelected
        {
            get => _MonthSelected;
            set
            {
                _MonthSelected = value;
                OnPropertyChanged();
                ListViewDisplay();
                GetListDay();
                if (ListDay.Count > 0)
                {
                    DaySelected = ListDay[ListDay.Count - 1];
                }
            }
        }

        private ObservableCollection<string> _ListDay = new ObservableCollection<string>();
        public ObservableCollection<string> ListDay { get => _ListDay; set { _ListDay = value; OnPropertyChanged(); } }

        private string _DaySelected = string.Empty;
        public string DaySelected
        {
            get => _DaySelected;
            set
            {
                _DaySelected = value;
                OnPropertyChanged();
                GetListCheck();
            }
        }

        private ObservableCollection<NhanVienCC> _ListStaff = new ObservableCollection<NhanVienCC>();
        public ObservableCollection<NhanVienCC> ListStaff { get => _ListStaff; set { _ListStaff = value; OnPropertyChanged(); } }

        private ObservableCollection<ChamCong> _ListCheck = new ObservableCollection<ChamCong>();
        public ObservableCollection<ChamCong> ListCheck { get => _ListCheck; set { _ListCheck = value; OnPropertyChanged(); } }

        public ICommand CloseCM { get; set; }
        public ICommand ExportCM { get; set; }
        public ICommand SaveCM { get; set; }

        public ChamCongViewModel()
        {
            ListMonth = new ObservableCollection<string>();
            ListDay = new ObservableCollection<string>();
            ListStaff = new ObservableCollection<NhanVienCC>();
            ListCheck = new ObservableCollection<ChamCong>();

            GetListMonth();
            MonthSelected = DateTime.Now.Month + "/" + DateTime.Now.Year;
            ListViewDisplay();

            CloseCM = new RelayCommand<System.Windows.Window>((p) => true, (p) =>
            {
                if (p == null) return;
                p.Close();
            });

            ExportCM = new RelayCommand<object>((p) => true, (p) =>
            {
                string filePath = string.Empty;

                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Filter = "Excel (*.xlsx)|*.xlsx";
                dialog.FileName = "Bảng chấm công tháng " + DateTime.Now.Month + "-" + DateTime.Now.Year;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = dialog.FileName;

                    if (string.IsNullOrEmpty(filePath))
                    {
                        MyMessageBox mess = new MyMessageBox("Đường dẫn không hợp lệ!");
                        mess.ShowDialog();
                        return;
                    }

                    ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
                    int month = int.Parse(GetMonth(MonthSelected));
                    int year = DateTime.Now.Year;
                    ObservableCollection<ChamCongDP.AttendanceExportRow> rows = ChamCongDP.Flag.GetAttendanceRowsForMonth(month, year);

                    using (ExcelPackage x = new ExcelPackage())
                    {
                        x.Workbook.Properties.Title = "Chấm công tháng " + month + "/" + year;
                        x.Workbook.Worksheets.Add("Sheet");
                        ExcelWorksheet ws = x.Workbook.Worksheets[0];
                        ws.Cells.Style.Font.Name = "Times New Roman";

                        string[] columnHeader = { "Họ tên", "Ngày", "Số giờ", "Ghi chú" };
                        int countColumn = columnHeader.Count();
                        ws.Cells[1, 1].Value = "Bảng chấm công tháng " + month + "/" + year;
                        ws.Cells[1, 1, 1, countColumn].Merge = true;
                        ws.Cells[1, 1, 1, countColumn].Style.Font.Bold = true;
                        ws.Cells[1, 1, 1, countColumn].Style.Font.Size = 16;
                        ws.Cells[1, 1, 1, countColumn].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        int row = 2;
                        int col = 1;
                        foreach (string column in columnHeader)
                        {
                            var cell = ws.Cells[row, col];
                            cell.Value = column;
                            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                            col++;
                        }

                        foreach (ChamCongDP.AttendanceExportRow attendanceRow in rows)
                        {
                            row++;
                            col = 1;
                            string ten = attendanceRow.EmployeeName;
                            if (ws.Cells[row - 1, 1].Value != null && ten != ws.Cells[row - 1, 1].Value.ToString())
                            {
                                row++;
                            }

                            ws.Cells[row, col++].Value = ten;
                            ws.Cells[row, col++].Value = attendanceRow.Day.ToShortDateString();
                            ws.Cells[row, col++].Value = attendanceRow.Hours.ToString(CultureInfo.InvariantCulture);
                            ws.Cells[row, col++].Value = attendanceRow.Note;
                        }

                        row += 2;
                        ws.Cells[row, 2].Value = "Tổng số giờ";
                        ws.Cells[row, 2].Style.Font.Bold = true;
                        foreach (NhanVienCC nv in ListStaff)
                        {
                            row++;
                            col = 1;
                            ws.Cells[row, col++].Value = nv.HoTen;
                            ws.Cells[row, col++].Value = nv.TongSoGio;
                        }

                        byte[] bin = x.GetAsByteArray();
                        File.WriteAllBytes(filePath, bin);
                    }

                    MyMessageBox msb = new MyMessageBox("Xuất file thành công!");
                    msb.ShowDialog();
                }
            });

            SaveCM = new RelayCommand<object>((p) =>
            {
                foreach (ChamCong nv in ListCheck)
                {
                    if (string.IsNullOrEmpty(nv.SoGioCong)) return false;
                    if (!isFloat(nv.SoGioCong)) return false;
                }

                return true;
            }, (p) =>
            {
                try
                {
                    DateTime selectedDay = ParseSelectedDay();
                    bool result = ChamCongDP.Flag.SaveAttendanceByDay(selectedDay, ListCheck);
                    if (result)
                    {
                        MyMessageBox msb = new MyMessageBox("Chấm công thành công!");
                        msb.ShowDialog();
                    }
                    else
                    {
                        MyMessageBox msb = new MyMessageBox("Chấm công không thành công!");
                        msb.ShowDialog();
                    }

                    ListViewDisplay();
                }
                catch (Exception ex)
                {
                    MyMessageBox msb = new MyMessageBox(ex.Message);
                    msb.ShowDialog();
                }
            });
        }

        private void ListViewDisplay()
        {
            int month = int.Parse(GetMonth(MonthSelected));
            int year = DateTime.Now.Year;

            ListStaff = ChamCongDP.Flag.GetEmployees();
            var summary = ChamCongDP.Flag.GetMonthlyAttendanceSummary(month, year);
            foreach (NhanVienCC nv in ListStaff)
            {
                if (summary.TryGetValue(nv.MaNV, out float tonggio))
                {
                    nv.TongSoGio = tonggio;
                }
            }
        }

        private void GetListMonth()
        {
            ListMonth.Clear();
            int month = 1;
            while (month <= DateTime.Now.Month)
            {
                ListMonth.Add(month + "/" + DateTime.Now.Year);
                month++;
            }
        }

        private void GetListDay()
        {
            ListDay.Clear();
            int year = DateTime.Now.Year;
            int month = int.Parse(GetMonth(MonthSelected));

            int lastDay;
            if (month == DateTime.Now.Month)
            {
                lastDay = DateTime.Now.Day;
            }
            else
            {
                lastDay = DateTime.DaysInMonth(year, month);
            }

            for (int day = 1; day <= lastDay; day++)
            {
                ListDay.Add(new DateTime(year, month, day).ToString("M/d/yyyy"));
            }
        }

        private void GetListCheck()
        {
            DateTime selectedDay = ParseSelectedDay();
            ListCheck.Clear();
            foreach (NhanVienCC nv in ListStaff)
            {
                ListCheck.Add(new ChamCong(nv.MaNV));
            }

            var dayAttendance = ChamCongDP.Flag.GetAttendanceByDay(selectedDay);
            foreach (ChamCong nv in ListCheck)
            {
                if (dayAttendance.TryGetValue(nv.MaNV, out ChamCong? dbRow))
                {
                    nv.Set(dbRow.NgayCC, dbRow.SoGioCong, dbRow.GhiChu);
                }
            }

            var startDates = ChamCongDP.Flag.GetEmployeeStartDates();
            foreach (ChamCong nv in ListCheck)
            {
                if (startDates.TryGetValue(nv.MaNV, out DateTime startDate) && startDate > selectedDay.Date)
                {
                    nv.Set(selectedDay.ToShortDateString(), "0", "Chưa vào làm");
                }
            }
        }

        private DateTime ParseSelectedDay()
        {
            if (DateTime.TryParse(DaySelected, out DateTime parsed))
            {
                return parsed.Date;
            }

            string[] formats = { "M/d/yyyy", "d/M/yyyy", "MM/dd/yyyy", "dd/MM/yyyy" };
            if (DateTime.TryParseExact(DaySelected, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return parsed.Date;
            }

            return DateTime.Today.Date;
        }

        private string GetMonth(string dt)
        {
            string temp = string.Empty;
            int i = 0;
            while (i < dt.Length && dt[i] != '/')
            {
                temp += dt[i];
                i++;
            }

            return temp;
        }

        private bool isFloat(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return false;
            }

            if (s[0] < 48 || s[0] > 57) return false;
            if (s[s.Length - 1] < 48 || s[s.Length - 1] > 57) return false;
            int count = 0;
            int i = 1;
            while (i < s.Length)
            {
                if (s[i] == '.') count++;
                if ((s[i] < 48 || s[i] > 57) && s[i] != '.') return false;
                i++;
            }

            if (count > 1) return false;
            return true;
        }
    }
}
