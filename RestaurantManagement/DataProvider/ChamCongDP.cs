using QuanLyNhaHang.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Globalization;

namespace QuanLyNhaHang.DataProvider
{
    public class ChamCongDP : DBConnection
    {
        public sealed class AttendanceExportRow
        {
            public AttendanceExportRow(string employeeName, DateTime day, double hours, string note)
            {
                EmployeeName = employeeName;
                Day = day;
                Hours = hours;
                Note = note;
            }

            public string EmployeeName { get; }
            public DateTime Day { get; }
            public double Hours { get; }
            public string Note { get; }
        }

        private static ChamCongDP flag;
        public static ChamCongDP Flag
        {
            get
            {
                if (flag == null)
                {
                    flag = new ChamCongDP();
                }

                return flag;
            }
        }

        public ObservableCollection<NhanVienCC> GetEmployees()
        {
            ObservableCollection<NhanVienCC> employees = new ObservableCollection<NhanVienCC>();
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand(
                    "SELECT MaNV, TenNV, ChucVu, Fulltime FROM NHANVIEN ORDER BY MaNV ASC",
                    SqlCon);
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string ma = reader.GetString(0);
                    string ten = reader.GetString(1);
                    string chucVu = reader.GetString(2);
                    string fullTime = reader.GetBoolean(3) ? "Full-time" : "Part-time";
                    employees.Add(new NhanVienCC(ma, ten, chucVu, fullTime));
                }
            }
            finally
            {
                DBClose();
            }

            return employees;
        }

        public Dictionary<string, float> GetMonthlyAttendanceSummary(int month, int year)
        {
            Dictionary<string, float> result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand(
                    "SELECT MaNV, SUM(SoGioCong) FROM CHITIETCHAMCONG " +
                    "WHERE MONTH(NgayCC) = @month AND YEAR(NgayCC) = @year " +
                    "GROUP BY MaNV ORDER BY MaNV ASC",
                    SqlCon);
                cmd.Parameters.AddWithValue("@month", month);
                cmd.Parameters.AddWithValue("@year", year);
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string ma = reader.GetString(0);
                    float tongGio = (float)Convert.ToDouble(reader.GetValue(1));
                    result[ma] = tongGio;
                }
            }
            finally
            {
                DBClose();
            }

            return result;
        }

        public Dictionary<string, ChamCong> GetAttendanceByDay(DateTime day)
        {
            Dictionary<string, ChamCong> result = new Dictionary<string, ChamCong>(StringComparer.OrdinalIgnoreCase);
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand(
                    "SELECT MaNV, NgayCC, SoGioCong, GhiChu " +
                    "FROM CHITIETCHAMCONG WHERE CONVERT(date, NgayCC) = @ngayCC ORDER BY MaNV ASC",
                    SqlCon);
                cmd.Parameters.AddWithValue("@ngayCC", day.Date);
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string maNv = reader.GetString(0);
                    string ngay = reader.GetDateTime(1).ToShortDateString();
                    string soGio = Convert.ToDouble(reader.GetValue(2)).ToString(CultureInfo.InvariantCulture);
                    string ghiChu = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                    result[maNv] = new ChamCong(maNv, ngay, soGio, ghiChu);
                }
            }
            finally
            {
                DBClose();
            }

            return result;
        }

        public Dictionary<string, DateTime> GetEmployeeStartDates()
        {
            Dictionary<string, DateTime> result = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand(
                    "SELECT MaNV, NgayVaoLam FROM NHANVIEN ORDER BY MaNV ASC",
                    SqlCon);
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result[reader.GetString(0)] = reader.GetDateTime(1).Date;
                }
            }
            finally
            {
                DBClose();
            }

            return result;
        }

        public ObservableCollection<AttendanceExportRow> GetAttendanceRowsForMonth(int month, int year)
        {
            ObservableCollection<AttendanceExportRow> rows = new ObservableCollection<AttendanceExportRow>();
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand(
                    "SELECT n.TenNV, c.NgayCC, c.SoGioCong, c.GhiChu " +
                    "FROM CHITIETCHAMCONG AS c " +
                    "JOIN NHANVIEN AS n ON c.MaNV = n.MaNV " +
                    "WHERE MONTH(c.NgayCC) = @month AND YEAR(c.NgayCC) = @year " +
                    "ORDER BY c.MaNV, c.NgayCC ASC",
                    SqlCon);
                cmd.Parameters.AddWithValue("@month", month);
                cmd.Parameters.AddWithValue("@year", year);
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    rows.Add(new AttendanceExportRow(
                        reader.GetString(0),
                        reader.GetDateTime(1),
                        Convert.ToDouble(reader.GetValue(2)),
                        reader.IsDBNull(3) ? string.Empty : reader.GetString(3)));
                }
            }
            finally
            {
                DBClose();
            }

            return rows;
        }

        public bool SaveAttendanceByDay(DateTime day, IEnumerable<ChamCong> records)
        {
            DBOpen();
            SqlTransaction transaction = SqlCon.BeginTransaction();
            try
            {
                foreach (ChamCong record in records)
                {
                    double soGioCong = ParseHours(record.SoGioCong);
                    using SqlCommand upsertCmd = new SqlCommand(
                        "UPDATE CHITIETCHAMCONG " +
                        "SET SoGioCong = @soGioCong, GhiChu = @ghiChu " +
                        "WHERE MaNV = @maNV AND CONVERT(date, NgayCC) = @ngayCC; " +
                        "IF @@ROWCOUNT = 0 " +
                        "BEGIN " +
                        "INSERT INTO CHITIETCHAMCONG (MaNV, NgayCC, SoGioCong, GhiChu) VALUES (@maNV, @ngayCC, @soGioCong, @ghiChu) " +
                        "END",
                        SqlCon,
                        transaction);
                    upsertCmd.Parameters.AddWithValue("@maNV", record.MaNV);
                    upsertCmd.Parameters.AddWithValue("@ngayCC", day.Date);
                    upsertCmd.Parameters.AddWithValue("@soGioCong", soGioCong);
                    upsertCmd.Parameters.AddWithValue("@ghiChu", record.GhiChu ?? string.Empty);
                    upsertCmd.ExecuteNonQuery();
                }

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                DBClose();
            }
        }

        private static double ParseHours(string value)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double invariantResult))
            {
                return invariantResult;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out double currentResult))
            {
                return currentResult;
            }

            throw new InvalidOperationException("Số giờ công không hợp lệ.");
        }
    }
}
