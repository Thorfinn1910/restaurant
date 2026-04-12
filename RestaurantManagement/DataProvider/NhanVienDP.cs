using QuanLyNhaHang.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace QuanLyNhaHang.DataProvider
{
    public class NhanVienDP : DataProvider
    {
        private static NhanVienDP? flag;
        public static NhanVienDP Flag
        {
            get
            {
                flag ??= new NhanVienDP();
                return flag;
            }
            set => flag = value;
        }

        public ObservableCollection<NhanVien> GetEmployees(string keyword)
        {
            ObservableCollection<NhanVien> result = new ObservableCollection<NhanVien>();
            try
            {
                DBOpen();
                string query =
                    "SELECT n.MaNV, n.TenNV, n.ChucVu, n.Fulltime, n.DiaChi, n.SDT, n.NgaySinh, n.NgayVaoLam, t.ID, t.MatKhau " +
                    "FROM NHANVIEN AS n " +
                    "LEFT JOIN TAIKHOAN AS t ON n.MaNV = t.MaNV " +
                    "WHERE (@keyword = '' OR n.TenNV LIKE N'%' + @keyword + N'%') " +
                    "ORDER BY n.TenNV";

                using SqlCommand cmd = new SqlCommand(query, SqlCon);
                cmd.Parameters.AddWithValue("@keyword", keyword ?? string.Empty);
                using SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    string id = reader.GetString(0);
                    string ten = reader.GetString(1);
                    string chucvu = reader.GetString(2);
                    bool fulltime = reader.GetBoolean(3);
                    string diachi = reader.GetString(4);
                    string sdt = reader.GetString(5);
                    string ngsinh = reader.GetDateTime(6).ToShortDateString();
                    string ngvl = reader.GetDateTime(7).ToShortDateString();
                    string taikhoan = reader.IsDBNull(8) ? string.Empty : reader.GetString(8);
                    string matkhau = reader.IsDBNull(9) ? string.Empty : reader.GetString(9);

                    result.Add(new NhanVien(id, ten, chucvu, diachi, fulltime, sdt, ngvl, ngsinh, taikhoan, matkhau));
                }
            }
            finally
            {
                DBClose();
            }

            return result;
        }

        public bool ExistsEmployeeId(string employeeId)
        {
            try
            {
                DBOpen();
                using SqlCommand cmd = new SqlCommand("SELECT COUNT(1) FROM NHANVIEN WHERE MaNV = @maNV", SqlCon);
                cmd.Parameters.AddWithValue("@maNV", employeeId);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
            finally
            {
                DBClose();
            }
        }

        public bool ExistsAccountId(string accountId, string excludeEmployeeId = "")
        {
            try
            {
                DBOpen();
                string query = "SELECT COUNT(1) FROM TAIKHOAN WHERE ID = @id AND (@exclude = '' OR MaNV <> @exclude)";
                using SqlCommand cmd = new SqlCommand(query, SqlCon);
                cmd.Parameters.AddWithValue("@id", accountId);
                cmd.Parameters.AddWithValue("@exclude", excludeEmployeeId ?? string.Empty);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
            finally
            {
                DBClose();
            }
        }

        public void CreateEmployeeWithAccount(EmployeeUpsertInput input)
        {
            DBOpen();
            SqlTransaction transaction = SqlCon.BeginTransaction();
            try
            {
                using (SqlCommand checkEmployee = new SqlCommand("SELECT COUNT(1) FROM NHANVIEN WITH (UPDLOCK, HOLDLOCK) WHERE MaNV = @maNV", SqlCon, transaction))
                {
                    checkEmployee.Parameters.AddWithValue("@maNV", input.EmployeeId);
                    if (Convert.ToInt32(checkEmployee.ExecuteScalar()) > 0)
                    {
                        throw new InvalidOperationException("Mã nhân viên đã tồn tại.");
                    }
                }

                using (SqlCommand checkAccount = new SqlCommand("SELECT COUNT(1) FROM TAIKHOAN WITH (UPDLOCK, HOLDLOCK) WHERE ID = @id", SqlCon, transaction))
                {
                    checkAccount.Parameters.AddWithValue("@id", input.AccountId);
                    if (Convert.ToInt32(checkAccount.ExecuteScalar()) > 0)
                    {
                        throw new InvalidOperationException("Tài khoản đăng nhập đã tồn tại.");
                    }
                }

                using (SqlCommand insertEmployee = new SqlCommand(
                    "INSERT INTO NHANVIEN (MaNV, TenNV, ChucVu, Fulltime, DiaChi, SDT, NgaySinh, NgayVaoLam) " +
                    "VALUES (@maNV, @tenNV, @chucVu, @fulltime, @diaChi, @sdt, @ngaySinh, @ngayVaoLam)",
                    SqlCon,
                    transaction))
                {
                    insertEmployee.Parameters.AddWithValue("@maNV", input.EmployeeId);
                    insertEmployee.Parameters.AddWithValue("@tenNV", input.FullName);
                    insertEmployee.Parameters.AddWithValue("@chucVu", input.Position);
                    insertEmployee.Parameters.AddWithValue("@fulltime", input.IsFulltime);
                    insertEmployee.Parameters.AddWithValue("@diaChi", input.Address);
                    insertEmployee.Parameters.AddWithValue("@sdt", input.Phone);
                    insertEmployee.Parameters.AddWithValue("@ngaySinh", input.DateOfBirth);
                    insertEmployee.Parameters.AddWithValue("@ngayVaoLam", input.DateStartWork);
                    insertEmployee.ExecuteNonQuery();
                }

                using (SqlCommand insertAccount = new SqlCommand(
                    "INSERT INTO TAIKHOAN (ID, MatKhau, Quyen, MaNV) VALUES (@id, @matKhau, @quyen, @maNV)",
                    SqlCon,
                    transaction))
                {
                    insertAccount.Parameters.AddWithValue("@id", input.AccountId);
                    insertAccount.Parameters.AddWithValue("@matKhau", input.Password);
                    insertAccount.Parameters.AddWithValue("@quyen", "nhan vien");
                    insertAccount.Parameters.AddWithValue("@maNV", input.EmployeeId);
                    insertAccount.ExecuteNonQuery();
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

        public void UpdateEmployeeWithAccount(EmployeeUpsertInput input, string originalEmployeeId, string originalAccountId)
        {
            DBOpen();
            SqlTransaction transaction = SqlCon.BeginTransaction();
            try
            {
                if (!string.Equals(input.EmployeeId, originalEmployeeId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Không được sửa mã nhân viên.");
                }

                if (!string.IsNullOrEmpty(originalAccountId)
                    && !string.Equals(input.AccountId, originalAccountId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Không được sửa tài khoản đăng nhập.");
                }

                if (string.IsNullOrEmpty(originalAccountId))
                {
                    using SqlCommand dupAccountCheck = new SqlCommand("SELECT COUNT(1) FROM TAIKHOAN WITH (UPDLOCK, HOLDLOCK) WHERE ID = @id", SqlCon, transaction);
                    dupAccountCheck.Parameters.AddWithValue("@id", input.AccountId);
                    if (Convert.ToInt32(dupAccountCheck.ExecuteScalar()) > 0)
                    {
                        throw new InvalidOperationException("Tài khoản đăng nhập đã tồn tại.");
                    }
                }

                using (SqlCommand updateEmployee = new SqlCommand(
                    "UPDATE NHANVIEN SET TenNV = @tenNV, ChucVu = @chucVu, Fulltime = @fulltime, DiaChi = @diaChi, SDT = @sdt, NgaySinh = @ngaySinh, NgayVaoLam = @ngayVaoLam " +
                    "WHERE MaNV = @maNV",
                    SqlCon,
                    transaction))
                {
                    updateEmployee.Parameters.AddWithValue("@maNV", input.EmployeeId);
                    updateEmployee.Parameters.AddWithValue("@tenNV", input.FullName);
                    updateEmployee.Parameters.AddWithValue("@chucVu", input.Position);
                    updateEmployee.Parameters.AddWithValue("@fulltime", input.IsFulltime);
                    updateEmployee.Parameters.AddWithValue("@diaChi", input.Address);
                    updateEmployee.Parameters.AddWithValue("@sdt", input.Phone);
                    updateEmployee.Parameters.AddWithValue("@ngaySinh", input.DateOfBirth);
                    updateEmployee.Parameters.AddWithValue("@ngayVaoLam", input.DateStartWork);
                    int changed = updateEmployee.ExecuteNonQuery();
                    if (changed == 0)
                    {
                        throw new InvalidOperationException("Không tìm thấy nhân viên để cập nhật.");
                    }
                }

                if (string.IsNullOrEmpty(originalAccountId))
                {
                    using SqlCommand insertAccount = new SqlCommand(
                        "INSERT INTO TAIKHOAN (ID, MatKhau, Quyen, MaNV) VALUES (@id, @matKhau, @quyen, @maNV)",
                        SqlCon,
                        transaction);
                    insertAccount.Parameters.AddWithValue("@id", input.AccountId);
                    insertAccount.Parameters.AddWithValue("@matKhau", input.Password);
                    insertAccount.Parameters.AddWithValue("@quyen", "nhan vien");
                    insertAccount.Parameters.AddWithValue("@maNV", input.EmployeeId);
                    insertAccount.ExecuteNonQuery();
                }
                else
                {
                    using SqlCommand updateAccount = new SqlCommand(
                        "UPDATE TAIKHOAN SET MatKhau = @matKhau WHERE MaNV = @maNV AND ID = @id",
                        SqlCon,
                        transaction);
                    updateAccount.Parameters.AddWithValue("@matKhau", input.Password);
                    updateAccount.Parameters.AddWithValue("@maNV", input.EmployeeId);
                    updateAccount.Parameters.AddWithValue("@id", input.AccountId);
                    updateAccount.ExecuteNonQuery();
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

        public void HardDeleteEmployeeCascade(string employeeId)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                throw new InvalidOperationException("Mã nhân viên không hợp lệ.");
            }

            DBOpen();
            SqlTransaction transaction = SqlCon.BeginTransaction();
            try
            {
                EnsureEmployeeExists(employeeId, transaction);

                TableRef employeeTable = GetEmployeeTable(transaction);
                List<ForeignKeyRelation> relations = LoadForeignKeyRelations(transaction);
                List<DeletePlan> deletePlans = BuildDeletePlans(employeeTable, relations);

                foreach (DeletePlan plan in deletePlans
                    .OrderByDescending(p => p.MaxDepth)
                    .ThenBy(p => p.TargetTable.Schema, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.TargetTable.Name, StringComparer.OrdinalIgnoreCase))
                {
                    ExecuteDeletePlan(plan, employeeId, transaction);
                }

                DeleteAccount(employeeId, transaction);
                DeleteEmployeeRow(employeeId, transaction);

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

        [Obsolete("Use HardDeleteEmployeeCascade instead.")]
        public void DeleteEmployee(string employeeId)
        {
            HardDeleteEmployeeCascade(employeeId);
        }

        private void EnsureEmployeeExists(string employeeId, SqlTransaction transaction)
        {
            using SqlCommand checkEmployee = new SqlCommand(
                "SELECT COUNT(1) FROM NHANVIEN WITH (UPDLOCK, HOLDLOCK) WHERE MaNV = @maNV",
                SqlCon,
                transaction);
            checkEmployee.Parameters.AddWithValue("@maNV", employeeId);
            if (Convert.ToInt32(checkEmployee.ExecuteScalar()) == 0)
            {
                throw new InvalidOperationException("Không tìm thấy nhân viên để xóa.");
            }
        }

        private TableRef GetEmployeeTable(SqlTransaction transaction)
        {
            const string query =
                "SELECT TOP (1) s.name AS SchemaName, t.name AS TableName " +
                "FROM sys.tables t " +
                "JOIN sys.schemas s ON t.schema_id = s.schema_id " +
                "WHERE t.name = 'NHANVIEN'";

            using SqlCommand cmd = new SqlCommand(query, SqlCon, transaction);
            using SqlDataReader reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException("Không tìm thấy bảng NHANVIEN trong cơ sở dữ liệu.");
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

        private List<DeletePlan> BuildDeletePlans(TableRef employeeTable, List<ForeignKeyRelation> relations)
        {
            Dictionary<TableRef, List<DeletePath>> planMap = new Dictionary<TableRef, List<DeletePath>>();
            Queue<DeletePath> queue = new Queue<DeletePath>();
            queue.Enqueue(new DeletePath(employeeTable, new List<ForeignKeyRelation>()));

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
                    DeletePath nextPath = new DeletePath(employeeTable, newEdges);
                    if (!nextPath.CurrentTable.Equals(employeeTable))
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

            return planMap
                .Select(kv => new DeletePlan(kv.Key, kv.Value))
                .ToList();
        }

        private void ExecuteDeletePlan(DeletePlan plan, string employeeId, SqlTransaction transaction)
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
            cmd.Parameters.AddWithValue("@maNV", employeeId);
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
            clause.Append(rootAlias).Append(".").Append(QuoteIdentifier("MaNV")).Append(" = @maNV");
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

        private void DeleteAccount(string employeeId, SqlTransaction transaction)
        {
            using SqlCommand deleteAccount = new SqlCommand("DELETE FROM TAIKHOAN WHERE MaNV = @maNV", SqlCon, transaction);
            deleteAccount.Parameters.AddWithValue("@maNV", employeeId);
            deleteAccount.ExecuteNonQuery();
        }

        private void DeleteEmployeeRow(string employeeId, SqlTransaction transaction)
        {
            using SqlCommand deleteEmployee = new SqlCommand("DELETE FROM NHANVIEN WHERE MaNV = @maNV", SqlCon, transaction);
            deleteEmployee.Parameters.AddWithValue("@maNV", employeeId);
            int deletedCount = deleteEmployee.ExecuteNonQuery();
            if (deletedCount == 0)
            {
                throw new InvalidOperationException("Không tìm thấy nhân viên để xóa.");
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
    }
}
