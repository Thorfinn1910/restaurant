using QuanLyNhaHang.Models;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;

namespace QuanLyNhaHang.DataProvider
{
    public class BepDP : DBConnection
    {
        private static BepDP flag;
        public static BepDP Flag
        {
            get
            {
                if (flag == null)
                {
                    flag = new BepDP();
                }

                return flag;
            }
        }

        public ObservableCollection<Bep> GetPreparingDishes()
        {
            return GetDishesByStatus("Đang chế biến");
        }

        public ObservableCollection<Bep> GetDoneDishes()
        {
            return GetDishesByStatus("XONG");
        }

        public bool MarkCooked(long maDatDon)
        {
            DBOpen();
            SqlTransaction transaction = SqlCon.BeginTransaction();
            try
            {
                using SqlCommand cmd = new SqlCommand(
                    "UPDATE CHEBIEN SET TrangThai = N'XONG' WHERE MaDatDon = @maDatDon AND TrangThai = N'Đang chế biến'",
                    SqlCon,
                    transaction);
                cmd.Parameters.AddWithValue("@maDatDon", maDatDon);
                int affected = cmd.ExecuteNonQuery();

                transaction.Commit();
                return affected > 0;
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

        public bool ServeDish(long maDatDon)
        {
            DBOpen();
            SqlTransaction transaction = SqlCon.BeginTransaction();
            try
            {
                using SqlCommand cmd = new SqlCommand(
                    "DELETE FROM CHEBIEN WHERE MaDatDon = @maDatDon AND TrangThai = N'XONG'",
                    SqlCon,
                    transaction);
                cmd.Parameters.AddWithValue("@maDatDon", maDatDon);
                int affected = cmd.ExecuteNonQuery();

                transaction.Commit();
                return affected > 0;
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

        public void ApplyStockConsumption(string maMon, int soLuong)
        {
            if (string.IsNullOrWhiteSpace(maMon) || soLuong <= 0)
            {
                return;
            }

            DBOpen();
            SqlTransaction transaction = SqlCon.BeginTransaction();
            try
            {
                using SqlCommand recipeCmd = new SqlCommand(
                    "SELECT TenNL, SoLuong FROM CHITIETMON WITH (UPDLOCK, HOLDLOCK) WHERE MaMon = @maMon",
                    SqlCon,
                    transaction);
                recipeCmd.Parameters.AddWithValue("@maMon", maMon);
                using SqlDataReader reader = recipeCmd.ExecuteReader();
                Collection<(string TenNL, double SoLuong)> recipes = new Collection<(string TenNL, double SoLuong)>();
                while (reader.Read())
                {
                    recipes.Add((reader.GetString(0), Convert.ToDouble(reader.GetValue(1))));
                }

                foreach ((string tenNl, double soLuongCongThuc) in recipes)
                {
                    double consumed = soLuongCongThuc * soLuong;
                    using SqlCommand getStock = new SqlCommand(
                        "SELECT TonDu FROM KHO WITH (UPDLOCK, HOLDLOCK) WHERE TenSanPham = @tenSanPham",
                        SqlCon,
                        transaction);
                    getStock.Parameters.AddWithValue("@tenSanPham", tenNl);
                    object? stockValue = getStock.ExecuteScalar();
                    if (stockValue == null || stockValue == DBNull.Value)
                    {
                        throw new InvalidOperationException("Không tìm thấy nguyên liệu trong kho: " + tenNl);
                    }

                    double newStock = Convert.ToDouble(stockValue) - consumed;
                    if (newStock < 0)
                    {
                        throw new InvalidOperationException("Không đủ nguyên liệu trong kho: " + tenNl);
                    }

                    using SqlCommand updateStock = new SqlCommand(
                        "UPDATE KHO SET TonDu = @tonDu WHERE TenSanPham = @tenSanPham",
                        SqlCon,
                        transaction);
                    updateStock.Parameters.AddWithValue("@tonDu", newStock);
                    updateStock.Parameters.AddWithValue("@tenSanPham", tenNl);
                    updateStock.ExecuteNonQuery();
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

        private ObservableCollection<Bep> GetDishesByStatus(string status)
        {
            ObservableCollection<Bep> dishes = new ObservableCollection<Bep>();
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand(
                    "SELECT c.MaDatDon, c.MaMon, c.SoLuong, c.SoBan, c.NgayCB, c.TrangThai, m.TenMon " +
                    "FROM CHEBIEN AS c JOIN MENU AS m ON c.MaMon = m.MaMon " +
                    "WHERE c.TrangThai = @trangThai " +
                    "ORDER BY c.NgayCB, m.ThoiGianLam",
                    SqlCon);
                cmd.Parameters.AddWithValue("@trangThai", status);
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    long maDatMon = Convert.ToInt64(reader.GetValue(0));
                    string maMon = reader.GetString(1);
                    int soLuong = Convert.ToInt32(reader.GetValue(2));
                    int soBan = Convert.ToInt32(reader.GetValue(3));
                    string ngayCb = reader.GetDateTime(4).ToShortDateString();
                    string trangThai = reader.GetString(5);
                    string tenMon = reader.GetString(6);
                    dishes.Add(new Bep(maDatMon, maMon, soBan, soLuong, ngayCb, trangThai, tenMon));
                }
            }
            finally
            {
                DBClose();
            }

            return dishes;
        }
    }
}
