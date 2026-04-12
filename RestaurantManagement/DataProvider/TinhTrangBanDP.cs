using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;

namespace QuanLyNhaHang.DataProvider
{
    public class TinhTrangBanDP : DataProvider
    {
        public sealed class BillItemRow
        {
            public BillItemRow(string tenMon, int soLuong, decimal thanhTien)
            {
                TenMon = tenMon;
                SoLuong = soLuong;
                ThanhTien = thanhTien;
            }

            public string TenMon { get; }
            public int SoLuong { get; }
            public decimal ThanhTien { get; }
        }

        private const string TableAvailableStatus = "\u0043\u00f3 th\u1ec3 s\u1eed d\u1ee5ng";
        private const string TableInUseStatus = "\u0110ang \u0111\u01b0\u1ee3c s\u1eed d\u1ee5ng";
        private const string BillUnpaidStatus = "Ch\u01b0a tr\u1ea3";
        private const string BillPaidStatus = "\u0110\u00e3 tr\u1ea3";

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
            string tableStatus = string.Empty;
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand("Select TrangThai from BAN where SoBan = @SoBan", SqlCon);
                cmd.Parameters.AddWithValue("@SoBan", ID);
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    tableStatus = reader.GetString(0);
                }

                return tableStatus;
            }
            finally
            {
                DBClose();
            }
        }

        public bool IsTableAvailableStatus(string status)
        {
            return string.Equals(status, TableAvailableStatus, StringComparison.OrdinalIgnoreCase);
        }

        public int LoadBill(int ID)
        {
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand(
                    "Select TOP (1) SoHD from HOADON where SoBan = @SoBan and TrangThai = @trangThai order by SoHD desc",
                    SqlCon);
                cmd.Parameters.AddWithValue("@SoBan", ID);
                cmd.Parameters.AddWithValue("@trangThai", BillUnpaidStatus);
                object? value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            }
            finally
            {
                DBClose();
            }
        }

        public ObservableCollection<string> GetEmptyTables()
        {
            ObservableCollection<string> result = new ObservableCollection<string>();
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand(
                    "Select SoBan from BAN where TrangThai = @trangThai order by SoBan",
                    SqlCon);
                cmd.Parameters.AddWithValue("@trangThai", TableAvailableStatus);
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(Convert.ToInt32(reader.GetValue(0)).ToString());
                }
            }
            finally
            {
                DBClose();
            }

            return result;
        }

        public List<BillItemRow> GetBillItems(int soHd)
        {
            List<BillItemRow> items = new List<BillItemRow>();
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand(
                    "Select m.TenMon, c.SoLuong, m.Gia * c.SoLuong as ThanhTien " +
                    "from CTHD c inner join MENU m on c.MaMon = m.MaMon " +
                    "where c.SoHD = @soHd",
                    SqlCon);
                cmd.Parameters.AddWithValue("@soHd", soHd);
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    items.Add(new BillItemRow(
                        reader.GetString(0),
                        Convert.ToInt32(reader.GetValue(1)),
                        reader.GetDecimal(2)));
                }
            }
            finally
            {
                DBClose();
            }

            return items;
        }

        public bool BillExists(int soHd)
        {
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand("Select Count(1) from HOADON where SoHD = @soHd", SqlCon);
                cmd.Parameters.AddWithValue("@soHd", soHd);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
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
                using SqlCommand cmd = new SqlCommand("Update BAN set TrangThai = @trangThai where SoBan = @SoBan", SqlCon);
                cmd.Parameters.AddWithValue("@SoBan", ID);
                cmd.Parameters.AddWithValue("@trangThai", isEmpty ? TableAvailableStatus : TableInUseStatus);
                cmd.ExecuteNonQuery();
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
                using SqlCommand cmd = new SqlCommand("Update HOADON set TrangThai = @trangThai where SoHD = @SoHD", SqlCon);
                cmd.Parameters.AddWithValue("@SoHD", BillID);
                cmd.Parameters.AddWithValue("@trangThai", BillPaidStatus);
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
                using SqlCommand cmd = new SqlCommand("Update HOADON set SoBan = @SoBan where SoHD = @SoHD", SqlCon);
                cmd.Parameters.AddWithValue("@SoBan", ID);
                cmd.Parameters.AddWithValue("@SoHD", BillID);
                cmd.ExecuteNonQuery();
            }
            finally
            {
                DBClose();
            }
        }

        public void PayBillTransactional(int tableId, int billId)
        {
            DBOpen();
            SqlTransaction transaction = SqlCon.BeginTransaction();
            try
            {
                using (SqlCommand validateBill = new SqlCommand(
                    "SELECT COUNT(1) FROM HOADON WITH (UPDLOCK, HOLDLOCK) WHERE SoHD = @billId AND SoBan = @tableId AND TrangThai = @trangThai",
                    SqlCon,
                    transaction))
                {
                    validateBill.Parameters.AddWithValue("@billId", billId);
                    validateBill.Parameters.AddWithValue("@tableId", tableId);
                    validateBill.Parameters.AddWithValue("@trangThai", BillUnpaidStatus);
                    if (Convert.ToInt32(validateBill.ExecuteScalar()) == 0)
                    {
                        throw new InvalidOperationException("Không tìm thấy hóa đơn mở của bàn đã chọn.");
                    }
                }

                using (SqlCommand updateTable = new SqlCommand(
                    "Update BAN set TrangThai = @trangThai where SoBan = @SoBan",
                    SqlCon,
                    transaction))
                {
                    updateTable.Parameters.AddWithValue("@SoBan", tableId);
                    updateTable.Parameters.AddWithValue("@trangThai", TableAvailableStatus);
                    updateTable.ExecuteNonQuery();
                }

                using (SqlCommand updateBill = new SqlCommand(
                    "Update HOADON set TrangThai = @trangThai where SoHD = @SoHD",
                    SqlCon,
                    transaction))
                {
                    updateBill.Parameters.AddWithValue("@SoHD", billId);
                    updateBill.Parameters.AddWithValue("@trangThai", BillPaidStatus);
                    updateBill.ExecuteNonQuery();
                }

                transaction.Commit();
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

        public void SwitchTableTransactional(int fromTableId, int toTableId, int billId)
        {
            DBOpen();
            SqlTransaction transaction = SqlCon.BeginTransaction();
            try
            {
                using (SqlCommand validateFrom = new SqlCommand(
                    "SELECT COUNT(1) FROM HOADON WITH (UPDLOCK, HOLDLOCK) WHERE SoHD = @billId AND SoBan = @fromTableId AND TrangThai = @trangThai",
                    SqlCon,
                    transaction))
                {
                    validateFrom.Parameters.AddWithValue("@billId", billId);
                    validateFrom.Parameters.AddWithValue("@fromTableId", fromTableId);
                    validateFrom.Parameters.AddWithValue("@trangThai", BillUnpaidStatus);
                    if (Convert.ToInt32(validateFrom.ExecuteScalar()) == 0)
                    {
                        throw new InvalidOperationException("Không tìm thấy hóa đơn mở để chuyển bàn.");
                    }
                }

                using (SqlCommand validateTo = new SqlCommand(
                    "SELECT COUNT(1) FROM BAN WITH (UPDLOCK, HOLDLOCK) WHERE SoBan = @toTableId AND TrangThai = @trangThai",
                    SqlCon,
                    transaction))
                {
                    validateTo.Parameters.AddWithValue("@toTableId", toTableId);
                    validateTo.Parameters.AddWithValue("@trangThai", TableAvailableStatus);
                    if (Convert.ToInt32(validateTo.ExecuteScalar()) == 0)
                    {
                        throw new InvalidOperationException("Bàn chuyển đến không ở trạng thái trống.");
                    }
                }

                using (SqlCommand updateFromTable = new SqlCommand(
                    "Update BAN set TrangThai = @trangThai where SoBan = @SoBan",
                    SqlCon,
                    transaction))
                {
                    updateFromTable.Parameters.AddWithValue("@SoBan", fromTableId);
                    updateFromTable.Parameters.AddWithValue("@trangThai", TableAvailableStatus);
                    updateFromTable.ExecuteNonQuery();
                }

                using (SqlCommand updateBill = new SqlCommand(
                    "Update HOADON set SoBan = @toTableId where SoHD = @billId",
                    SqlCon,
                    transaction))
                {
                    updateBill.Parameters.AddWithValue("@toTableId", toTableId);
                    updateBill.Parameters.AddWithValue("@billId", billId);
                    updateBill.ExecuteNonQuery();
                }

                using (SqlCommand updateToTable = new SqlCommand(
                    "Update BAN set TrangThai = @trangThai where SoBan = @SoBan",
                    SqlCon,
                    transaction))
                {
                    updateToTable.Parameters.AddWithValue("@SoBan", toTableId);
                    updateToTable.Parameters.AddWithValue("@trangThai", TableInUseStatus);
                    updateToTable.ExecuteNonQuery();
                }

                transaction.Commit();
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
    }
}
