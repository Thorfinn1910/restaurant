using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using QuanLyNhaHang.Models;
using TinhTrangBan.Models;

namespace QuanLyNhaHang.DataProvider
{
    public class MenuDP : DataProvider
    {
        public sealed class IngredientAvailabilityResult
        {
            public IngredientAvailabilityResult(
                IReadOnlyList<string> insufficientIngredients,
                IReadOnlyList<string> dishesWithoutRecipe)
            {
                InsufficientIngredients = insufficientIngredients;
                DishesWithoutRecipe = dishesWithoutRecipe;
            }

            public IReadOnlyList<string> InsufficientIngredients { get; }
            public IReadOnlyList<string> DishesWithoutRecipe { get; }
            public bool HasMissingRecipe => DishesWithoutRecipe.Count > 0;
            public bool HasInsufficientIngredient => InsufficientIngredients.Count > 0;
        }

        private static MenuDP flag;
        public static MenuDP Flag
        {
            get
            {
                if (flag == null) flag = new MenuDP();
                return flag;
            }
            set
            {
                flag = value;
            }
        }
        public async Task<ObservableCollection<MenuItem>> ConvertToCollection()
        {
            ObservableCollection<MenuItem> menuItems = new ObservableCollection<MenuItem>();
            try
            {
                DataTable dt = LoadInitialData("Select * from MENU");
                foreach (DataRow row in dt.Rows)
                {
                    string maMon = row["MaMon"].ToString();
                    string tenMon = row["TenMon"].ToString();
                    BitmapImage anhMon = Converter.ImageConverter.ConvertByteToBitmapImage((byte[])row["AnhMonAn"]);
                    Decimal gia = (Decimal)row["Gia"];
                    int thoiGianLam = (Int16)row["ThoiGianLam"];


                    menuItems.Add(new MenuItem(maMon, tenMon, gia, anhMon, thoiGianLam));
                }
            }
            finally
            {
                DBClose();
            }
            return menuItems;
        }
        public int CreateOpenOrderTransactional(int soBan, string maNV, IEnumerable<SelectedMenuItem> items)
        {
            if (string.IsNullOrWhiteSpace(maNV))
            {
                throw new InvalidOperationException("Không xác định được nhân viên đang thao tác.");
            }

            Dictionary<string, int> normalizedItems = NormalizeOrderItems(items);
            if (normalizedItems.Count == 0)
            {
                throw new InvalidOperationException("Order chưa có món.");
            }

            DBOpen();
            SqlTransaction transaction = SqlCon.BeginTransaction();
            try
            {
                EnsureTableCanCreateOpenOrder(soBan, transaction);
                ReserveIngredientsForOrder(normalizedItems, transaction);

                int soHd;
                using (SqlCommand insertBill = new SqlCommand(
                    "INSERT INTO HOADON (TriGia, MaNV, SoBan, NgayHD, TrangThai) " +
                    "OUTPUT INSERTED.SoHD VALUES (@triGia, @maNV, @soBan, @ngayHD, @trangThai)",
                    SqlCon,
                    transaction))
                {
                    insertBill.Parameters.AddWithValue("@triGia", 0m);
                    insertBill.Parameters.AddWithValue("@maNV", maNV);
                    insertBill.Parameters.AddWithValue("@soBan", soBan);
                    insertBill.Parameters.AddWithValue("@ngayHD", DateTime.Now);
                    insertBill.Parameters.AddWithValue("@trangThai", "Chưa trả");
                    soHd = Convert.ToInt32(insertBill.ExecuteScalar());
                }

                foreach (KeyValuePair<string, int> item in normalizedItems)
                {
                    using SqlCommand insertDetail = new SqlCommand(
                        "INSERT INTO CTHD (SoHD, MaMon, SoLuong) VALUES (@soHd, @maMon, @soLuong)",
                        SqlCon,
                        transaction);
                    insertDetail.Parameters.AddWithValue("@soHd", soHd);
                    insertDetail.Parameters.AddWithValue("@maMon", item.Key);
                    insertDetail.Parameters.AddWithValue("@soLuong", item.Value);
                    insertDetail.ExecuteNonQuery();

                    using SqlCommand insertChef = new SqlCommand(
                        "INSERT INTO CHEBIEN (MaMon, SoBan, SoLuong, NgayCB, TrangThai) VALUES (@maMon, @soBan, @soLuong, @ngayCB, @trangThai)",
                        SqlCon,
                        transaction);
                    insertChef.Parameters.AddWithValue("@maMon", item.Key);
                    insertChef.Parameters.AddWithValue("@soBan", soBan);
                    insertChef.Parameters.AddWithValue("@soLuong", item.Value);
                    insertChef.Parameters.AddWithValue("@ngayCB", DateTime.Now);
                    insertChef.Parameters.AddWithValue("@trangThai", "Đang chế biến");
                    insertChef.ExecuteNonQuery();
                }

                using (SqlCommand updateBill = new SqlCommand(
                    "UPDATE HOADON SET TriGia = ISNULL((SELECT SUM(c.SoLuong * m.Gia) FROM CTHD c JOIN MENU m ON m.MaMon = c.MaMon WHERE c.SoHD = @soHd), 0) WHERE SoHD = @soHd",
                    SqlCon,
                    transaction))
                {
                    updateBill.Parameters.AddWithValue("@soHd", soHd);
                    updateBill.ExecuteNonQuery();
                }

                using (SqlCommand updateTable = new SqlCommand(
                    "UPDATE BAN SET TrangThai = N'Đang được sử dụng' WHERE SoBan = @soBan",
                    SqlCon,
                    transaction))
                {
                    updateTable.Parameters.AddWithValue("@soBan", soBan);
                    updateTable.ExecuteNonQuery();
                }

                transaction.Commit();
                return soHd;
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
        public ObservableCollection<Table> GetTables()
        {
            ObservableCollection<Table> tables = new ObservableCollection<Table>();
            try
            {
                DataTable dt = LoadInitialData("Select * from BAN");
                int tinhtrang;
                foreach (DataRow dr in dt.Rows)
                {
                    if (String.Compare(dr["TrangThai"].ToString(), "Có thể sử dụng") == 0 ) tinhtrang = 1;
                    else tinhtrang = 0;
                    tables.Add(new Table { NumOfTable = dr["SoBan"].ToString(), Status = tinhtrang });
                }
            }
            finally
            {
                DBClose();
            }
            return tables;
        }

        public int GetOpenOrderByTable(int tableId)
        {
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand(
                    "SELECT TOP (1) SoHD FROM HOADON WHERE SoBan = @soBan AND TrangThai = N'Chưa trả' ORDER BY SoHD DESC",
                    SqlCon);
                cmd.Parameters.AddWithValue("@soBan", tableId);
                object? value = cmd.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                {
                    return 0;
                }

                return Convert.ToInt32(value);
            }
            finally
            {
                DBClose();
            }
        }

        public bool CanEditDishInOpenOrder(int soHd, string maMon)
        {
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand(
                    "SELECT CASE WHEN EXISTS (" +
                    "    SELECT 1 " +
                    "    FROM HOADON h " +
                    "    JOIN CHEBIEN c ON c.SoBan = h.SoBan " +
                    "    WHERE h.SoHD = @soHd " +
                    "      AND c.MaMon = @maMon " +
                    "      AND c.TrangThai IN (N'Đang chế biến', N'XONG')" +
                    ") THEN 0 ELSE 1 END",
                    SqlCon);
                cmd.Parameters.AddWithValue("@soHd", soHd);
                cmd.Parameters.AddWithValue("@maMon", maMon);
                return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
            }
            finally
            {
                DBClose();
            }
        }

        public ObservableCollection<SelectedMenuItem> GetOpenOrderItems(int soHd)
        {
            ObservableCollection<SelectedMenuItem> items = new ObservableCollection<SelectedMenuItem>();
            try
            {
                DBOpen();
                string query =
                    "SELECT c.MaMon, m.TenMon, m.Gia, c.SoLuong, " +
                    "CASE WHEN EXISTS (" +
                    "   SELECT 1 FROM CHEBIEN cb " +
                    "   JOIN HOADON h2 ON h2.SoBan = cb.SoBan " +
                    "   WHERE h2.SoHD = c.SoHD " +
                    "     AND cb.MaMon = c.MaMon " +
                    "     AND cb.TrangThai IN (N'Đang chế biến', N'XONG')" +
                    ") THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsLockedByChef " +
                    "FROM CTHD c " +
                    "JOIN MENU m ON m.MaMon = c.MaMon " +
                    "WHERE c.SoHD = @soHd";

                using SqlCommand cmd = new SqlCommand(query, SqlCon);
                cmd.Parameters.AddWithValue("@soHd", soHd);
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string maMon = reader.GetString(0);
                    string tenMon = reader.GetString(1);
                    decimal gia = reader.GetDecimal(2);
                    int soLuong = Convert.ToInt32(reader.GetValue(3));
                    bool isLocked = reader.GetBoolean(4);
                    items.Add(new SelectedMenuItem(maMon, tenMon, gia, soLuong, isLocked));
                }
            }
            finally
            {
                DBClose();
            }

            return items;
        }

        public void UpdateOpenOrder(int soHd, int soBan, ObservableCollection<SelectedMenuItem> updatedItems)
        {
            DBOpen();
            SqlTransaction transaction = SqlCon.BeginTransaction();
            try
            {
                EnsureOpenOrderExists(soHd, soBan, transaction);
                Dictionary<string, int> existing = GetExistingOrderDetails(soHd, transaction);
                HashSet<string> lockedDishIds = GetLockedDishIdsForOrder(soHd, transaction);

                Dictionary<string, SelectedMenuItem> updated = new Dictionary<string, SelectedMenuItem>(StringComparer.OrdinalIgnoreCase);
                foreach (SelectedMenuItem item in updatedItems)
                {
                    if (item.Quantity <= 0)
                    {
                        throw new InvalidOperationException("Số lượng món phải lớn hơn 0.");
                    }

                    updated[item.ID] = item;
                }

                foreach (string lockedDishId in lockedDishIds)
                {
                    int oldQty = existing.TryGetValue(lockedDishId, out int oldVal) ? oldVal : 0;
                    int newQty = updated.TryGetValue(lockedDishId, out SelectedMenuItem? newItem) ? newItem.Quantity : 0;
                    if (newQty < oldQty)
                    {
                        throw new InvalidOperationException("Món đã báo bếp chỉ có thể gọi thêm, không thể giảm hoặc xóa.");
                    }
                }

                HashSet<string> allDishIds = new HashSet<string>(existing.Keys, StringComparer.OrdinalIgnoreCase);
                foreach (string dishId in updated.Keys)
                {
                    allDishIds.Add(dishId);
                }

                foreach (string dishId in allDishIds)
                {
                    int oldQty = existing.TryGetValue(dishId, out int oldValue) ? oldValue : 0;
                    int newQty = updated.TryGetValue(dishId, out SelectedMenuItem? newItem) ? newItem.Quantity : 0;
                    int delta = newQty - oldQty;

                    if (delta == 0)
                    {
                        continue;
                    }

                    if (delta < 0 && lockedDishIds.Contains(dishId))
                    {
                        throw new InvalidOperationException("Món đã báo bếp chỉ có thể gọi thêm, không thể giảm hoặc xóa.");
                    }

                    AdjustIngredientStockForDish(dishId, delta, transaction);

                    if (oldQty == 0 && newQty > 0)
                    {
                        using SqlCommand insertDetail = new SqlCommand(
                            "INSERT INTO CTHD (SoHD, MaMon, SoLuong) VALUES (@soHd, @maMon, @soLuong)",
                            SqlCon,
                            transaction);
                        insertDetail.Parameters.AddWithValue("@soHd", soHd);
                        insertDetail.Parameters.AddWithValue("@maMon", dishId);
                        insertDetail.Parameters.AddWithValue("@soLuong", newQty);
                        insertDetail.ExecuteNonQuery();
                    }
                    else if (newQty == 0)
                    {
                        using SqlCommand deleteDetail = new SqlCommand(
                            "DELETE FROM CTHD WHERE SoHD = @soHd AND MaMon = @maMon",
                            SqlCon,
                            transaction);
                        deleteDetail.Parameters.AddWithValue("@soHd", soHd);
                        deleteDetail.Parameters.AddWithValue("@maMon", dishId);
                        deleteDetail.ExecuteNonQuery();
                    }
                    else
                    {
                        using SqlCommand updateDetail = new SqlCommand(
                            "UPDATE CTHD SET SoLuong = @soLuong WHERE SoHD = @soHd AND MaMon = @maMon",
                            SqlCon,
                            transaction);
                        updateDetail.Parameters.AddWithValue("@soLuong", newQty);
                        updateDetail.Parameters.AddWithValue("@soHd", soHd);
                        updateDetail.Parameters.AddWithValue("@maMon", dishId);
                        updateDetail.ExecuteNonQuery();
                    }

                    if (delta > 0)
                    {
                        using SqlCommand insertChef = new SqlCommand(
                            "INSERT INTO CHEBIEN (MaMon, SoBan, SoLuong, NgayCB, TrangThai) VALUES (@maMon, @soBan, @soLuong, @ngayCB, @trangThai)",
                            SqlCon,
                            transaction);
                        insertChef.Parameters.AddWithValue("@maMon", dishId);
                        insertChef.Parameters.AddWithValue("@soBan", soBan);
                        insertChef.Parameters.AddWithValue("@soLuong", delta);
                        insertChef.Parameters.AddWithValue("@ngayCB", DateTime.Now);
                        insertChef.Parameters.AddWithValue("@trangThai", "Đang chế biến");
                        insertChef.ExecuteNonQuery();
                    }
                }

                using (SqlCommand updateBill = new SqlCommand(
                    "UPDATE HOADON SET TriGia = ISNULL((SELECT SUM(c.SoLuong * m.Gia) FROM CTHD c JOIN MENU m ON m.MaMon = c.MaMon WHERE c.SoHD = @soHd), 0) WHERE SoHD = @soHd",
                    SqlCon,
                    transaction))
                {
                    updateBill.Parameters.AddWithValue("@soHd", soHd);
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
        
        public void AddDish(MenuItem x)
        {
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = "INSERT INTO MENU VALUES (@MaMon, @TenMon, @Gia, @AnhMonAn, @ThoiGianLam)";
                cmd.Parameters.AddWithValue("@MaMon", x.ID);
                cmd.Parameters.AddWithValue("@TenMon", x.FoodName);
                cmd.Parameters.AddWithValue("@AnhMonAn", Converter.ImageConverter.ConvertImageToBytes(x.FoodImage));
                cmd.Parameters.AddWithValue("@Gia", x.Price);
                cmd.Parameters.AddWithValue("@ThoiGianLam", x.CookingTime);

                cmd.Connection = SqlCon;
                cmd.ExecuteNonQuery();
            }
            finally
            {
                DBClose();
            }
        }
        public void RemoveDish(string MaMon)
        {
            HardDeleteDishCascade(MaMon);
        }

        public void HardDeleteDishCascade(string maMon)
        {
            if (string.IsNullOrWhiteSpace(maMon))
            {
                throw new InvalidOperationException("Mã món không hợp lệ.");
            }

            DBOpen();
            SqlTransaction transaction = SqlCon.BeginTransaction();
            try
            {
                EnsureDishExists(maMon, transaction);

                TableRef menuTable = GetMenuTable(transaction);
                List<ForeignKeyRelation> relations = LoadForeignKeyRelations(transaction);
                List<DeletePlan> deletePlans = BuildDeletePlans(menuTable, relations);

                foreach (DeletePlan plan in deletePlans
                    .OrderByDescending(p => p.MaxDepth)
                    .ThenBy(p => p.TargetTable.Schema, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.TargetTable.Name, StringComparer.OrdinalIgnoreCase))
                {
                    ExecuteDeletePlan(plan, maMon, transaction);
                }

                DeleteDishRow(maMon, transaction);
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
        public MenuItem GetDishInfo(string MaMon)
        {
            MenuItem X = null;
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = "Select * from MENU where MaMon = @mamon";
                cmd.Parameters.AddWithValue("@mamon", MaMon);
                cmd.Connection = SqlCon;

                SqlDataReader reader = cmd.ExecuteReader();
                if(reader.Read())
                {
                   X = new MenuItem(reader.GetString(0), reader.GetString(1), reader.GetDecimal(2), Converter.ImageConverter.ConvertByteToBitmapImage((byte[])reader[3]), reader.GetInt16(4));
                }
            }
            finally
            {
                DBClose();
            }
            return X;
        }
        public void EditDishInfo(MenuItem item)
        {
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = "Update MENU set TenMon = @tenmon, AnhMonAn = @anh, Gia = @gia, ThoiGianLam = @thoigian where MaMon = @mamon ";
                cmd.Parameters.AddWithValue("@mamon", item.ID);
                cmd.Parameters.AddWithValue("@anh", Converter.ImageConverter.ConvertImageToBytes(item.FoodImage));
                cmd.Parameters.AddWithValue("@thoigian", item.CookingTime);
                cmd.Parameters.AddWithValue("@tenmon", item.FoodName);
                cmd.Parameters.AddWithValue("@gia", item.Price);
                cmd.Connection = SqlCon;

                cmd.ExecuteNonQuery();
            }
            finally
            {
                DBClose();
            }
        }
        public ObservableCollection<Kho> GetIngredients()
        {
            ObservableCollection<Kho> NLs = new ObservableCollection<Kho>();
            try
            {
                DataTable dt = LoadInitialData("Select * from KHO");
                foreach(DataRow dr in dt.Rows)
                {
                    string tensp = dr["TenSanPham"].ToString();
                    float tondu = (float)Convert.ToDouble(dr["TonDu"]);
                    string donvi = dr["DonVi"].ToString();
                    string dongia = dr["DonGia"].ToString();
                    NLs.Add(new Kho(tensp, tondu, donvi, dongia));
                }
            }
            finally 
            {
                DBClose();
            }
            return NLs;
        }

        public ObservableCollection<ChiTietMon> getSumIngredients(ObservableCollection<SelectedMenuItem> arr)
        {
            ObservableCollection<ChiTietMon> ctm = new ObservableCollection<ChiTietMon>();
            try
            {
                Dictionary<string, int> normalizedItems = NormalizeOrderItems(arr);
                if (normalizedItems.Count == 0)
                {
                    return ctm;
                }

                DBOpen();
                Dictionary<string, double> requiredIngredients = GetRequiredIngredients(normalizedItems, null, out _);
                foreach (KeyValuePair<string, double> item in requiredIngredients)
                {
                    ctm.Add(new ChiTietMon(item.Key, (float)item.Value));
                }
            }
            finally
            {
                DBClose();

            }
            return ctm;
        }

        public IngredientAvailabilityResult CheckIngredientAvailability(IEnumerable<SelectedMenuItem> items)
        {
            Dictionary<string, int> normalizedItems = NormalizeOrderItems(items);
            if (normalizedItems.Count == 0)
            {
                return new IngredientAvailabilityResult(Array.Empty<string>(), Array.Empty<string>());
            }

            DBOpen();
            try
            {
                Dictionary<string, double> requiredIngredients = GetRequiredIngredients(normalizedItems, null, out HashSet<string> dishesWithoutRecipe);
                List<string> insufficientIngredients = new List<string>();
                foreach (KeyValuePair<string, double> ingredient in requiredIngredients)
                {
                    using SqlCommand stockCmd = new SqlCommand(
                        "SELECT TonDu FROM KHO WHERE TenSanPham = @tenSanPham",
                        SqlCon);
                    stockCmd.Parameters.AddWithValue("@tenSanPham", ingredient.Key);
                    object? stockValue = stockCmd.ExecuteScalar();
                    if (stockValue == null || stockValue == DBNull.Value)
                    {
                        insufficientIngredients.Add(ingredient.Key);
                        continue;
                    }

                    double currentStock = Convert.ToDouble(stockValue);
                    if (currentStock < ingredient.Value)
                    {
                        insufficientIngredients.Add(ingredient.Key);
                    }
                }

                return new IngredientAvailabilityResult(
                    insufficientIngredients.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    dishesWithoutRecipe.ToList());
            }
            finally
            {
                DBClose();
            }
        }

        public ObservableCollection<ChiTietMon> GetIngredientsForDish(string MaMon)
        {
            ObservableCollection<ChiTietMon> Ingredients = new ObservableCollection<ChiTietMon>();
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand(
                    "SELECT MaMon, TenNL, SoLuong FROM CHITIETMON WHERE MaMon = @maMon",
                    SqlCon);
                cmd.Parameters.AddWithValue("@maMon", MaMon);
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    Ingredients.Add(new ChiTietMon(
                        reader.GetString(1),
                        reader.GetString(0),
                        (float)Convert.ToDouble(reader.GetValue(2))));
                }
            }
            finally
            {
                DBClose();
            }
            return Ingredients;
        }
        public void SaveIngredients(ChiTietMon ctm)
        {
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = "Insert into CHITIETMON values (@mamon, @tennl, @soluong)";
                cmd.Parameters.AddWithValue("@mamon", ctm.MaMon);
                cmd.Parameters.AddWithValue("@tennl", ctm.TenNL);
                cmd.Parameters.AddWithValue("@soluong", ctm.SoLuong);
                cmd.Connection = SqlCon;

                cmd.ExecuteNonQuery();
            }finally
            {
                DBClose();
            }
        }
        public void UpdateIngredients_Kho(Kho item)
        {
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = "Update KHO set TonDu = @tondu where TenSanPham = @tensanpham";
                cmd.Parameters.AddWithValue("@tondu", item.TonDu);
                cmd.Parameters.AddWithValue("@tensanpham", item.TenSanPham);

                cmd.Connection = SqlCon;
                cmd.ExecuteNonQuery();
            } finally
            {
                DBClose();
            }
        }
        public int UpdateIngredients(ChiTietMon ctm)
        {
            int n = 0;
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = "Update CHITIETMON SET SoLuong = @soluong where TenNL = @tennl and MaMon = @mamon";
                cmd.Parameters.AddWithValue("@soluong", ctm.SoLuong);
                cmd.Parameters.AddWithValue("@tennl", ctm.TenNL);
                cmd.Parameters.AddWithValue("@mamon", ctm.MaMon);
                cmd.Connection = SqlCon;

                n = cmd.ExecuteNonQuery();
            }
            finally
            {
                DBClose();
            }
            return n;
        }
        public void RemoveIngredients(ChiTietMon ctm)
        {
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = "Delete from CHITIETMON where MaMon = @mamon and TenNL = @tennl";
                cmd.Parameters.AddWithValue("@mamon", ctm.MaMon);
                cmd.Parameters.AddWithValue("@tennl", ctm.TenNL);
                cmd.Connection = SqlCon;

                cmd.ExecuteNonQuery();
            }
            finally
            {
                DBClose();
            }
        }

        private void EnsureDishExists(string maMon, SqlTransaction transaction)
        {
            using SqlCommand checkDish = new SqlCommand(
                "SELECT COUNT(1) FROM MENU WITH (UPDLOCK, HOLDLOCK) WHERE MaMon = @maMon",
                SqlCon,
                transaction);
            checkDish.Parameters.AddWithValue("@maMon", maMon);
            if (Convert.ToInt32(checkDish.ExecuteScalar()) == 0)
            {
                throw new InvalidOperationException("Không tìm thấy món để xóa.");
            }
        }

        private TableRef GetMenuTable(SqlTransaction transaction)
        {
            const string query =
                "SELECT TOP (1) s.name AS SchemaName, t.name AS TableName " +
                "FROM sys.tables t " +
                "JOIN sys.schemas s ON t.schema_id = s.schema_id " +
                "WHERE t.name = 'MENU'";

            using SqlCommand cmd = new SqlCommand(query, SqlCon, transaction);
            using SqlDataReader reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException("Không tìm thấy bảng MENU trong cơ sở dữ liệu.");
            }

            return new TableRef(reader.GetString(0), reader.GetString(1));
        }

        private List<ForeignKeyRelation> LoadForeignKeyRelations(SqlTransaction transaction)
        {
            const string query =
                "SELECT fk.object_id, fk.name, " +
                "       parentSchema.name AS ParentSchema, parentTable.name AS ParentTable, parentCol.name AS ParentColumn, " +
                "       refSchema.name AS RefSchema, refTable.name AS RefTable, refCol.name AS RefColumn, " +
                "       fkc.constraint_column_id " +
                "FROM sys.foreign_keys fk " +
                "JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id " +
                "JOIN sys.tables parentTable ON parentTable.object_id = fkc.parent_object_id " +
                "JOIN sys.schemas parentSchema ON parentSchema.schema_id = parentTable.schema_id " +
                "JOIN sys.columns parentCol ON parentCol.object_id = fkc.parent_object_id AND parentCol.column_id = fkc.parent_column_id " +
                "JOIN sys.tables refTable ON refTable.object_id = fkc.referenced_object_id " +
                "JOIN sys.schemas refSchema ON refSchema.schema_id = refTable.schema_id " +
                "JOIN sys.columns refCol ON refCol.object_id = fkc.referenced_object_id AND refCol.column_id = fkc.referenced_column_id " +
                "WHERE fk.is_disabled = 0 " +
                "ORDER BY fk.object_id, fkc.constraint_column_id";

            Dictionary<int, ForeignKeyRelation> map = new Dictionary<int, ForeignKeyRelation>();

            using SqlCommand cmd = new SqlCommand(query, SqlCon, transaction);
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int fkId = reader.GetInt32(0);
                if (!map.TryGetValue(fkId, out ForeignKeyRelation? relation))
                {
                    relation = new ForeignKeyRelation(
                        reader.GetString(1),
                        new TableRef(reader.GetString(5), reader.GetString(6)),
                        new TableRef(reader.GetString(2), reader.GetString(3)));
                    map[fkId] = relation;
                }

                relation.ColumnMappings.Add(new ColumnMapping(
                    reader.GetString(4),
                    reader.GetString(7)));
            }

            return map.Values.ToList();
        }

        private List<DeletePlan> BuildDeletePlans(TableRef rootTable, List<ForeignKeyRelation> relations)
        {
            Dictionary<TableRef, List<DeletePath>> planMap = new Dictionary<TableRef, List<DeletePath>>();
            Queue<DeletePath> queue = new Queue<DeletePath>();
            queue.Enqueue(new DeletePath(rootTable, new List<ForeignKeyRelation>()));

            while (queue.Count > 0)
            {
                DeletePath current = queue.Dequeue();
                TableRef currentTable = current.CurrentTable;

                foreach (ForeignKeyRelation nextRelation in relations.Where(r => r.FromTable.Equals(currentTable)))
                {
                    if (current.ContainsTable(nextRelation.ToTable))
                    {
                        continue;
                    }

                    List<ForeignKeyRelation> newEdges = new List<ForeignKeyRelation>(current.Edges) { nextRelation };
                    DeletePath nextPath = new DeletePath(rootTable, newEdges);
                    if (!nextPath.CurrentTable.Equals(rootTable))
                    {
                        if (!planMap.TryGetValue(nextPath.CurrentTable, out List<DeletePath>? pathList))
                        {
                            pathList = new List<DeletePath>();
                            planMap[nextPath.CurrentTable] = pathList;
                        }
                        pathList.Add(nextPath);
                    }

                    queue.Enqueue(nextPath);
                }
            }

            return planMap.Select(kv => new DeletePlan(kv.Key, kv.Value)).ToList();
        }

        private void ExecuteDeletePlan(DeletePlan plan, string maMon, SqlTransaction transaction)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("DELETE t FROM ");
            sql.Append(plan.TargetTable.ToSqlIdentifier());
            sql.Append(" t WHERE ");

            for (int i = 0; i < plan.Paths.Count; i++)
            {
                if (i > 0)
                {
                    sql.Append(" OR ");
                }
                sql.Append(BuildExistsClause(plan.Paths[i], i));
            }

            using SqlCommand cmd = new SqlCommand(sql.ToString(), SqlCon, transaction);
            cmd.Parameters.AddWithValue("@maMon", maMon);
            cmd.ExecuteNonQuery();
        }

        private string BuildExistsClause(DeletePath path, int pathIndex)
        {
            int depth = path.Edges.Count;
            if (depth <= 0)
            {
                return "1 = 0";
            }

            string aliasPrefix = "p" + pathIndex.ToString();
            string nearestParentAlias = aliasPrefix + "_0";
            StringBuilder clause = new StringBuilder();
            clause.Append("EXISTS (SELECT 1 FROM ");
            clause.Append(path.GetTableAt(depth - 1).ToSqlIdentifier());
            clause.Append(" ").Append(nearestParentAlias);

            for (int level = 1; level < depth; level++)
            {
                string descendantAlias = aliasPrefix + "_" + (level - 1).ToString();
                string ancestorAlias = aliasPrefix + "_" + level.ToString();
                TableRef ancestorTable = path.GetTableAt(depth - 1 - level);
                ForeignKeyRelation relation = path.Edges[depth - 1 - level];

                clause.Append(" INNER JOIN ");
                clause.Append(ancestorTable.ToSqlIdentifier());
                clause.Append(" ").Append(ancestorAlias);
                clause.Append(" ON ");
                clause.Append(BuildJoinCondition(descendantAlias, ancestorAlias, relation.ColumnMappings));
            }

            ForeignKeyRelation outerRelation = path.Edges[depth - 1];
            string rootAlias = aliasPrefix + "_" + (depth - 1).ToString();

            clause.Append(" WHERE ");
            clause.Append(BuildJoinCondition("t", nearestParentAlias, outerRelation.ColumnMappings));
            clause.Append(" AND ");
            clause.Append(rootAlias).Append(".").Append(QuoteIdentifier("MaMon")).Append(" = @maMon");
            clause.Append(")");
            return clause.ToString();
        }

        private static string BuildJoinCondition(string descendantAlias, string ancestorAlias, List<ColumnMapping> mappings)
        {
            StringBuilder join = new StringBuilder();
            for (int i = 0; i < mappings.Count; i++)
            {
                if (i > 0)
                {
                    join.Append(" AND ");
                }

                join.Append(descendantAlias)
                    .Append(".")
                    .Append(QuoteIdentifier(mappings[i].ParentColumn))
                    .Append(" = ")
                    .Append(ancestorAlias)
                    .Append(".")
                    .Append(QuoteIdentifier(mappings[i].ReferencedColumn));
            }
            return join.ToString();
        }

        private void DeleteDishRow(string maMon, SqlTransaction transaction)
        {
            using SqlCommand deleteDish = new SqlCommand("DELETE FROM MENU WHERE MaMon = @maMon", SqlCon, transaction);
            deleteDish.Parameters.AddWithValue("@maMon", maMon);
            int deletedCount = deleteDish.ExecuteNonQuery();
            if (deletedCount == 0)
            {
                throw new InvalidOperationException("Không tìm thấy món để xóa.");
            }
        }

        private void EnsureOpenOrderExists(int soHd, int soBan, SqlTransaction transaction)
        {
            using SqlCommand cmd = new SqlCommand(
                "SELECT COUNT(1) FROM HOADON WITH (UPDLOCK, HOLDLOCK) WHERE SoHD = @soHd AND SoBan = @soBan AND TrangThai = N'Chưa trả'",
                SqlCon,
                transaction);
            cmd.Parameters.AddWithValue("@soHd", soHd);
            cmd.Parameters.AddWithValue("@soBan", soBan);
            if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
            {
                throw new InvalidOperationException("Không tìm thấy order mở cho bàn đã chọn.");
            }
        }

        private Dictionary<string, int> GetExistingOrderDetails(int soHd, SqlTransaction transaction)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using SqlCommand cmd = new SqlCommand(
                "SELECT MaMon, SoLuong FROM CTHD WITH (UPDLOCK, HOLDLOCK) WHERE SoHD = @soHd",
                SqlCon,
                transaction);
            cmd.Parameters.AddWithValue("@soHd", soHd);
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result[reader.GetString(0)] = Convert.ToInt32(reader.GetValue(1));
            }
            return result;
        }

        private HashSet<string> GetLockedDishIdsForOrder(int soHd, SqlTransaction transaction)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using SqlCommand cmd = new SqlCommand(
                "SELECT DISTINCT c.MaMon " +
                "FROM CTHD c " +
                "JOIN HOADON h ON h.SoHD = c.SoHD " +
                "WHERE c.SoHD = @soHd " +
                "AND EXISTS (" +
                "   SELECT 1 FROM CHEBIEN cb " +
                "   WHERE cb.SoBan = h.SoBan " +
                "   AND cb.MaMon = c.MaMon " +
                "   AND cb.TrangThai IN (N'Đang chế biến', N'XONG')" +
                ")",
                SqlCon,
                transaction);
            cmd.Parameters.AddWithValue("@soHd", soHd);
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(reader.GetString(0));
            }
            return result;
        }

        private void AdjustIngredientStockForDish(string maMon, int deltaQuantity, SqlTransaction transaction)
        {
            if (deltaQuantity == 0)
            {
                return;
            }

            List<(string ingredientName, float recipeQuantity)> recipeRows = new List<(string ingredientName, float recipeQuantity)>();
            using (SqlCommand cmd = new SqlCommand(
                "SELECT TenNL, SoLuong FROM CHITIETMON WITH (UPDLOCK, HOLDLOCK) WHERE MaMon = @maMon",
                SqlCon,
                transaction))
            {
                cmd.Parameters.AddWithValue("@maMon", maMon);
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    recipeRows.Add((reader.GetString(0), (float)Convert.ToDouble(reader.GetValue(1))));
                }
            }

            if (deltaQuantity > 0 && recipeRows.Count == 0)
            {
                throw new InvalidOperationException("Món chưa có định mức nguyên liệu, không thể báo chế biến.");
            }

            foreach ((string ingredientName, float recipeQuantity) in recipeRows)
            {
                double quantityChange = recipeQuantity * deltaQuantity;

                using SqlCommand getStock = new SqlCommand(
                    "SELECT TonDu FROM KHO WITH (UPDLOCK, HOLDLOCK) WHERE TenSanPham = @tenSanPham",
                    SqlCon,
                    transaction);
                getStock.Parameters.AddWithValue("@tenSanPham", ingredientName);
                object? stockValue = getStock.ExecuteScalar();
                if (stockValue == null || stockValue == DBNull.Value)
                {
                    throw new InvalidOperationException("Không tìm thấy nguyên liệu trong kho: " + ingredientName);
                }

                double currentStock = Convert.ToDouble(stockValue);
                double newStock = currentStock - quantityChange;
                if (newStock < 0)
                {
                    throw new InvalidOperationException("Không đủ nguyên liệu trong kho: " + ingredientName);
                }

                using SqlCommand updateStock = new SqlCommand(
                    "UPDATE KHO SET TonDu = @tonDu WHERE TenSanPham = @tenSanPham",
                    SqlCon,
                    transaction);
                updateStock.Parameters.AddWithValue("@tonDu", newStock);
                updateStock.Parameters.AddWithValue("@tenSanPham", ingredientName);
                updateStock.ExecuteNonQuery();
            }
        }

        private void EnsureTableCanCreateOpenOrder(int soBan, SqlTransaction transaction)
        {
            using SqlCommand hasOpenOrder = new SqlCommand(
                "SELECT COUNT(1) FROM HOADON WITH (UPDLOCK, HOLDLOCK) WHERE SoBan = @soBan AND TrangThai = N'Chưa trả'",
                SqlCon,
                transaction);
            hasOpenOrder.Parameters.AddWithValue("@soBan", soBan);
            if (Convert.ToInt32(hasOpenOrder.ExecuteScalar()) > 0)
            {
                throw new InvalidOperationException("Bàn hiện đang có order mở.");
            }
        }

        private Dictionary<string, int> NormalizeOrderItems(IEnumerable<SelectedMenuItem> items)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (items == null)
            {
                return result;
            }

            foreach (SelectedMenuItem item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ID))
                {
                    continue;
                }

                if (item.Quantity <= 0)
                {
                    throw new InvalidOperationException("Số lượng món phải lớn hơn 0.");
                }

                result[item.ID] = result.TryGetValue(item.ID, out int qty) ? qty + item.Quantity : item.Quantity;
            }
            return result;
        }

        private Dictionary<string, double> GetRequiredIngredients(
            IReadOnlyDictionary<string, int> orderItems,
            SqlTransaction? transaction,
            out HashSet<string> dishesWithoutRecipe)
        {
            Dictionary<string, double> requiredIngredients = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            dishesWithoutRecipe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, int> item in orderItems)
            {
                bool hasRecipe = false;
                using SqlCommand recipeCmd = transaction == null
                    ? new SqlCommand("SELECT TenNL, SoLuong FROM CHITIETMON WHERE MaMon = @maMon", SqlCon)
                    : new SqlCommand("SELECT TenNL, SoLuong FROM CHITIETMON WITH (UPDLOCK, HOLDLOCK) WHERE MaMon = @maMon", SqlCon, transaction);

                recipeCmd.Parameters.AddWithValue("@maMon", item.Key);
                using SqlDataReader reader = recipeCmd.ExecuteReader();
                while (reader.Read())
                {
                    hasRecipe = true;
                    string ingredientName = reader.GetString(0);
                    double recipeQty = Convert.ToDouble(reader.GetValue(1));
                    double requiredQty = recipeQty * item.Value;
                    requiredIngredients[ingredientName] = requiredIngredients.TryGetValue(ingredientName, out double oldQty)
                        ? oldQty + requiredQty
                        : requiredQty;
                }

                if (!hasRecipe)
                {
                    dishesWithoutRecipe.Add(item.Key);
                }
            }

            return requiredIngredients;
        }

        private void ReserveIngredientsForOrder(IReadOnlyDictionary<string, int> orderItems, SqlTransaction transaction)
        {
            Dictionary<string, double> requiredIngredients = GetRequiredIngredients(orderItems, transaction, out HashSet<string> dishesWithoutRecipe);
            if (dishesWithoutRecipe.Count > 0)
            {
                throw new InvalidOperationException("Hãy thêm thông tin nguyên liệu cho món!");
            }

            foreach (KeyValuePair<string, double> ingredient in requiredIngredients)
            {
                using SqlCommand stockCmd = new SqlCommand(
                    "SELECT TonDu FROM KHO WITH (UPDLOCK, HOLDLOCK) WHERE TenSanPham = @tenSanPham",
                    SqlCon,
                    transaction);
                stockCmd.Parameters.AddWithValue("@tenSanPham", ingredient.Key);
                object? stockValue = stockCmd.ExecuteScalar();
                if (stockValue == null || stockValue == DBNull.Value)
                {
                    throw new InvalidOperationException("Không tìm thấy nguyên liệu trong kho: " + ingredient.Key);
                }

                double newStock = Convert.ToDouble(stockValue) - ingredient.Value;
                if (newStock < 0)
                {
                    throw new InvalidOperationException("Không đủ nguyên liệu trong kho: " + ingredient.Key);
                }

                using SqlCommand updateStock = new SqlCommand(
                    "UPDATE KHO SET TonDu = @tonDu WHERE TenSanPham = @tenSanPham",
                    SqlCon,
                    transaction);
                updateStock.Parameters.AddWithValue("@tonDu", newStock);
                updateStock.Parameters.AddWithValue("@tenSanPham", ingredient.Key);
                updateStock.ExecuteNonQuery();
            }
        }

        private static string QuoteIdentifier(string identifier)
        {
            return "[" + identifier.Replace("]", "]]") + "]";
        }

        private sealed class TableRef : IEquatable<TableRef>
        {
            public TableRef(string schema, string name)
            {
                Schema = schema;
                Name = name;
            }

            public string Schema { get; }
            public string Name { get; }

            public string ToSqlIdentifier()
            {
                return QuoteIdentifier(Schema) + "." + QuoteIdentifier(Name);
            }

            public bool Equals(TableRef? other)
            {
                return other != null
                    && string.Equals(Schema, other.Schema, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object? obj)
            {
                return Equals(obj as TableRef);
            }

            public override int GetHashCode()
            {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(Schema + "." + Name);
            }
        }

        private sealed class ColumnMapping
        {
            public ColumnMapping(string parentColumn, string referencedColumn)
            {
                ParentColumn = parentColumn;
                ReferencedColumn = referencedColumn;
            }

            public string ParentColumn { get; }
            public string ReferencedColumn { get; }
        }

        private sealed class ForeignKeyRelation
        {
            public ForeignKeyRelation(string name, TableRef fromTable, TableRef toTable)
            {
                Name = name;
                FromTable = fromTable;
                ToTable = toTable;
                ColumnMappings = new List<ColumnMapping>();
            }

            public string Name { get; }
            public TableRef FromTable { get; }
            public TableRef ToTable { get; }
            public List<ColumnMapping> ColumnMappings { get; }
        }

        private sealed class DeletePath
        {
            public DeletePath(TableRef rootTable, List<ForeignKeyRelation> edges)
            {
                RootTable = rootTable;
                Edges = edges;
            }

            public TableRef RootTable { get; }
            public List<ForeignKeyRelation> Edges { get; }
            public TableRef CurrentTable => Edges.Count == 0 ? RootTable : Edges[Edges.Count - 1].ToTable;

            public bool ContainsTable(TableRef table)
            {
                if (RootTable.Equals(table))
                {
                    return true;
                }

                return Edges.Any(e => e.ToTable.Equals(table));
            }

            public TableRef GetTableAt(int index)
            {
                if (index == 0)
                {
                    return RootTable;
                }

                return Edges[index - 1].ToTable;
            }
        }

        private sealed class DeletePlan
        {
            public DeletePlan(TableRef targetTable, List<DeletePath> paths)
            {
                TargetTable = targetTable;
                Paths = paths;
            }

            public TableRef TargetTable { get; }
            public List<DeletePath> Paths { get; }
            public int MaxDepth => Paths.Count == 0 ? 0 : Paths.Max(p => p.Edges.Count);
        }
        #region complementary functions
        public Decimal Calculate_Sum(Int16 Soban)
        {
            Decimal sum = 0;
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = "Exec GET_SUM_OF_PRICE_PD @soban";
                cmd.Parameters.AddWithValue("@soban", Soban);
                cmd.Connection = SqlCon;

                SqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    sum = reader.GetDecimal(0);
                }
                reader.Close();
                return sum;
            }
            finally
            {
                DBClose();
            }
        }
        [Obsolete("Use CreateOpenOrderTransactional or UpdateOpenOrder instead.")]
        public void Fill_CTHD(int soHd, string maMon, int soLuong)
        {
            if (soHd <= 0)
            {
                throw new InvalidOperationException("Số hóa đơn không hợp lệ.");
            }

            try
            {
                DBOpen();
                UpdateCTHD(soHd, maMon, soLuong);
            }
            finally
            {
                DBClose();
            }
        }

        public void UpdateCTHD(int soHd, string maMon, int soLuong)
        {
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand(
                    "UPDATE CTHD SET SoLuong = @soLuong WHERE SoHD = @soHd AND MaMon = @maMon",
                    SqlCon);
                cmd.Parameters.AddWithValue("@soLuong", soLuong);
                cmd.Parameters.AddWithValue("@soHd", soHd);
                cmd.Parameters.AddWithValue("@maMon", maMon);
                int affected = cmd.ExecuteNonQuery();
                if (affected == 0)
                {
                    using SqlCommand insert = new SqlCommand(
                        "INSERT INTO CTHD (SoHD, MaMon, SoLuong) VALUES (@soHd, @maMon, @soLuong)",
                        SqlCon);
                    insert.Parameters.AddWithValue("@soHd", soHd);
                    insert.Parameters.AddWithValue("@maMon", maMon);
                    insert.Parameters.AddWithValue("@soLuong", soLuong);
                    insert.ExecuteNonQuery();
                }

            }
            finally
            {
                DBClose();
            }
        }
        #endregion
    }
}
