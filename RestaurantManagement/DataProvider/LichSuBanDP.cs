using LichSuBan.Models;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;

namespace QuanLyNhaHang.DataProvider
{
    public class LichSuBanDP : DBConnection
    {
        private static LichSuBanDP flag;
        public static LichSuBanDP Flag
        {
            get
            {
                if (flag == null)
                {
                    flag = new LichSuBanDP();
                }

                return flag;
            }
        }

        public ObservableCollection<LichSuBanModel> GetAllSalesHistory()
        {
            const string query =
                "SELECT ct.SoHD, mn.MaMon, mn.TenMon, ct.SoLuong, mn.Gia, hd.NgayHD " +
                "FROM HOADON hd " +
                "JOIN CTHD ct ON hd.SoHD = ct.SoHD " +
                "JOIN MENU mn ON ct.MaMon = mn.MaMon";
            return ExecuteHistoryQuery(query, null);
        }

        public ObservableCollection<LichSuBanModel> SearchSalesHistoryByDishName(string keyword)
        {
            const string query =
                "SELECT ct.SoHD, mn.MaMon, mn.TenMon, ct.SoLuong, mn.Gia, hd.NgayHD " +
                "FROM HOADON hd " +
                "JOIN CTHD ct ON hd.SoHD = ct.SoHD " +
                "JOIN MENU mn ON ct.MaMon = mn.MaMon " +
                "WHERE mn.TenMon LIKE @keyword";
            return ExecuteHistoryQuery(query, cmd =>
            {
                cmd.Parameters.AddWithValue("@keyword", "%" + keyword + "%");
            });
        }

        public ObservableCollection<LichSuBanModel> GetSalesHistoryByDate(DateTime date)
        {
            const string query =
                "SELECT ct.SoHD, mn.MaMon, mn.TenMon, ct.SoLuong, mn.Gia, hd.NgayHD " +
                "FROM HOADON hd " +
                "JOIN CTHD ct ON hd.SoHD = ct.SoHD " +
                "JOIN MENU mn ON ct.MaMon = mn.MaMon " +
                "WHERE CONVERT(date, hd.NgayHD) = @ngay";
            return ExecuteHistoryQuery(query, cmd =>
            {
                cmd.Parameters.AddWithValue("@ngay", date.Date);
            });
        }

        public ObservableCollection<LichSuBanModel> GetSalesHistoryByMonth(int month)
        {
            const string query =
                "SELECT ct.SoHD, mn.MaMon, mn.TenMon, ct.SoLuong, mn.Gia, hd.NgayHD " +
                "FROM HOADON hd " +
                "JOIN CTHD ct ON hd.SoHD = ct.SoHD " +
                "JOIN MENU mn ON ct.MaMon = mn.MaMon " +
                "WHERE MONTH(hd.NgayHD) = @month";
            return ExecuteHistoryQuery(query, cmd =>
            {
                cmd.Parameters.AddWithValue("@month", month);
            });
        }

        private ObservableCollection<LichSuBanModel> ExecuteHistoryQuery(string query, Action<SqlCommand>? parameterize)
        {
            ObservableCollection<LichSuBanModel> result = new ObservableCollection<LichSuBanModel>();
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand(query, SqlCon);
                parameterize?.Invoke(cmd);
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int soHd = Convert.ToInt32(reader.GetValue(0));
                    string maMon = reader.GetString(1);
                    string tenMon = reader.GetString(2);
                    int soLuong = Convert.ToInt32(reader.GetValue(3));
                    decimal gia = reader.GetDecimal(4);
                    string ngayHd = reader.GetDateTime(5).ToShortDateString();
                    string triGia = (gia * soLuong).ToString();
                    result.Add(new LichSuBanModel(soHd, maMon, tenMon, soLuong, triGia, ngayHd));
                }
            }
            finally
            {
                DBClose();
            }

            return result;
        }
    }
}
