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
    public class TinhTrangBanDP : DataProvider
    {
        private static TinhTrangBanDP flag;
        public static TinhTrangBanDP Flag
        {
            get
            {
                if (flag == null) flag = new TinhTrangBanDP();
                return flag;
            }
            set
            {
                flag = value;
            }
        }
        public string LoadEachTableStatus(int ID)
        {
            string TableStatus = "";
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "Select TrangThai from BAN where SoBan = @SoBan";
                cmd.Parameters.AddWithValue("@SoBan", ID);

                cmd.Connection = SqlCon;
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    TableStatus = reader.GetString(0);
                }
                return TableStatus;
            }
            finally
            {
                DBClose();               
            }
        }
        public int LoadBill(int ID)
        {
            int bill = 0;
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();               
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "Select SoHD from HOADON where SoBan = @SoBan and TrangThai = N'Chưa trả'";
                cmd.Parameters.AddWithValue("@SoBan", ID);

                cmd.Connection = SqlCon;
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    bill = reader.GetInt16(0);
                }
                return bill;
            }
            finally
            {
                DBClose();
            }
        }
        public void UpdateTable(int ID, bool isEmpty)
        {
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();
                cmd.CommandType = CommandType.Text;

                cmd.Connection = SqlCon;               
                if (isEmpty)
                {
                    cmd.CommandText = "Update BAN set TrangThai = N'Có thể sử dụng' where SoBan = @SoBan";
                    cmd.Parameters.AddWithValue("@SoBan", ID);

                    cmd.ExecuteNonQuery();
                }
                else
                {
                    cmd.CommandText = "Update BAN set TrangThai = N'Đang được sử dụng' where SoBan = @SoBan";
                    cmd.Parameters.AddWithValue("@SoBan", ID);

                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                DBClose();
            }
        }
        public void UpdateBillStatus(int BillID)
        {
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();
                cmd.CommandType = CommandType.Text;
                cmd.Connection = SqlCon;
                
                cmd.CommandText = "Update HOADON set TrangThai = N'Đã trả' where SoHD = @SoHD";
                cmd.Parameters.AddWithValue("@SoHD", BillID);

                cmd.ExecuteNonQuery();
            }
            finally
            {
                DBClose();
            }
        }
        public void SwitchTable(int ID, int BillID)
        {
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();
                cmd.CommandType = CommandType.Text;
                cmd.Connection = SqlCon;

                cmd.CommandText = "Update HOADON set SoBan = @SoBan where SoHD = @SoHD";            
                cmd.Parameters.AddWithValue("@SoBan", ID);
                cmd.Parameters.AddWithValue("@SoHD", BillID);

                cmd.ExecuteNonQuery();
            }
            finally
            {
                DBClose();
            }
        }
    }
}
