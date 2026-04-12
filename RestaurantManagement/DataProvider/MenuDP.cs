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
        public void InformChef(string maMon, int soban, int soluong)
        {
            try
            {
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = "Exec Inform_Chef_PD @mamon, @soban, @soluong, @ngaycb, @trangthai, @trangthaiban";
                cmd.Parameters.AddWithValue("@mamon", maMon);
                cmd.Parameters.AddWithValue("@soban", soban);
                cmd.Parameters.AddWithValue("@soluong", soluong);
                cmd.Parameters.AddWithValue("@ngaycb", DateTime.Now);
                cmd.Parameters.AddWithValue("@trangthai", "Đang chế biến");
                cmd.Parameters.AddWithValue("@trangthaiban", "Đang được sử dụng");
                DBOpen();
                cmd.Connection = SqlCon;
                cmd.ExecuteNonQuery();
            }
            finally
            {
                DBClose();
            }
        }

        public void PayABill(Int16 soban, Decimal sum, string MaNV)
        {
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = "Exec PAY_A_BILL_PD @trigia, @manv, @soban, @ngayHD, @trangthai";
                cmd.Parameters.AddWithValue("@trigia", sum);
                cmd.Parameters.AddWithValue("@manv", MaNV);
                cmd.Parameters.AddWithValue("@soban", soban);
                cmd.Parameters.AddWithValue("@ngayHD", DateTime.Now);
                cmd.Parameters.AddWithValue("@trangthai", "Chưa trả");
                DBOpen();
                cmd.Connection = SqlCon;
                
                cmd.ExecuteNonQuery();
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
                string fullQuery = string.Empty;
                string outerquery = "select T.TenNL, SUM(T.SoLuong) as Tong from ( ";
                string endquery = " ) as T group by T.TenNL";
                string query = "select TenNL, SoLuong from CHITIETMON where ";
                query += $"MaMon = '{arr[0].ID}'";
                if(arr.Count > 1)
                {
                    for (int i = 1; i < arr.Count; i++)
                    {
                        query += $" or MaMon = '{arr[i].ID}'";
                    }
                }
                fullQuery = outerquery + query + endquery;
                DataTable dt = LoadInitialData(fullQuery);
                foreach(DataRow dr in dt.Rows)
                {
                    ctm.Add(new ChiTietMon(dr["TenNL"].ToString(), (float)Convert.ToDouble(dr["Tong"])));
                }
            } finally
            {
                DBClose();

            }
            return ctm;
        }
        public ObservableCollection<ChiTietMon> GetIngredientsForDish(string MaMon)
        {
            ObservableCollection<ChiTietMon> Ingredients = new ObservableCollection<ChiTietMon>();
            try
            {
                DataTable dt = LoadInitialData($"Select * from CHITIETMON where MaMon = '{MaMon}'");
                foreach(DataRow dr in dt.Rows)
                {
                    Ingredients.Add(new ChiTietMon(dr["TenNL"].ToString(), dr["MaMon"].ToString(), (float)Convert.ToDouble(dr["SoLuong"])));
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
        public void Fill_CTHD(string MaMon, int SoLuong)
        {
            try
            {
                DBOpen();
                SqlCommand cmd_InsertDetail = new SqlCommand();
                cmd_InsertDetail.CommandText = "Exec INSERT_DETAIL_PD @mamon, @soluong";
                cmd_InsertDetail.Parameters.AddWithValue("@mamon", MaMon);
                cmd_InsertDetail.Parameters.AddWithValue("@soluong", SoLuong);
                DBOpen();
                cmd_InsertDetail.Connection = SqlCon;

                cmd_InsertDetail.ExecuteNonQuery();
            }
            catch (SqlException ex)
            {
                UpdateCTHD(MaMon, SoLuong);
            }
            finally
            {
                DBClose();
            }
            
        }
        public void UpdateCTHD(string MaMon, int SoLuong)
        {
            try
            {
                DBOpen();
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = "UPDATE CTHD " +
                                  "SET SOLUONG = @soluong " +
                                  "WHERE SoHD = (SELECT IDENT_CURRENT('HOADON')) AND MaMon = @mamon";
                cmd.Parameters.AddWithValue("@mamon", MaMon);
                cmd.Parameters.AddWithValue("@soluong", SoLuong);

                cmd.Connection = SqlCon;
                cmd.ExecuteNonQuery();

            }
            finally
            {
                DBClose();
            }
        }
        #endregion
    }
}
