using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Data.SqlClient;
using System.Data;
using System.Collections.ObjectModel;
using QuanLyNhaHang.Models;
using System.Windows.Forms;
using System.IO;
using System.Configuration;
using iTextSharp.text.pdf;
using iTextSharp.text;
using Document = iTextSharp.text.Document;

namespace QuanLyNhaHang.ViewModel
{
    public class KhoViewModel : BaseViewModel
    {
        private ObservableCollection<Kho> _ListWareHouse;
        public ObservableCollection<Kho> ListWareHouse { get => _ListWareHouse; set { _ListWareHouse = value; OnPropertyChanged(); } }
        private Kho _Selected;
        public Kho Selected 
        {
            get => _Selected; 
            set
            {
                _Selected = value;
                OnPropertyChanged();
                if (Selected != null)
                {
                    GetInputInfo(Selected.TenSanPham);
                } 
                OnPropertyChanged();
            }
        }
        private ObservableCollection<NhapKho> _ListIn;
        public ObservableCollection<NhapKho> ListIn { get => _ListIn; set { _ListIn = value; OnPropertyChanged(); } }
        private string _TimeSelected;
        public string TimeSelected 
        { 
            get => _TimeSelected; 
            set 
            {
                _TimeSelected = value;
                OnPropertyChanged();
                if (!String.IsNullOrEmpty(TimeSelected))
                {
                    ID = ListIn[TimeIndex].MaNhap;
                    Name = ListIn[TimeIndex].TenSP;
                    Count = ListIn[TimeIndex].SoLuong;
                    Unit = ListIn[TimeIndex].DonVi;
                    Value = ListIn[TimeIndex].DonGia;
                    DateIn = ListIn[TimeIndex].NgayNhap;
                    Suplier = ListIn[TimeIndex].NguonNhap;
                    SuplierInfo = ListIn[TimeIndex].LienLac;

                    IDBeforeEdit = ID;
                    NameBeforeEdit = Name;
                }
            } 
        }
        private int _TimeIndex;
        public int TimeIndex { get => _TimeIndex; set { _TimeIndex = value; OnPropertyChanged(); } }
        private ObservableCollection<string> _ListTime;
        public ObservableCollection<string> ListTime { get => _ListTime; set { _ListTime = value; OnPropertyChanged(); } }


        #region // Right Card
        private string IDBeforeEdit;
        private string _ID;
        public string ID { get => _ID; set { _ID = value; OnPropertyChanged(); } }
        private string NameBeforeEdit;
        private string _Name;
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Count;
        public string Count { get => _Count; set { _Count = value; OnPropertyChanged(); } }
        private string _Unit;
        public string Unit { get => _Unit; set { _Unit = value; OnPropertyChanged(); } }
        private string _Value;
        public string Value { get => _Value; set { _Value = value; OnPropertyChanged(); } }
        private string _DateIn;
        public string DateIn { get => _DateIn; set { _DateIn = value; OnPropertyChanged("DateIn"); } }
        private string _Suplier;
        public string Suplier { get => _Suplier; set { _Suplier = value; OnPropertyChanged(); } }
        private string _SuplierInfo;
        public string SuplierInfo { get => _SuplierInfo; set { _SuplierInfo = value; OnPropertyChanged(); } }
        #endregion

        #region // Search bar
        private string _Search;
        public string Search 
        { 
            get => _Search; 
            set 
            { 
                _Search = value;
                string strQuery;
                OnPropertyChanged();
                if (!String.IsNullOrEmpty(Search))
                {
                    strQuery = "SELECT * FROM KHO WHERE Xoa = 0 AND TenSanPham LIKE N'%" + Search + "%'";
                }
                else
                    strQuery = "SELECT * FROM KHO WHERE Xoa = 0";
                ListViewDisplay(strQuery);
            } 
        }
        #endregion


        public ICommand AddCM { get; set; }
        public ICommand EditCM { get; set; }
        public ICommand DeleteCM { get; set; }
        public ICommand CheckCM { get; set; }


        private string strCon = ConfigurationManager.ConnectionStrings["QuanLyNhaHang"].ConnectionString;
        private SqlConnection sqlCon = null;


        public KhoViewModel()
        {
            OpenConnect();

            ListWareHouse = new ObservableCollection<Kho>();
            ListIn = new ObservableCollection<NhapKho>();
            ListTime = new ObservableCollection<string>();
            DateIn = DateTime.Now.ToShortDateString();

            ListViewDisplay("SELECT * FROM KHO WHERE Xoa = 0");


            #region //add command
            AddCM = new RelayCommand<object>((p) =>
            {
                if (string.IsNullOrEmpty(Name) || string.IsNullOrEmpty(Count) || string.IsNullOrEmpty(DateIn.ToString()) || string.IsNullOrEmpty(Unit) || string.IsNullOrEmpty(Value))
                    return false;
                OnPropertyChanged("ID");

                OpenConnect();

                SqlCommand cmd = new SqlCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "SELECT MaNhap FROM CHITIETNHAP";
                cmd.Connection = sqlCon;
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (ID == reader.GetString(0)) return false;
                }
                reader.Close();

                CloseConnect();

                if (Count == "0") return false;
                if (!isMoney(Value)) return false;
                if (SuplierInfo != null && !isNumber(SuplierInfo)) return false;
                return true;
            }, (p) =>
            {
                OpenConnect();

                SqlCommand cmd = new SqlCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "SELECT * FROM KHO WHERE TenSanPham = N'" + Name + "'";
                cmd.Connection = sqlCon;

                SqlDataReader reader = cmd.ExecuteReader();
                if (!reader.Read())
                {
                    reader.Close();
                    cmd.CommandText = "INSERT INTO KHO VALUES(N'" + Name + "', " + 0 + ", N'" + Unit + "', " + Value + ", 0)";
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    int xoa = reader.GetInt16(4);
                    if (xoa > 0)
                    {
                        reader.Close();
                        cmd.CommandText = "UPDATE KHO SET TonDu = 0, Xoa = 0 WHERE TenSanPham = N'" + Name + "'";
                        cmd.ExecuteNonQuery();
                    }    
                    else
                        reader.Close();
                }    
                CloseConnect();

                OpenConnect();

                SqlCommand sqlCmd = new SqlCommand();
                sqlCmd.CommandType = CommandType.Text;
                sqlCmd.CommandText = "INSERT INTO CHITIETNHAP VALUES ('" + ID + "',N'" + Name + "',N'" + Unit + "'," + Value + "," + Count + ",'" + DateIn + "',N'" + Suplier + "','" + SuplierInfo + "')";
                sqlCmd.Connection = sqlCon;

                int result = sqlCmd.ExecuteNonQuery();
                if (result > 0)
                {
                    MyMessageBox mess = new MyMessageBox("Nhập thành công!");
                    mess.ShowDialog();
                    GetInputInfo(Name);
                }
                else
                {
                    MyMessageBox mess = new MyMessageBox("Nhập không thành công!");
                    mess.ShowDialog();
                }


                ListViewDisplay("SELECT * FROM KHO WHERE Xoa = 0");


                CloseConnect();
            });
            #endregion


            #region // edit command
            EditCM = new RelayCommand<object>((p) =>
            {
                foreach (NhapKho item in ListIn)
                {
                    if (ID == item.MaNhap && Name == item.TenSP && Count == item.SoLuong && DateIn == item.NgayNhap && Value == item.DonGia && Unit == item.DonVi && Suplier == item.NguonNhap && SuplierInfo == item.LienLac)
                        return false;
                }
                if (string.IsNullOrEmpty(ID) || string.IsNullOrEmpty(Name) || string.IsNullOrEmpty(Count) || string.IsNullOrEmpty(DateIn.ToString()) || string.IsNullOrEmpty(Unit) || string.IsNullOrEmpty(Value))
                    return false;
                if (Count == "0") return false;
                if (!isMoney(Value)) return false;
                if (SuplierInfo != null && !isNumber(SuplierInfo)) return false;
                foreach(NhapKho item in ListIn)
                {
                    if (ID == item.MaNhap) return true;
                }
                return false;
            }, (p) =>
            {
                OpenConnect();


                if (Name != NameBeforeEdit)
                {
                    MyMessageBox mess = new MyMessageBox("Không được sửa Tên sản phẩm!");
                    Name = NameBeforeEdit;
                    mess.ShowDialog();
                }
                else
                if (ID != IDBeforeEdit)
                {
                    MyMessageBox mess = new MyMessageBox("Không được sửa Mã nhập!");
                    ID = IDBeforeEdit;
                    mess.ShowDialog();
                }
                else
                {
                    SqlCommand cmd = new SqlCommand();
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = "UPDATE CHITIETNHAP SET TenSanPham = N'" + Name + "', DonVi = N'" + Unit + "', DonGia = " + Value + ", SoLuong = " + Count + ", NgayNhap = '" + DateIn + "', NguonNhap = N'" + Suplier + "', LienLac = '" + SuplierInfo + "' WHERE MaNhap = '" + ID + "'";
                    cmd.Connection = sqlCon;

                    int result = cmd.ExecuteNonQuery();

                    if (result > 0)
                    {
                        MyMessageBox mess = new MyMessageBox("Sửa thành công!");
                        mess.ShowDialog();
                        GetInputInfo(Name);
                    }
                    else
                    {
                        MyMessageBox mess = new MyMessageBox("Sửa không thành công!");
                        mess.ShowDialog();
                    }
                    ListViewDisplay("SELECT * FROM KHO WHERE Xoa = 0");
                }

                CloseConnect();
            });
            #endregion


            #region // delete command
            DeleteCM = new RelayCommand<object>((p) =>
            {
                if (Selected == null) return false;
                return true;
            }, (p) =>
            {
                bool delete = false;

                if (Selected.TonDu > 0)
                {
                    MyMessageBox yn = new MyMessageBox("Sản phẩm này đang còn trong kho!\n   Bạn có chắc chắn xóa?", true);
                    yn.ShowDialog();
                    if (yn.ACCEPT())
                    {
                        delete = true;
                    }
                }
                else
                    delete = true;

                if (delete)
                {
                    OpenConnect();

                    SqlCommand cmd = new SqlCommand();
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = "UPDATE KHO SET Xoa = 1 WHERE TenSanPham = N'" + Selected.TenSanPham + "'";
                    cmd.Connection = sqlCon;

                    int result = cmd.ExecuteNonQuery();
                    if (result > 0)
                    {
                        MyMessageBox mess = new MyMessageBox("Xóa thành công!");
                        mess.ShowDialog();
                        RefreshRightCard();
                    }
                    else
                    {
                        MyMessageBox mess = new MyMessageBox("Xóa không thành công!");
                        mess.ShowDialog();
                    }
                    ListViewDisplay("SELECT * FROM KHO WHERE Xoa = 0");

                    CloseConnect();
                }    
            });
            #endregion


            #region // check command
            CheckCM = new RelayCommand<object>((p) =>
            {
                if (ListWareHouse == null) return false;
                return true;
            }, (p) =>
            {
                OpenConnect();

                string strQuery = "SELECT * FROM KHO WHERE (DonVi = N'Kg' AND TonDu <= 1) OR (DonVi != N'Kg' AND TonDu <= 5)";

                SqlCommand cmd = new SqlCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = strQuery;
                cmd.Connection = sqlCon;

                SqlDataReader reader = cmd.ExecuteReader();
                List<string> ten = new List<string>();
                List<string> soluong = new List<string>();
                List<string> donvi = new List<string>();

                while (reader.Read())
                {
                    ten.Add(reader.GetString(0));
                    soluong.Add(reader.GetDouble(1).ToString());
                    donvi.Add(reader.GetString(2));
                }

                if (ten.Count > 0)
                { 
                    ListViewDisplay(strQuery);
                    MyMessageBox yesno = new MyMessageBox("Bạn có muốn in danh sách?", true);
                    yesno.ShowDialog();
                    if (yesno.ACCEPT())
                    {
                        SaveFileDialog sfd = new SaveFileDialog();
                        sfd.Filter = "PDF (*.pdf)|*.pdf";
                        sfd.FileName = "Danh sách cần nhập " + DateTime.Now.Day + "-" + DateTime.Now.Month + "-" + DateTime.Now.Year;
                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            if (File.Exists(sfd.FileName))
                            {
                                try
                                {
                                    File.Delete(sfd.FileName);
                                }
                                catch (IOException ex)
                                {
                                    MyMessageBox msb = new MyMessageBox("Đã có lỗi xảy ra!");
                                    msb.ShowDialog();
                                }
                            }
                            try
                            {
                                PdfPTable pdfTable = new PdfPTable(3);
                                pdfTable.DefaultCell.Padding = 3;
                                pdfTable.WidthPercentage = 100;
                                pdfTable.HorizontalAlignment = Element.ALIGN_LEFT;

                                BaseFont bf = BaseFont.CreateFont(Environment.GetEnvironmentVariable("windir") + @"\fonts\TIMES.TTF", BaseFont.IDENTITY_H, true);
                                Font f = new Font(bf, 16, Font.NORMAL);

                                PdfPCell cell = new PdfPCell(new Phrase("Tên sản phẩm",f));
                                pdfTable.AddCell(cell);
                                cell = new PdfPCell(new Phrase("Tồn dư",f));
                                pdfTable.AddCell(cell);
                                cell = new PdfPCell(new Phrase("Đơn vị",f));
                                pdfTable.AddCell(cell);
                                for (int i = 0; i < ten.Count; i++)
                                {
                                    pdfTable.AddCell(new Phrase(ten[i],f));
                                    pdfTable.AddCell(new Phrase(soluong[i], f));
                                    pdfTable.AddCell(new Phrase(donvi[i], f));
                                }    

                                using (FileStream stream = new FileStream(sfd.FileName, FileMode.Create))
                                {
                                    Document pdfDoc = new Document(PageSize.A4, 50f, 50f, 40f, 40f);
                                    PdfWriter.GetInstance(pdfDoc, stream);
                                    pdfDoc.Open();
                                    pdfDoc.Add(new Paragraph("              DANH SÁCH SẢN PHẨM CẦN NHẬP THÊM " + DateTime.Now.ToShortDateString(),f));
                                    pdfDoc.Add(new Paragraph("    "));
                                    pdfDoc.Add(pdfTable);
                                    pdfDoc.Close();
                                    stream.Close();
                                }

                                MyMessageBox mess = new MyMessageBox("In thành công!");
                                mess.ShowDialog();
                            }
                            catch (Exception ex)
                            {
                                MyMessageBox msb = new MyMessageBox("Đã có lỗi xảy ra!");
                                msb.ShowDialog();
                            }
                        }
                        ListViewDisplay("SELECT * FROM KHO");
                    }
                    else
                        ListViewDisplay("SELECT * FROM KHO");
                }
                else
                {
                    MyMessageBox mess = new MyMessageBox("Chưa có sản phẩm nào \n      cần nhập thêm!");
                    mess.ShowDialog();
                }

                CloseConnect();
            });
            #endregion

            CloseConnect();
        }

        private void OpenConnect()
        {
            sqlCon = new SqlConnection(strCon);
            if (sqlCon.State == ConnectionState.Closed)
            {
                sqlCon.Open();
            }    
        }

        private void CloseConnect()
        {
            if (sqlCon.State == ConnectionState.Open)
            {
                sqlCon.Close();
            }
        }

        private void ListViewDisplay(string strQuery)
        {
            OpenConnect();

            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = strQuery;
            cmd.Connection = sqlCon;
            SqlDataReader reader = cmd.ExecuteReader();
            ListWareHouse.Clear();
            while (reader.Read())
            {
                string ten = reader.GetString(0);
                float tondu = (float)reader.GetDouble(1);
                string donvi = reader.GetString(2);
                string dongia = reader.GetSqlMoney(3).ToString();
                ListWareHouse.Add(new Kho(ten, tondu, donvi, dongia));
            }

            CloseConnect();
        }

        private void GetInputInfo(string tensanpham)
        {
            OpenConnect();

            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = "SELECT TOP 10 * FROM CHITIETNHAP WHERE TenSanPham = N'" + tensanpham + "' ORDER BY NgayNhap DESC";
            cmd.Connection = sqlCon;
            SqlDataReader reader = cmd.ExecuteReader();
            ListIn.Clear();
            ListTime.Clear();
            while (reader.Read())
            {
                string ma = reader.GetString(0);
                string ten = reader.GetString(1);
                string donvi = reader.GetString(2);
                string dongia = reader.GetSqlMoney(3).ToString();
                string soluong = reader.GetDouble(4).ToString();
                string date = reader.GetDateTime(5).ToShortDateString();
                string nguon = reader.GetString(6);
                string lienlac = reader.GetString(7);
                ListIn.Add(new NhapKho(ma, ten, donvi, dongia, soluong, date, nguon, lienlac));
                ListTime.Add(date);
            }
            if (ListTime.Count > 0)
                TimeSelected = ListTime[0].ToString();
            reader.Close();

            CloseConnect();
        }

        private void RefreshRightCard()
        {
            ID = "";
            Name = "";
            Count = "";
            Unit = "";
            Value = "";
            DateIn = "";
            Suplier = "";
            SuplierInfo = "";

            TimeSelected = "";
        }

        private bool isNumber(string s)
        {
            if (s == null) return false;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] < 48 || s[i] > 57) return false;
            }
            return true;
        }
        private bool isMoney(string s)
        {
            int count = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if ((s[i] < 48 || s[i] > 57) && s[i] != '.')
                    return false;
                if (s[i] == '.') count++;
            }
            if (s[0] == '.') return false;
            if (s[s.Length - 1] == '.') return false;
            if (s[0] == '0') return false;
            if (count > 1) return false;
            return true;
        }
    }
}
