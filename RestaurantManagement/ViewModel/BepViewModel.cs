using QuanLyNhaHang.DataProvider;
using QuanLyNhaHang.Models;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

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
            get => _DoneSelected;
            set
            {
                _DoneSelected = value;
                OnPropertyChanged();
            }
        }

        private Bep _OrderSelected;
        public Bep OrderSelected
        {
            get => _OrderSelected;
            set
            {
                _OrderSelected = value;
                OnPropertyChanged();
            }
        }

        private string _NumberOfDishesNeedServing;
        public string NumberOfDishesNeedServing
        {
            get => _NumberOfDishesNeedServing;
            set
            {
                _NumberOfDishesNeedServing = value;
                OnPropertyChanged("PropertyB");
                Mediator.Instance.NotifyColleagues("PropertyBChanged", value);
            }
        }

        public ICommand OrderCM { get; set; }
        public ICommand DoneCM { get; set; }

        public BepViewModel()
        {
            ListDone = new ObservableCollection<Bep>();
            ListOrder = new ObservableCollection<Bep>();
            ReloadKitchenData();

            DoneCM = new RelayCommand<object>(
                (p) => DoneSelected != null,
                (p) =>
                {
                    try
                    {
                        bool success = BepDP.Flag.MarkCooked(DoneSelected.MaDM);
                        MyMessageBox msb = new MyMessageBox(success ? "Đã chế biến xong!" : "Đã xảy ra lỗi!");
                        msb.ShowDialog();
                        ReloadKitchenData();
                    }
                    catch (Exception ex)
                    {
                        MyMessageBox msb = new MyMessageBox(ex.Message);
                        msb.ShowDialog();
                    }
                });

            OrderCM = new RelayCommand<object>(
                (p) => OrderSelected != null,
                (p) =>
                {
                    try
                    {
                        bool success = BepDP.Flag.ServeDish(OrderSelected.MaDM);
                        MyMessageBox ms = new MyMessageBox(success ? "Đã phục vụ khách hàng!" : "Đã có lỗi xảy ra!");
                        ms.ShowDialog();
                        ReloadKitchenData();
                    }
                    catch (Exception ex)
                    {
                        MyMessageBox ms = new MyMessageBox(ex.Message);
                        ms.ShowDialog();
                    }
                });
        }

        private void ReloadKitchenData()
        {
            ListDone = BepDP.Flag.GetPreparingDishes();
            ListOrder = BepDP.Flag.GetDoneDishes();
            NumberOfDishesNeedServing = ListOrder.Count.ToString();
        }
    }
}
