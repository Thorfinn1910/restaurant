using QuanLyNhaHang.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuanLyNhaHang.DataProvider
{
    public class CaiDatDP : DataProvider
    {
        private static CaiDatDP flag;
        public static CaiDatDP Flag
        {
            get
            {
                if (flag == null) flag = new CaiDatDP();
                return flag;
            }
            set
            {
                flag = value;
            }
        }
        public NhanVien GetCurrentEmployee(string MaNV, string pw)
        {
            NhanVien? nv = null;
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand(
                    "SELECT n.MaNV, n.TenNV, n.ChucVu, n.DiaChi, n.FullTime, n.SDT, n.NgayVaoLam, n.NgaySinh, " +
                    "       ISNULL(t.ID, '') AS AccountID, ISNULL(t.MatKhau, '') AS AccountPassword " +
                    "FROM NHANVIEN n " +
                    "LEFT JOIN TAIKHOAN t ON t.MaNV = n.MaNV " +
                    "WHERE n.MaNV = @maNV",
                    SqlCon);
                cmd.Parameters.AddWithValue("@maNV", MaNV);
                using SqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    string accountId = reader.GetString(8);
                    string accountPassword = reader.GetString(9);
                    string finalPassword = string.IsNullOrWhiteSpace(pw) ? accountPassword : pw;
                    nv = new NhanVien(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetBoolean(4),
                        reader.GetString(5),
                        reader.GetDateTime(6).ToShortDateString(),
                        reader.GetDateTime(7).ToShortDateString(),
                        accountId,
                        finalPassword);
                }
            }
            finally
            {
                DBClose();
            }

            if (nv == null)
            {
                throw new InvalidOperationException("Không tìm thấy nhân viên.");
            }

            return nv;
        }
        public void ChangePassword(string pw, string ID)
        {
            try
            {
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = "Update TaiKhoan set MatKhau = @password where ID = @id";
                cmd.Parameters.AddWithValue("@password", pw);
                cmd.Parameters.AddWithValue("@id", ID);
                DBOpen();

                cmd.Connection = SqlCon;
                cmd.ExecuteNonQuery();
            }
            finally
            {
                DBClose();
                MyMessageBox msb = new MyMessageBox("Đổi mật khẩu thành công!");
                msb.Show();
            }
             
        }
        
        public void UpdateInfo(string HoTen, string Diachi, string SDT, string NgaySinh, string MaNV)
        {
            try
            {
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = "Exec UPDATEINFO @hoten, @diachi, @manv, @sdt, @ngaysinh";
                cmd.Parameters.AddWithValue("@hoten", HoTen);
                cmd.Parameters.AddWithValue("@diachi", Diachi);
                cmd.Parameters.AddWithValue("@sdt", SDT);
                cmd.Parameters.AddWithValue("@ngaysinh", NgaySinh);
                cmd.Parameters.AddWithValue("@manv", MaNV);
                DBOpen();
                cmd.Connection = SqlCon;
                cmd.ExecuteNonQuery();
            }
            finally
            {
                DBClose();
                MyMessageBox msb = new MyMessageBox("Thay đổi và lưu thông tin thành công");
                msb.Show();
            }
        }
        public void LoadProfileImage(NhanVien nv)
        {
            try
            {
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = "Select AnhDaiDien from TaiKhoan where MaNV = @manv";
                cmd.Parameters.AddWithValue("@manv", nv.MaNV);
                DBOpen();
                cmd.Connection = SqlCon;
                SqlDataReader reader = cmd.ExecuteReader();
                if(reader.Read())
                {
                    nv.AnhDaiDien = Converter.ImageConverter.ConvertByteToBitmapImage((byte[])reader[0]);
                }
            }
            finally
            {
                DBClose();
            }
        }
        public void ChangeProfileImage_SaveToDB(NhanVien nv, string ID)
        {
            try
            {
                SqlCommand cmd = new SqlCommand();
 
                cmd.CommandText = "Update TaiKhoan set AnhDaiDien = @anhdaidien where ID = @id";
                cmd.Parameters.AddWithValue("@id", ID);
                cmd.Parameters.AddWithValue("@anhdaidien", Converter.ImageConverter.ConvertImageToBytes(nv.AnhDaiDien));

                DBOpen();
                cmd.Connection = SqlCon;
                cmd.ExecuteNonQuery();
            }
            finally
            {
                DBClose();
            }
        }
        #region complementary methods
        public string GetAccountIdByEmployee(string maNV)
        {
            if (string.IsNullOrWhiteSpace(maNV))
            {
                return string.Empty;
            }

            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand(
                    "SELECT TOP (1) ID FROM TaiKhoan WHERE MaNV = @maNV",
                    SqlCon);
                cmd.Parameters.AddWithValue("@maNV", maNV);
                object? value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? string.Empty : value.ToString() ?? string.Empty;
            }
            finally
            {
                DBClose();
            }
        }

        [Obsolete("Use GetAccountIdByEmployee(maNV) instead.")]
        public string getAccountIDFromTaiKhoan()
        {
            return string.Empty;
        }
        #endregion
    }
}
