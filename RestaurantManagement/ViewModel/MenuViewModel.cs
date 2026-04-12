using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuanLyNhaHang.Command;
using System.Windows.Input;
using Diacritics.Extensions;
using QuanLyNhaHang.Models;
using QuanLyNhaHang.View;
using QuanLyNhaHang.DataProvider;
using QuanLyNhaHang.ViewModel;
using QuanLyNhaHang.State.Navigator;
using QuanLyNhaHang.Utils;
using System.Windows.Data;
using System.ComponentModel;
using TinhTrangBan.Models;
using System.Windows;
using RestaurantManagement.ViewModel;

namespace QuanLyNhaHang.ViewModel
{
    public class MenuViewModel : BaseViewModel
    {
        public MenuViewModel()
        {
            //LoadMenu
            LoadMenu();
            Tables = MenuDP.Flag.GetTables();
            Kho = MenuDP.Flag.GetIngredients();
            _menuItemsView = new CollectionViewSource();
            _menuItemsView.Source = MenuItems;
            _menuItemsView.Filter += MenuItems_Filter;
            _menuItemsView.SortDescriptions.Add(new SortDescription("FoodName", ListSortDirection.Ascending));
            //Command actions
            OrderFeature_Command = new RelayCommand<MenuItem>((p) => true, (p) => OrderAnItem(p.ID));
            RemoveItemFeature_Command = new RelayCommand<SelectedMenuItem>((p) => p != null && !p.IsLockedByChef, (p) => RemoveAnItem(p));
            ClearAllSelectedDishes = new RelayCommand<object>((p) =>
            {
                if (IsEditOrderMode || SelectedItems.Count == 0) return false;
                return true;
            }, (p) => {
                MyMessageBox msb = new MyMessageBox("Bạn có muốn xoá tất cả những món đã chọn?", true);
                msb.ShowDialog();
                if (msb.ACCEPT() == false)
                {
                    return;
                }
                SelectedItems.Clear();
                DecSubtotal = 0;
                StrSubtotal = MoneyFormatter.FormatVnd(0);
            });
            SortingFeature_Command = new RelayCommand<object>((p) => true, (p) => {
                SortMenuItems();
            });
            Inform_Chef_Of_OrderedDishes = new RelayCommand<object>((p) =>
            {
                if (SelectedItems.Count == 0) return false;
                return true;
            }, (p) =>
            {
                if (IsEditOrderMode)
                {
                    SaveCurrentOrderChanges();
                    return;
                }

                string mess = "";
                try
                {
                    if (SelectedTable != null)
                    {
                        MenuDP.IngredientAvailabilityResult availability = MenuDP.Flag.CheckIngredientAvailability(SelectedItems);
                        if (SelectedTable.Status == 0)
                        {
                            MyMessageBox typeOfCustomerAnnouncement = new MyMessageBox("Bạn muốn order cho khách mới?", true);
                            typeOfCustomerAnnouncement.ShowDialog();
                            if (typeOfCustomerAnnouncement.ACCEPT() == true)
                            {
                                mess = "Bàn hiện đang được sử dụng! Hãy chọn bàn khác";
                                return;
                            }
                        }
                        if (availability.HasMissingRecipe)
                        {
                            mess = "Hãy thêm thông tin nguyên liệu cho món!";
                            return;
                        }

                        if (availability.HasInsufficientIngredient)
                        {
                            string tennl = string.Join(" , ", availability.InsufficientIngredients);
                            mess = $"Không đủ nguyên liệu ({tennl}). Hãy nhập thêm!";
                            return;
                        }

                        MaNV = getMaNV();
                        int soBan = Convert.ToInt32(SelectedTable.NumOfTable);
                        MenuDP.Flag.CreateOpenOrderTransactional(soBan, MaNV, SelectedItems);

                        mess = "Đã báo chế biến thành công!";
                        SelectedItems.Clear();
                        DecSubtotal = 0;
                        StrSubtotal = MoneyFormatter.FormatVnd(0);
                        Tables = MenuDP.Flag.GetTables();
                        Kho = MenuDP.Flag.GetIngredients();
                    }
                    else if (SelectedTable == null)
                    {
                        mess = "Bạn chưa chọn bàn";
                    }
                }
                catch (Exception ex)
                {
                    MyMessageBox msb = new MyMessageBox(ex.Message);
                    msb.Show();
                }
                finally
                {
                    if (!string.IsNullOrWhiteSpace(mess))
                    {
                        MyMessageBox ms = new MyMessageBox(mess);
                        ms.Show();
                    }
                }
            });
            LoadOpenOrderForSelectedTable_Command = new RelayCommand<object>((p) => SelectedTable != null, (p) => LoadOpenOrderForSelectedTable());
            CancelOrderEditing_Command = new RelayCommand<object>((p) => IsEditOrderMode, (p) => CancelOrderEditing());
            _selectedItems = new ObservableCollection<SelectedMenuItem>();
            _comboBox_2Items = new ObservableCollection<string>();
            LoadCombobox_2Items();
            IsEditOrderMode = false;
        }
          
        #region attributes
        private ObservableCollection<MenuItem> _menuItems = new ObservableCollection<MenuItem>();
        private ObservableCollection<SelectedMenuItem> _selectedItems = new ObservableCollection<SelectedMenuItem>();
        private ObservableCollection<Table> _tables = new ObservableCollection<Table>();
        private ObservableCollection<ChiTietMon> _ingredients = new ObservableCollection<ChiTietMon>();
        private ObservableCollection<Models.Kho> _kho = new ObservableCollection<Models.Kho>();
        private Table _selectedTable = new Table();
        private ObservableCollection<string> _comboBox_2Items = new ObservableCollection<string>();
        private CollectionViewSource _menuItemsView = new CollectionViewSource();
        private string myComboboxSelection = "A -> Z";
        private decimal dec_subtotal = 0;
        private string str_subtotal = MoneyFormatter.FormatVnd(0);
        private string _searchText = string.Empty;
        private string MaNV = string.Empty;
        private int _currentOpenOrderId = 0;
        private bool _isEditOrderMode = false;
        private bool _isLoadingOrder = false;
        #endregion

        #region properties
        public ObservableCollection<MenuItem> MenuItems { get { return _menuItems; } set { _menuItems = value; OnPropertyChanged(); } }
        public ObservableCollection<SelectedMenuItem> SelectedItems { get { return _selectedItems; } set { _selectedItems = value; OnPropertyChanged(); } }
        public ObservableCollection<Table> Tables { get { return _tables; } set { _tables = value;  OnPropertyChanged(); } }
        public ObservableCollection<ChiTietMon> Ingredients { get { return _ingredients; } set { _ingredients = value; OnPropertyChanged(); } }
        public ObservableCollection<Models.Kho> Kho { get { return _kho; } set { _kho = value; OnPropertyChanged(); } }
        public ObservableCollection<string> ComboBox_2Items { get { return _comboBox_2Items; } set { _comboBox_2Items = value; } }
        public string MyComboboxSelection { get { return myComboboxSelection; } set { myComboboxSelection = value; OnPropertyChanged(); }}
        public Table SelectedTable
        {
            get { return _selectedTable; }
            set
            {
                _selectedTable = value;
                OnPropertyChanged();
                if (!_isLoadingOrder)
                {
                    LoadOpenOrderForSelectedTable();
                }
            }
        }
        public ICollectionView MenuItemCollection
        {
            get
            {
                return this._menuItemsView.View;
            }
        }
        public string Day
        {
            get
            {
                return DateTime.Today.DayOfWeek.ToString() + ", " + DateTime.Now.ToString("dd/MM/yyyy");
            }
        }
        public Decimal DecSubtotal
        {
            set
            {
                dec_subtotal = value;
                OnPropertyChanged();
            }
            get { return dec_subtotal; }
        }

        public string StrSubtotal
        {
            set
            {
                str_subtotal = value;
                OnPropertyChanged();
            }
            get { return str_subtotal; }
        }

        public string SearchText { get { return _searchText; } set { _searchText = value; this._menuItemsView.View.Refresh(); OnPropertyChanged(); } }

        public bool IsEditOrderMode
        {
            get => _isEditOrderMode;
            set
            {
                _isEditOrderMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNewOrderMode));
                OnPropertyChanged(nameof(CurrentOrderModeKey));
                OnPropertyChanged(nameof(CurrentOrderModeDescription));
                OnPropertyChanged(nameof(PrimaryOrderActionText));
            }
        }

        public bool IsNewOrderMode => !IsEditOrderMode;
        public string CurrentOrderModeKey => IsEditOrderMode ? "EDIT_ORDER_MODE" : "NEW_ORDER_MODE";
        public string CurrentOrderModeDescription => IsEditOrderMode
            ? "Đang sửa order mở của bàn đã chọn. Món đã báo bếp chỉ có thể gọi thêm."
            : "Đang tạo order mới.";

        public string PrimaryOrderActionText => IsEditOrderMode ? "LƯU CHỈNH SỬA" : "BÁO CHẾ BIẾN";
        #endregion

        #region commands
        public ICommand OrderFeature_Command { get; set; }
        public ICommand RemoveItemFeature_Command { get; set; }
        public ICommand SortingFeature_Command { get; set; }
        public ICommand ClearAllSelectedDishes { get; set; }
        public ICommand Inform_Chef_Of_OrderedDishes { get; set; }
        public ICommand SwitchCustomerTable { get; set; } = null!;
        public ICommand LoadOpenOrderForSelectedTable_Command { get; set; }
        public ICommand CancelOrderEditing_Command { get; set; }
        #endregion

        #region methods
        private void LoadCombobox_2Items()
        {
            _comboBox_2Items.Add("Giá cao -> thấp");
            _comboBox_2Items.Add("Giá thấp -> cao");
            _comboBox_2Items.Add("A -> Z");
            _comboBox_2Items.Add("Z -> A");

            ComboBox_2Items = _comboBox_2Items;
        }

        private void OrderAnItem(string ID)
        {
            foreach (MenuItem item in _menuItems)
            {
                if (item.ID == ID)
                {
                    SelectedMenuItem? x = checkIfAnItemIsInOrderItems(ID);
                    if (x != null)
                    {
                        x.Quantity++;
                        DecSubtotal += item.Price;
                        StrSubtotal = MoneyFormatter.FormatVnd(DecSubtotal);
                        return;
                    }

                    SelectedMenuItem s_item = new SelectedMenuItem(item.ID, item.FoodName, item.Price, 1, false);
                    SelectedItems.Add(s_item);
                    DecSubtotal += item.Price;
                    StrSubtotal = MoneyFormatter.FormatVnd(DecSubtotal);
                    break;
                }
            }
        }
        private void RemoveAnItem(SelectedMenuItem x)
        {
            if (x.IsLockedByChef)
            {
                MyMessageBox mess = new MyMessageBox("Món đã báo bếp chỉ có thể gọi thêm.");
                mess.Show();
                return;
            }

            DecSubtotal -= x.Price;
            StrSubtotal = MoneyFormatter.FormatVnd(DecSubtotal);
            if (x.Quantity > 1)
            {
                x.Quantity--;
            }
            else
            {
                SelectedItems.Remove(x);
            }
        }

        public void SortMenuItems()
        {
            _menuItemsView.SortDescriptions.Clear();

            if (MyComboboxSelection == "Giá cao -> thấp")
            {
                _menuItemsView.SortDescriptions.Add(new SortDescription("Price", ListSortDirection.Descending));
            }
            else if (MyComboboxSelection == "Giá thấp -> cao")
            {
                _menuItemsView.SortDescriptions.Add(new SortDescription("Price", ListSortDirection.Ascending));
            }
            else if (MyComboboxSelection == "A -> Z")
            {
                _menuItemsView.SortDescriptions.Add(new SortDescription("FoodName", ListSortDirection.Ascending));
            }
            else if (MyComboboxSelection == "Z -> A")
            {
                _menuItemsView.SortDescriptions.Add(new SortDescription("FoodName", ListSortDirection.Descending));
            }
        }

        private void LoadOpenOrderForSelectedTable()
        {
            if (SelectedTable == null)
            {
                return;
            }

            if (SelectedTable.Status != 0)
            {
                _currentOpenOrderId = 0;
                IsEditOrderMode = false;
                SelectedItems = new ObservableCollection<SelectedMenuItem>();
                RefreshSubtotal();
                return;
            }

            try
            {
                int tableId = Convert.ToInt32(SelectedTable.NumOfTable);
                int openOrderId = MenuDP.Flag.GetOpenOrderByTable(tableId);
                if (openOrderId <= 0)
                {
                    _currentOpenOrderId = 0;
                    IsEditOrderMode = false;
                    SelectedItems = new ObservableCollection<SelectedMenuItem>();
                    RefreshSubtotal();
                    return;
                }

                ObservableCollection<SelectedMenuItem> openItems = MenuDP.Flag.GetOpenOrderItems(openOrderId);
                _isLoadingOrder = true;
                SelectedItems = openItems;
                _isLoadingOrder = false;
                _currentOpenOrderId = openOrderId;
                IsEditOrderMode = true;
                RefreshSubtotal();
            }
            catch (Exception ex)
            {
                _isLoadingOrder = false;
                MyMessageBox mess = new MyMessageBox(ex.Message);
                mess.Show();
            }
        }

        private void SaveCurrentOrderChanges()
        {
            if (!IsEditOrderMode || SelectedTable == null || _currentOpenOrderId <= 0)
            {
                MyMessageBox mess = new MyMessageBox("Không tìm thấy order mở để cập nhật.");
                mess.Show();
                return;
            }

            try
            {
                int tableId = Convert.ToInt32(SelectedTable.NumOfTable);
                MenuDP.Flag.UpdateOpenOrder(_currentOpenOrderId, tableId, SelectedItems);
                MyMessageBox mess = new MyMessageBox("Đã lưu chỉnh sửa order thành công!");
                mess.Show();
                LoadOpenOrderForSelectedTable();
            }
            catch (InvalidOperationException ex)
            {
                MyMessageBox mess = new MyMessageBox(ex.Message);
                mess.Show();
            }
            catch (SqlException)
            {
                MyMessageBox mess = new MyMessageBox("Lỗi cơ sở dữ liệu khi lưu chỉnh sửa order.");
                mess.Show();
            }
        }

        private void CancelOrderEditing()
        {
            if (!IsEditOrderMode)
            {
                return;
            }

            LoadOpenOrderForSelectedTable();
        }
        #endregion

        #region complementary methods
        private SelectedMenuItem? checkIfAnItemIsInOrderItems(string ID)
        {
            foreach (SelectedMenuItem item in _selectedItems)
            {
                if (item.ID == ID)
                {
                    return item;
                }
            }
            return null;
        }
        public void MenuItems_Filter(object sender, FilterEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchText))
            {
                e.Accepted = true;
                return;
            }

            if (e.Item is not Models.MenuItem item)
            {
                e.Accepted = false;
                return;
            }

            if (item.FoodName.RemoveDiacritics().ToLower().Contains(SearchText.RemoveDiacritics().ToLower()))
            {
                e.Accepted = true;
            }
            else
            {
                e.Accepted = false;
            }
        }

        private async Task LoadMenu()
        {
            _menuItems = await MenuDP.Flag.ConvertToCollection();
        }

        private string getMaNV()
        {
            return LoginWindowVM.MaNV;
        }

        private void RefreshSubtotal()
        {
            decimal subtotal = 0;
            foreach (SelectedMenuItem item in SelectedItems)
            {
                subtotal += item.Price * item.Quantity;
            }

            DecSubtotal = subtotal;
            StrSubtotal = MoneyFormatter.FormatVnd(DecSubtotal);
        }

        #endregion
    }
}
