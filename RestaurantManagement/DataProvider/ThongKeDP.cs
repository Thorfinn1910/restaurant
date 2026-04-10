using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuanLyNhaHang.Models;

namespace QuanLyNhaHang.DataProvider
{
    public class ThongKeDP : DataProvider
    {
        private static ThongKeDP flag;
        public static ThongKeDP Flag
        {
            get
            {
                if (flag == null) flag = new ThongKeDP();
                return flag;
            }
            set
            {
                flag = value;
            }
        }
        public double GetBillofDay(string day)
        {
            double d = 0;
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();
                cmd.Connection = SqlCon;
                cmd.CommandType = CommandType.Text;

                cmd.CommandText = "Select SUM(TriGia) from HOADON where CONVERT(date, NgayHD, 103) = @nghd and TrangThai = N'Đã trả'";
                cmd.Parameters.AddWithValue("@nghd", day);

                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    try
                    {
                        d = reader.GetSqlMoney(0).ToDouble();
                    }
                    catch
                    {
                        d = 0;
                    }
                }
                return d;
            }
            finally
            {
                DBClose();
            }
        }
        public double GetBillofMonth(string month, string year)
        {
            double d = 0;
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();
                cmd.Connection = SqlCon;
                cmd.CommandType = CommandType.Text;

                cmd.CommandText = "Select SUM(TriGia) from HOADON where MONTH(CONVERT(date, NgayHD, 103)) = @month and YEAR(CONVERT(date, NgayHD, 103)) = @year and TrangThai = N'Đã trả'";
                cmd.Parameters.AddWithValue("@month", month);
                cmd.Parameters.AddWithValue("@year", year);

                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    try
                    {
                        d = reader.GetSqlMoney(0).ToDouble();
                    }
                    catch
                    {
                        d = 0;
                    }
                }
                return d;
            }
            finally
            {
                DBClose();
            }
        }
        public double GetPaidofDay(string day)
        {
            double d = 0;
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();
                cmd.Connection = SqlCon;
                cmd.CommandType = CommandType.Text;

                cmd.CommandText = "Select SUM(TONG) from (select DonGia * SoLuong as TONG from CHITIETNHAP where CONVERT(date, NgayNhap, 103) = @ngnh) as TONGGIA";
                cmd.Parameters.AddWithValue("@ngnh", day);

                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    try
                    {
                        d = reader.GetDouble(0);
                    }
                    catch
                    {
                        d = 0;
                    }
                }
                return d;
            }
            finally
            {
                DBClose();
            }
        }
        public double GetPaidofMonth(string month, string year)
        {
            double d = 0;
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();
                cmd.Connection = SqlCon;
                cmd.CommandType = CommandType.Text;

                cmd.CommandText = "Select SUM(TONG) from (select DonGia * SoLuong as TONG from CHITIETNHAP where MONTH(CONVERT(date, NgayNhap, 103)) = @month and YEAR(CONVERT(date, NgayNhap, 103)) = @year) as TONGGIA";
                cmd.Parameters.AddWithValue("@month", month);
                cmd.Parameters.AddWithValue("@year", year);

                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    try
                    {
                        d = reader.GetDouble(0);
                    }
                    catch
                    {
                        d = 0;
                    }
                }
                return d;
            }
            finally
            {
                DBClose();
            }
        }
    }
}
