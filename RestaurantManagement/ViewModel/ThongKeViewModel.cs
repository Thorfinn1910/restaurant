using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using LiveCharts;
using LiveCharts.Wpf;
using RestaurantManagement.Models;
using System.Data.SqlClient;
using System.Configuration;
using System.Data;
using QuanLyNhaHang.DataProvider;

namespace QuanLyNhaHang.ViewModel
{
    public class ThongKeViewModel : BaseViewModel
    {
        string connectstring = ConfigurationManager.ConnectionStrings["QuanLyNhaHang"].ConnectionString;
        public ThongKeViewModel()
        {
            SeriesCollection = new SeriesCollection();
            Formatter = value => value.ToString("G");
            DayMonthCheckingCommand = new RelayCommand<string>((p) => true, (p) => DayMonthCheck());
            MonthYearCheckingCommand = new RelayCommand<string>((p) => true, (p) => MonthYearCheck());
            LoadMonth();
            LoadYear();
        }
        #region Attributes
        private SeriesCollection seriesCollection;
        private ObservableCollection<string> months = new ObservableCollection<string>();
        private ObservableCollection<string> years = new ObservableCollection<string>();

        private string selectedMonth = DateTime.Now.Month.ToString();
        private string selectedYear = DateTime.Now.Year.ToString();
        private string[] labels;
        private double dec_sumofprofit = 0;
        private string sumofprofit = "0 VND";
        private double dec_sumofpaid = 0;
        private string sumofpaid = "0 VND";
        private string visibility = "hidden";

        #endregion
        #region Properties
        public SeriesCollection SeriesCollection
        {
            get { return seriesCollection; }
            set { seriesCollection = value; }
        }
        public ObservableCollection<string> Months
        {
            get { return months; }
            set
            {
                months = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<string> Years
        {
            get { return years; }
            set
            {
                years = value;
                OnPropertyChanged();
            }
        }
        public string SelectedMonth
        {
            get { return selectedMonth; }
            set
            {
                if (value != selectedMonth)
                {
                    selectedMonth = value;
                    OnPropertyChanged();
                }
            }
        }
        public string SelectedYear
        {
            get { return selectedYear; }
            set
            {
                if (value != selectedYear)
                {
                    selectedYear = value;
                    OnPropertyChanged();
                }
            }
        }
        public string[] Labels
        {
            get { return labels; }
            set { labels = value; OnPropertyChanged(); }
        }
        public Func<double, string> Formatter { get; set; }
        public string SumofProfit
        {
            get { return sumofprofit; }
            set { sumofprofit = value; OnPropertyChanged(); }
        }
        public double DecSumofProfit
        {
            get { return dec_sumofprofit; }
            set { dec_sumofprofit = value; OnPropertyChanged(); }
        }
        public string SumofPaid
        {
            get { return sumofpaid; }
            set { sumofpaid = value; OnPropertyChanged(); }
        }
        public double DecSumofPaid
        {
            get { return dec_sumofpaid; }
            set { dec_sumofpaid = value; OnPropertyChanged(); }
        }
        public string Visibility
        {
            get { return visibility; }
            set { visibility = value; OnPropertyChanged(); }
        }
        #endregion
        #region Commands
        public ICommand DayMonthCheckingCommand { get; set; }
        public ICommand MonthYearCheckingCommand { get; set; }

        #endregion
        #region Methods
        public void LoadMonth()
        {
            months.Add("1");
            months.Add("2");
            months.Add("3");
            months.Add("4");
            months.Add("5");
            months.Add("6");
            months.Add("7");
            months.Add("8");
            months.Add("9");
            months.Add("10");
            months.Add("11");
            months.Add("12");

            Months = months;
        }
        public void LoadYear()
        {
            years.Add("2017");
            years.Add("2018");
            years.Add("2019");
            years.Add("2020");
            years.Add("2021");
            years.Add("2022");
            years.Add("2023");
            years.Add("2024");

            Years = years;
        }
        public void ResetSum()
        {
            DecSumofProfit = 0;
            DecSumofPaid = 0;
        }        
        public void DayMonthCheck()
        {
            SeriesCollection.Clear();
            ResetSum();
            if (int.Parse(SelectedYear) == -1 || int.Parse(SelectedMonth) == -1) return;
            
            int NumofDay = DateTime.DaysInMonth(int.Parse(SelectedYear), int.Parse(SelectedMonth));
            Visibility = "Visibility";

            double[] month = new double[NumofDay];
            string[] months = new string[NumofDay];
            for (int i = 0; i < month.Length; i++)
            {
                month[i] = i + 1;
                months[i] = "Ngày " + month[i].ToString();
            }
            Labels = months;
            
            //Mang doanh thu
            double[] ProfitbyMonth = new double[NumofDay];
            for (int i = 0; i < NumofDay; i++)
            {
                //Lay ngay dang xet
                DateTime day = new DateTime(int.Parse(SelectedYear), int.Parse(SelectedMonth), i + 1);

                //Tinh so tien thu duoc theo ngay cua thang
                ProfitbyMonth[i] = ThongKeDP.Flag.GetBillofDay(day.ToShortDateString()) / 1000000; 
                DecSumofProfit += ThongKeDP.Flag.GetBillofDay(day.ToShortDateString());
                SumofProfit = String.Format("{0:0,0 VND}", DecSumofProfit);
            }
            SeriesCollection.Add(new LineSeries
            {
                Title = "Thu",
                Values = new ChartValues<double>(ProfitbyMonth)
            });

            //Mang chi ra
            double[] PaidbyMonth = new double[NumofDay];
            for (int i = 0; i < NumofDay; i++)
            {
                //Lay ngay dang xet
                DateTime day = new DateTime(int.Parse(SelectedYear), int.Parse(SelectedMonth), i + 1);

                //Tinh so tien chi ra theo ngay cua thang
                PaidbyMonth[i] = ThongKeDP.Flag.GetPaidofDay(day.ToShortDateString()) / 1000000;
                DecSumofPaid += ThongKeDP.Flag.GetPaidofDay(day.ToShortDateString());
                SumofPaid = String.Format("{0:0,0 VND}", DecSumofPaid);
            }
            SeriesCollection.Add(new LineSeries
            {
                Title = "Chi",
                Values = new ChartValues<double>(PaidbyMonth)
            });
        }
        public void MonthYearCheck()
        {
            SeriesCollection.Clear();
            ResetSum();
            if (int.Parse(SelectedYear) == -1 || int.Parse(SelectedMonth) == -1) return;

            Visibility = "Visibility";
            Labels = new[] { "Tháng 1", "Tháng 2", "Tháng 3", "Tháng 4", "Tháng 5", "Tháng 6", "Tháng 7", "Tháng 8", "Tháng 9", "Tháng 10", "Tháng 11", "Tháng 12" };

            //Mang doanh thu
            double[] ProfitbyYear = new double[12];
            for (int i = 0; i < 12; i++)
            {
                //Tinh so tien thu duoc theo thang cua nam 

                ProfitbyYear[i] = ThongKeDP.Flag.GetBillofMonth((i + 1).ToString(), SelectedYear) / 1000000;  
                DecSumofProfit += ThongKeDP.Flag.GetBillofMonth((i + 1).ToString(), SelectedYear);
                SumofProfit = String.Format("{0:0,0 VND}", DecSumofProfit);
            }
            SeriesCollection.Add(new LineSeries
            {
                Title = "Thu",
                Values = new ChartValues<double>(ProfitbyYear)
            });

            //Mang chi ra
            double[] PaidbyYear = new double[12];
            for (int i = 0; i < 12; i++)
            {
                //Tinh so tien chi ra theo thang cua nam

                PaidbyYear[i] = ThongKeDP.Flag.GetPaidofMonth((i + 1).ToString(), SelectedYear) / 1000000; 
                DecSumofPaid += ThongKeDP.Flag.GetPaidofMonth((i + 1).ToString(), SelectedYear);
                SumofPaid = String.Format("{0:0,0 VND}", DecSumofPaid);
            }
            SeriesCollection.Add(new LineSeries
            {
                Title = "Chi",
                Values = new ChartValues<double>(PaidbyYear)
            });
        }
        #endregion
    }
}