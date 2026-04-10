using QuanLyNhaHang.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Configuration;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using ListView = System.Windows.Forms.ListView;
namespace QuanLyNhaHang.ViewModel
{

    public class BepViewModel : BaseViewModel
    {
        private ObservableCollection<Bep> _ListDone;

        public ObservableCollection<Bep> ListDone { get => _ListDone; set { _ListDone = value; OnPropertyChanged(); } }
        private ObservableCollection<Bep> _ListOrder;

        public ObservableCollection<Bep> ListOrder { get => _ListOrder; set { _ListOrder = value; OnPropertyChanged(); } }
        private Bep _DoneSelected;
        public Bep DoneSelected
        {
            get
            {
                return _DoneSelected;
            }
            set
            {
                _DoneSelected = value;
                OnPropertyChanged();

            }
        }
        private Bep _OrderSelected;
        public Bep OrderSelected
        {
            get
            {
                return _OrderSelected;
            }
            set
            {
                _OrderSelected = value;
                OnPropertyChanged();
            }
        }
        private string _NumberOfDishesNeedServing;
        public string NumberOfDishesNeedServing
        {
            get
            {
               return _NumberOfDishesNeedServing;
            }
            set
            {
                _NumberOfDishesNeedServing = value;
                OnPropertyChanged("PropertyB");
                Mediator.Instance.NotifyColleagues("PropertyBChanged", value);
            }
        }

        private string strCon = ConfigurationManager.ConnectionStrings["QuanLyNhaHang"].ConnectionString;
        private SqlConnection sqlCon = null;


        public ICommand OrderCM { get; set; }
        public ICommand DoneCM { get; set; }

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
        public BepViewModel()
        {
            ListDone = new ObservableCollection<Bep>();
            ListOrder = new ObservableCollection<Bep>();
            GetListDone();
            GetListOrder();

            DoneCM = new RelayCommand<object>((p) =>
            {

                if (DoneSelected == null) return false;
                return true;
            }, (p) =>
            {
                OpenConnect();

                SqlCommand cmd = new SqlCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "UPDATE CHEBIEN SET TrangThai = N'XONG' WHERE MaDatDon = " + DoneSelected.MaDM;
              

                cmd.Connection = sqlCon;

                int result = cmd.ExecuteNonQuery();
                if (result > 0)
                {
                    MyMessageBox msb = new MyMessageBox("Đã chế biến xong!");
                    msb.ShowDialog();
                }
                else
                {
                    MyMessageBox msb = new MyMessageBox("Đã xảy ra lỗi!");
                    msb.ShowDialog();
                }
                List<string> listTenSP = new List<string>();
                List<float> listSoLuong = new List<float>();
                cmd.CommandText = "SELECT TenNL, SoLuong FROM CHITIETMON WHERE MaMon = '" + DoneSelected.MaMon + "'";

                listTenSP.Clear();
                listSoLuong.Clear();
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    listTenSP.Add(reader.GetString(0));
                    listSoLuong.Add((float)reader.GetDouble(1));
                }
                reader.Close();
                for (int i = 0; i < listTenSP.Count(); i++)
                {
                    cmd.CommandText = "UPDATE KHO SET TonDu = TonDu - " + listSoLuong[i] * DoneSelected.SoLuong + " WHERE TenSanPham = N'" + listTenSP[i] + "'";
                    cmd.ExecuteNonQuery();
                }
                GetListDone();
                GetListOrder();
                NumberOfDishesNeedServing = ListOrder.Count.ToString();
                CloseConnect();
            });

            OrderCM = new RelayCommand<object>((p) =>
            {
                if (OrderSelected == null) return false;
                return true;
            }, (p) =>
            {
                OpenConnect();

                SqlCommand cmd = new SqlCommand();
                cmd.CommandType = CommandType.Text;

                cmd.CommandText = "DELETE FROM CHEBIEN WHERE MaDatDon = " + OrderSelected.MaDM;
              

                cmd.Connection = sqlCon;
                int result = cmd.ExecuteNonQuery();

                if (result > 0)
                {
                    MyMessageBox ms = new MyMessageBox("Đã phục vụ khách hàng!");
                    ms.ShowDialog();
                }
                else
                {
                    MyMessageBox ms = new MyMessageBox("Đã có lỗi xảy ra!");
                    ms.ShowDialog();
                }

                GetListOrder();
                GetListDone();
                NumberOfDishesNeedServing = ListOrder.Count.ToString();
                CloseConnect();
            });

        }
        private void GetListDone()
        {
            OpenConnect();

            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = "SELECT c.*, m.TenMon FROM CHEBIEN AS c JOIN MENU AS m ON c.MaMon = m.MaMon WHERE TrangThai = N'Đang chế biến'  ORDER BY NGAYCB, ThoiGianLam ";
            cmd.Connection = sqlCon;
            SqlDataReader reader = cmd.ExecuteReader();
            ListDone.Clear();
            while (reader.Read())
            {

                string maMon = reader.GetString(1);
                int soBan = reader.GetInt16(3);
                int soLuong = reader.GetInt16(2);
                string tinhTrang = reader.GetString(5);
                string tenMon = reader.GetString(6);
                string ngayCB = reader.GetDateTime(4).ToShortDateString();
                long maDM = reader.GetInt32(0);
                ListDone.Add(new Bep(maDM, maMon, soLuong, soBan, ngayCB, "Đang chế biến", tenMon));
                //CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(ListDone);
                //view.SortDescriptions.Add(new SortDescription("SELECT * FROM CHEBIEN ORDER BY NgayCB", ListSortDirection.Ascending));


            }
            reader.Close();

            CloseConnect();
        }
        private void GetListOrder()
        {
            OpenConnect();

            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = "SELECT c.*, m.TenMon FROM CHEBIEN AS c JOIN MENU AS m ON c.MaMon = m.MaMon WHERE TrangThai = N'XONG'";
           
            cmd.Connection = sqlCon;
            SqlDataReader reader = cmd.ExecuteReader();
            ListOrder.Clear();
            while (reader.Read())
            {

                string maMon = reader.GetString(1);
                int soBan = reader.GetInt16(3);
                int soLuong = reader.GetInt16(2);
                string tinhTrang = reader.GetString(5);
                string tenMon = reader.GetString(6);
                string ngayCB = reader.GetDateTime(4).ToShortDateString();
                long maDM = reader.GetInt32(0);
                ListOrder.Add(new Bep(maDM, maMon, soLuong, soBan, ngayCB, "XONG", tenMon));
                //CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(ListDone);
            }
            reader.Close();
            NumberOfDishesNeedServing = ListOrder.Count.ToString();
            CloseConnect();
        }
    }
}
