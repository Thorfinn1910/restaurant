using QuanLyNhaHang.Models;
using System;
using System.Data;
using System.Data.SqlClient;

namespace QuanLyNhaHang.DataProvider
{
    public class KhoDP : DataProvider
    {
        private static KhoDP? flag;
        public static KhoDP Flag
        {
            get
            {
                flag ??= new KhoDP();
                return flag;
            }
            set => flag = value;
        }

        public bool ExistsStockInId(string stockInId)
        {
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand("SELECT COUNT(1) FROM CHITIETNHAP WHERE MaNhap = @maNhap", SqlCon);
                cmd.Parameters.AddWithValue("@maNhap", stockInId);
                int count = Convert.ToInt32(cmd.ExecuteScalar());
                return count > 0;
            }
            finally
            {
                DBClose();
            }
        }

        public bool ExistsWarehouseProduct(string productName)
        {
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand("SELECT COUNT(1) FROM KHO WHERE TenSanPham = @tenSanPham", SqlCon);
                cmd.Parameters.AddWithValue("@tenSanPham", productName);
                int count = Convert.ToInt32(cmd.ExecuteScalar());
                return count > 0;
            }
            finally
            {
                DBClose();
            }
        }

        public void CreateStockInEntry(StockInEntryInput input)
        {
            DBOpen();
            SqlTransaction transaction = SqlCon.BeginTransaction();
            try
            {
                using (SqlCommand duplicateCheck = new SqlCommand("SELECT COUNT(1) FROM CHITIETNHAP WITH (UPDLOCK, HOLDLOCK) WHERE MaNhap = @maNhap", SqlCon, transaction))
                {
                    duplicateCheck.Parameters.AddWithValue("@maNhap", input.StockInId);
                    if (Convert.ToInt32(duplicateCheck.ExecuteScalar()) > 0)
                    {
                        throw new InvalidOperationException("Mã nhập đã tồn tại.");
                    }
                }

                bool hasWarehouseItem;
                using (SqlCommand existsCmd = new SqlCommand("SELECT COUNT(1) FROM KHO WITH (UPDLOCK, HOLDLOCK) WHERE TenSanPham = @tenSanPham", SqlCon, transaction))
                {
                    existsCmd.Parameters.AddWithValue("@tenSanPham", input.ProductName);
                    hasWarehouseItem = Convert.ToInt32(existsCmd.ExecuteScalar()) > 0;
                }

                if (hasWarehouseItem)
                {
                    using SqlCommand updateWarehouse = new SqlCommand(
                        "UPDATE KHO SET TonDu = TonDu + @soLuong, DonVi = @donVi, DonGia = @donGia, Xoa = 0 WHERE TenSanPham = @tenSanPham",
                        SqlCon,
                        transaction);
                    updateWarehouse.Parameters.AddWithValue("@soLuong", input.Quantity);
                    updateWarehouse.Parameters.AddWithValue("@donVi", input.Unit);
                    updateWarehouse.Parameters.AddWithValue("@donGia", input.UnitPrice);
                    updateWarehouse.Parameters.AddWithValue("@tenSanPham", input.ProductName);
                    updateWarehouse.ExecuteNonQuery();
                }
                else
                {
                    using SqlCommand insertWarehouse = new SqlCommand(
                        "INSERT INTO KHO (TenSanPham, TonDu, DonVi, DonGia, Xoa) VALUES (@tenSanPham, @tonDu, @donVi, @donGia, 0)",
                        SqlCon,
                        transaction);
                    insertWarehouse.Parameters.AddWithValue("@tenSanPham", input.ProductName);
                    insertWarehouse.Parameters.AddWithValue("@tonDu", input.Quantity);
                    insertWarehouse.Parameters.AddWithValue("@donVi", input.Unit);
                    insertWarehouse.Parameters.AddWithValue("@donGia", input.UnitPrice);
                    insertWarehouse.ExecuteNonQuery();
                }

                using (SqlCommand insertHistory = new SqlCommand(
                    "INSERT INTO CHITIETNHAP (MaNhap, TenSanPham, DonVi, DonGia, SoLuong, NgayNhap, NguonNhap, LienLac) " +
                    "VALUES (@maNhap, @tenSanPham, @donVi, @donGia, @soLuong, @ngayNhap, @nguonNhap, @lienLac)",
                    SqlCon,
                    transaction))
                {
                    insertHistory.Parameters.AddWithValue("@maNhap", input.StockInId);
                    insertHistory.Parameters.AddWithValue("@tenSanPham", input.ProductName);
                    insertHistory.Parameters.AddWithValue("@donVi", input.Unit);
                    insertHistory.Parameters.AddWithValue("@donGia", input.UnitPrice);
                    insertHistory.Parameters.AddWithValue("@soLuong", input.Quantity);
                    insertHistory.Parameters.AddWithValue("@ngayNhap", input.DateIn);
                    insertHistory.Parameters.AddWithValue("@nguonNhap", input.Supplier);
                    insertHistory.Parameters.AddWithValue("@lienLac", input.SupplierContact);
                    insertHistory.ExecuteNonQuery();
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

        public void UpdateStockInEntry(string originalStockInId, StockInEntryInput input)
        {
            DBOpen();
            SqlTransaction transaction = SqlCon.BeginTransaction();
            try
            {
                string oldProductName = string.Empty;
                double oldQuantity = 0;

                using (SqlCommand readOriginal = new SqlCommand(
                    "SELECT TenSanPham, SoLuong FROM CHITIETNHAP WITH (UPDLOCK, HOLDLOCK) WHERE MaNhap = @maNhap",
                    SqlCon,
                    transaction))
                {
                    readOriginal.Parameters.AddWithValue("@maNhap", originalStockInId);
                    using SqlDataReader reader = readOriginal.ExecuteReader();
                    if (!reader.Read())
                    {
                        throw new InvalidOperationException("Không tìm thấy phiếu nhập để sửa.");
                    }
                    oldProductName = reader.GetString(0);
                    oldQuantity = Convert.ToDouble(reader[1]);
                }

                if (!string.Equals(input.StockInId, originalStockInId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Không được đổi mã nhập.");
                }
                if (!string.Equals(input.ProductName, oldProductName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Không được đổi tên sản phẩm.");
                }

                using (SqlCommand updateHistory = new SqlCommand(
                    "UPDATE CHITIETNHAP SET DonVi = @donVi, DonGia = @donGia, SoLuong = @soLuong, NgayNhap = @ngayNhap, NguonNhap = @nguonNhap, LienLac = @lienLac " +
                    "WHERE MaNhap = @maNhap",
                    SqlCon,
                    transaction))
                {
                    updateHistory.Parameters.AddWithValue("@maNhap", input.StockInId);
                    updateHistory.Parameters.AddWithValue("@donVi", input.Unit);
                    updateHistory.Parameters.AddWithValue("@donGia", input.UnitPrice);
                    updateHistory.Parameters.AddWithValue("@soLuong", input.Quantity);
                    updateHistory.Parameters.AddWithValue("@ngayNhap", input.DateIn);
                    updateHistory.Parameters.AddWithValue("@nguonNhap", input.Supplier);
                    updateHistory.Parameters.AddWithValue("@lienLac", input.SupplierContact);
                    updateHistory.ExecuteNonQuery();
                }

                double deltaQuantity = input.Quantity - oldQuantity;
                using (SqlCommand updateWarehouse = new SqlCommand(
                    "UPDATE KHO SET TonDu = TonDu + @delta, DonVi = @donVi, DonGia = @donGia, Xoa = 0 " +
                    "WHERE TenSanPham = @tenSanPham AND TonDu + @delta >= 0",
                    SqlCon,
                    transaction))
                {
                    updateWarehouse.Parameters.AddWithValue("@delta", deltaQuantity);
                    updateWarehouse.Parameters.AddWithValue("@donVi", input.Unit);
                    updateWarehouse.Parameters.AddWithValue("@donGia", input.UnitPrice);
                    updateWarehouse.Parameters.AddWithValue("@tenSanPham", input.ProductName);
                    int affectedRows = updateWarehouse.ExecuteNonQuery();
                    if (affectedRows == 0)
                    {
                        throw new InvalidOperationException("Số lượng sửa làm tồn kho âm hoặc nguyên liệu không tồn tại.");
                    }
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
