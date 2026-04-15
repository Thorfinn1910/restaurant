using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Diacritics.Extensions;
using QuanLyNhaHang.DataProvider;
using QuanLyNhaHang.Models;
using QuanLyNhaHang.View;

namespace QuanLyNhaHang.ViewModel
{
    public class MenuAdminViewModel : BaseViewModel
    {
        public enum AddDishStep
        {
            DishInfo = 0,
            Recipe = 1
        }

        public MenuAdminViewModel()
        {
            MenuItems = new ObservableCollection<Models.MenuItem>();
            Ingredients = MenuDP.Flag.GetIngredients();
            _ingredients_ForDishes = new ObservableCollection<ChiTietMon>();
            Deleted_Ingredients = new ObservableCollection<ChiTietMon>();

            _menuItemsView = new CollectionViewSource { Source = MenuItems };
            _menuItemsView.Filter += MenuItems_Filter;

            _ingredientsView = new CollectionViewSource { Source = Ingredients };
            _ingredientsView.Filter += Ingredients_Filter;

            MenuItem = new Models.MenuItem();
            AddItem = CreateDefaultAddItem();
            DishHasBeenAdded = false;
            _activeAddDishId = string.Empty;
            _activeAddDishName = string.Empty;
            _isCurrentAddDishPersisted = false;
            CurrentAddDishStep = AddDishStep.DishInfo;

            EditView = Visibility.Visible;
            AddView = Visibility.Collapsed;

            AddDishes_Command = new RelayCommand<object>((p) => true, (p) =>
            {
                AddView = Visibility.Visible;
                EditView = Visibility.Collapsed;
                ResetAddDishFlow();
            });

            SwitchToEditView_Command = new RelayCommand<object>((p) => true, (p) =>
            {
                AddView = Visibility.Collapsed;
                EditView = Visibility.Visible;
            });

            RemoveDish_Command = new RelayCommand<object>((p) =>
            {
                if (MenuItem?.FoodImage == null
                    || string.IsNullOrWhiteSpace(MenuItem.FoodName)
                    || string.IsNullOrWhiteSpace(MenuItem.ID))
                {
                    return false;
                }

                return true;
            }, (p) =>
            {
                MyMessageBox msb = new MyMessageBox("Bạn có chắc chắn xóa cứng món ăn này và toàn bộ dữ liệu liên quan?", true);
                msb.ShowDialog();
                if (!msb.ACCEPT())
                {
                    return;
                }

                try
                {
                    MenuDP.Flag.HardDeleteDishCascade(MenuItem.ID);
                    MenuItems.Remove(MenuItem);
                    MenuItem = new Models.MenuItem();
                    ShowMessage("Xóa cứng thành công!");
                }
                catch (InvalidOperationException ex)
                {
                    ShowMessage(ex.Message);
                }
                catch (SqlException)
                {
                    ShowMessage("Lỗi cơ sở dữ liệu khi xóa cứng món ăn.");
                }
            });

            AddDish_Command = new RelayCommand<object>((p) => CanSaveAndGoToRecipe(), (p) => SaveAndGoToRecipe());
            SaveAndGoToRecipe_Command = new RelayCommand<object>((p) => CanSaveAndGoToRecipe(), (p) => SaveAndGoToRecipe());
            BackToDishInfo_Command = new RelayCommand<object>((p) => CurrentAddDishStep == AddDishStep.Recipe, (p) =>
            {
                CurrentAddDishStep = AddDishStep.DishInfo;
            });
            FinishCreateDish_Command = new RelayCommand<object>((p) => CanFinishCreateDish(), (p) => FinishCreateDish());

            AddImage_Command = new RelayCommand<object>((p) => true, (p) =>
            {
                OpenFileDialog op = new OpenFileDialog
                {
                    Filter = "All supported graphics|*.jpg;*.jpeg;*.png|" +
                             "JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
                             "Portable Network Graphic (*.png)|*.png",
                    Title = "Thêm ảnh món ăn"
                };

                if (op.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                BitmapImage bmi = new BitmapImage();
                bmi.BeginInit();
                bmi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bmi.CacheOption = BitmapCacheOption.OnLoad;
                bmi.UriSource = new Uri(op.FileName);
                bmi.EndInit();
                AddItem.FoodImage = bmi;
            });

            SaveChanges_Command = new RelayCommand<object>((p) =>
            {
                return !(MenuItem?.IsNullOrEmpty() ?? true);
            }, (p) =>
            {
                try
                {
                    MenuDP.Flag.EditDishInfo(MenuItem);
                    ShowMessage("Sửa thành công!");
                }
                catch (Exception ex)
                {
                    ShowMessage(ex.Message);
                }
            });

            DiscardChanges_Command = new RelayCommand<object>((p) => true, (p) =>
            {
                if (string.IsNullOrWhiteSpace(MenuItem?.ID))
                {
                    return;
                }

                MenuItem = MenuDP.Flag.GetDishInfo(MenuItem.ID);
            });

            EditFoodImage_Command = new RelayCommand<object>((p) => true, (p) =>
            {
                OpenFileDialog op = new OpenFileDialog
                {
                    Filter = "All supported graphics|*.jpg;*.jpeg;*.png|" +
                             "JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
                             "Portable Network Graphic (*.png)|*.png",
                    Title = "Đổi ảnh món ăn"
                };

                if (op.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                BitmapImage bmi = new BitmapImage();
                bmi.BeginInit();
                bmi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bmi.CacheOption = BitmapCacheOption.OnLoad;
                bmi.UriSource = new Uri(op.FileName);
                bmi.EndInit();
                MenuItem.FoodImage = bmi;
            });

            AddIngredient_Command = new RelayCommand<object>((p) => CanOpenLegacyIngredientWindow(), (p) =>
            {
                MenuAdmin_ThemNLieu ingreAddView = new MenuAdmin_ThemNLieu();
                ingreAddView.DataContext = this;
                ingreAddView.ShowDialog();
            });

            AddIngredientsToDish_Command = new RelayCommand<object>((p) => ResolveIngredient(p) != null && CanAddIngredientToCurrentDish(), (p) =>
            {
                AddIngredientToCurrentDish(ResolveIngredient(p)!);
            });
            QuickAddIngredient_Command = new RelayCommand<object>((p) => ResolveIngredient(p) != null && CanAddIngredientToCurrentDish(), (p) =>
            {
                AddIngredientToCurrentDish(ResolveIngredient(p)!);
            });
            IncreaseIngredientQuantity_Command = new RelayCommand<ChiTietMon>((p) => p != null, (p) =>
            {
                p.SoLuong += 1;
            });
            DecreaseIngredientQuantity_Command = new RelayCommand<ChiTietMon>((p) => p != null, (p) =>
            {
                p.SoLuong = Math.Max(1, p.SoLuong - 1);
            });

            SaveDishIngredients_Command = new RelayCommand<object>((p) => CanSaveDishIngredients(), (p) =>
            {
                SaveCurrentDishRecipe(false);
            });

            HideIngredientWindow_Command = new RelayCommand<Window>((p) => true, (p) =>
            {
                p.Close();
                Deleted_Ingredients.Clear();
            });

            RemoveIngredientFromDish_Command = new RelayCommand<ChiTietMon>((p) => p != null, (p) =>
            {
                Ingredients_ForDishes.Remove(p);
                if (EditView == Visibility.Visible && !Deleted_Ingredients.Any(x => string.Equals(x.MaMon, p.MaMon, StringComparison.OrdinalIgnoreCase) && string.Equals(x.TenNL, p.TenNL, StringComparison.OrdinalIgnoreCase)))
                {
                    Deleted_Ingredients.Add(p);
                }
            });

            EditIngredient_Command = new RelayCommand<object>((p) => !string.IsNullOrWhiteSpace(MenuItem?.ID), (p) =>
            {
                Ingredients_ForDishes = MenuDP.Flag.GetIngredientsForDish(MenuItem.ID);
                Deleted_Ingredients.Clear();
                MenuAdmin_ThemNLieu ingreAddView = new MenuAdmin_ThemNLieu();
                ingreAddView.DataContext = this;
                ingreAddView.ShowDialog();
            });

            _ = LoadMenu();
        }

        #region attributes
        private ObservableCollection<Models.MenuItem> _menuitems;
        private ObservableCollection<Models.Kho> _ingredients;
        private ObservableCollection<ChiTietMon> _ingredients_ForDishes;
        private ObservableCollection<ChiTietMon> _deletedIngredients;
        private string _filterText;
        private string _ingreFilterText;
        private CollectionViewSource _menuItemsView;
        private CollectionViewSource _ingredientsView;
        private Models.MenuItem _menuitem;
        private Models.Kho? _selected_Ingredient;
        private Visibility editView;
        private Visibility addView;
        private Models.MenuItem addItem;
        private bool _dishHasBeenAdded;
        private AddDishStep _currentAddDishStep;
        private bool _isCurrentAddDishPersisted;
        private string _activeAddDishId;
        private string _activeAddDishName;
        #endregion

        #region properties
        public ObservableCollection<Models.MenuItem> MenuItems
        {
            get { return _menuitems; }
            set
            {
                _menuitems = value ?? new ObservableCollection<Models.MenuItem>();
                if (_menuItemsView != null)
                {
                    _menuItemsView.Source = _menuitems;
                    _menuItemsView.View.Refresh();
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(MenuItemCollection));
            }
        }

        public ObservableCollection<Models.Kho> Ingredients { get { return _ingredients; } set { _ingredients = value; OnPropertyChanged(); } }

        public ObservableCollection<ChiTietMon> Ingredients_ForDishes
        {
            get { return _ingredients_ForDishes; }
            set
            {
                _ingredients_ForDishes = value ?? new ObservableCollection<ChiTietMon>();
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ChiTietMon> Deleted_Ingredients { get { return _deletedIngredients; } set { _deletedIngredients = value; OnPropertyChanged(); } }
        public Models.MenuItem MenuItem { get { return _menuitem; } set { _menuitem = value; OnPropertyChanged(); } }

        public Models.MenuItem AddItem
        {
            get { return addItem; }
            set
            {
                addItem = value ?? CreateDefaultAddItem();
                OnPropertyChanged();
            }
        }

        public Models.Kho? Selected_Ingredient
        {
            get { return _selected_Ingredient; }
            set
            {
                _selected_Ingredient = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }
        public bool DishHasBeenAdded { get { return _dishHasBeenAdded; } set { _dishHasBeenAdded = value; OnPropertyChanged(); } }
        public Visibility EditView
        {
            get { return editView; }
            set
            {
                editView = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public Visibility AddView
        {
            get { return addView; }
            set
            {
                addView = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }
        public string FilterText { get { return _filterText; } set { _filterText = value; _menuItemsView.View.Refresh(); OnPropertyChanged(); } }
        public string IngreFilterText { get { return _ingreFilterText; } set { _ingreFilterText = value; _ingredientsView.View.Refresh(); OnPropertyChanged(); } }

        public AddDishStep CurrentAddDishStep
        {
            get { return _currentAddDishStep; }
            set
            {
                if (_currentAddDishStep == value)
                {
                    return;
                }

                _currentAddDishStep = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AddDishInfoStepView));
                OnPropertyChanged(nameof(AddDishRecipeStepView));
                OnPropertyChanged(nameof(AddDishStepTitle));
                OnPropertyChanged(nameof(AddDishStepDescription));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public Visibility AddDishInfoStepView => CurrentAddDishStep == AddDishStep.DishInfo ? Visibility.Visible : Visibility.Collapsed;
        public Visibility AddDishRecipeStepView => CurrentAddDishStep == AddDishStep.Recipe ? Visibility.Visible : Visibility.Collapsed;
        public string AddDishStepTitle => CurrentAddDishStep == AddDishStep.DishInfo ? "Bước 1/2: Thông tin món ăn" : "Bước 2/2: Công thức nguyên liệu";
        public string AddDishStepDescription => CurrentAddDishStep == AddDishStep.DishInfo
            ? "Nhập đủ thông tin bắt buộc và lưu món trước khi khai báo công thức."
            : "Thêm nguyên liệu vào công thức, chỉnh số lượng rồi hoàn tất tạo món.";

        public bool IsAddDishInfoReadOnly => _isCurrentAddDishPersisted;
        public string SaveAndGoToRecipeButtonText => _isCurrentAddDishPersisted ? "Tiếp tục sang bước 2" : "Lưu và sang bước 2";

        public string ActiveAddDishSummary =>
            string.IsNullOrWhiteSpace(_activeAddDishId)
                ? "Chưa có món được lưu."
                : $"{_activeAddDishId} - {_activeAddDishName}";

        public ICollectionView MenuItemCollection
        {
            get
            {
                return _menuItemsView.View;
            }
        }

        public ICollectionView IngredientCollection
        {
            get
            {
                return _ingredientsView.View;
            }
        }
        #endregion

        #region commands
        public ICommand AddDishes_Command { get; set; }
        public ICommand AddDish_Command { get; set; }
        public ICommand SwitchToEditView_Command { get; set; }
        public ICommand RemoveDish_Command { get; set; }
        public ICommand AddImage_Command { get; set; }
        public ICommand SaveChanges_Command { get; set; }
        public ICommand DiscardChanges_Command { get; set; }
        public ICommand EditFoodImage_Command { get; set; }
        public ICommand AddIngredient_Command { get; set; }
        public ICommand EditIngredient_Command { get; set; }
        public ICommand AddIngredientsToDish_Command { get; set; }
        public ICommand QuickAddIngredient_Command { get; set; }
        public ICommand IncreaseIngredientQuantity_Command { get; set; }
        public ICommand DecreaseIngredientQuantity_Command { get; set; }
        public ICommand RemoveIngredientFromDish_Command { get; set; }
        public ICommand SaveDishIngredients_Command { get; set; }
        public ICommand HideIngredientWindow_Command { get; set; }
        public ICommand SaveAndGoToRecipe_Command { get; set; }
        public ICommand BackToDishInfo_Command { get; set; }
        public ICommand FinishCreateDish_Command { get; set; }
        #endregion

        #region complementary functions
        public BitmapImage converting(string ur)
        {
            BitmapImage bmi = new BitmapImage();
            bmi.BeginInit();
            bmi.CacheOption = BitmapCacheOption.OnLoad;
            bmi.UriSource = new Uri(ur);
            bmi.EndInit();

            return bmi;
        }

        private Models.MenuItem CreateDefaultAddItem()
        {
            return new Models.MenuItem
            {
                FoodImage = converting("pack://application:,,,/images/menu_default_image.jpg")
            };
        }

        private Models.MenuItem CloneMenuItem(Models.MenuItem source)
        {
            return new Models.MenuItem(
                source.ID?.Trim() ?? string.Empty,
                source.FoodName?.Trim() ?? string.Empty,
                source.Price,
                source.FoodImage,
                source.CookingTime);
        }

        private bool IsValidDishId(string dishId)
        {
            return Regex.IsMatch(dishId, "^[A-Za-z0-9_-]+$");
        }

        private bool HasDuplicateDishId(string dishId)
        {
            return MenuItems.Any(mi => string.Equals(mi.ID, dishId, StringComparison.OrdinalIgnoreCase));
        }

        private List<string> CollectDishInfoErrors()
        {
            List<string> errors = new List<string>();
            string idText = (AddItem?.ID ?? string.Empty).Trim();
            string nameText = (AddItem?.FoodName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(idText))
            {
                errors.Add("Mã món là bắt buộc.");
            }
            else if (!IsValidDishId(idText))
            {
                errors.Add("Mã món chỉ gồm chữ, số, '_' hoặc '-'.");
            }
            else if (!_isCurrentAddDishPersisted && HasDuplicateDishId(idText))
            {
                errors.Add("Mã món đã tồn tại.");
            }

            if (string.IsNullOrWhiteSpace(nameText))
            {
                errors.Add("Tên món là bắt buộc.");
            }

            if (AddItem == null || AddItem.Price <= 0)
            {
                errors.Add("Giá món phải lớn hơn 0.");
            }

            if (AddItem == null || AddItem.CookingTime <= 0)
            {
                errors.Add("Thời gian nấu phải lớn hơn 0.");
            }

            if (AddItem?.FoodImage == null)
            {
                errors.Add("Vui lòng thêm ảnh món.");
            }

            return errors;
        }

        private List<string> CollectRecipeErrors()
        {
            List<string> errors = new List<string>();
            if (Ingredients_ForDishes.Count == 0)
            {
                errors.Add("Công thức phải có ít nhất 1 nguyên liệu.");
            }

            if (CheckIfIngredientListInclude0InQuantity())
            {
                errors.Add("Số lượng nguyên liệu phải lớn hơn 0.");
            }

            HashSet<string> seenIngredients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ChiTietMon ingredient in Ingredients_ForDishes)
            {
                string ingredientName = (ingredient.TenNL ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(ingredientName))
                {
                    errors.Add("Tên nguyên liệu không hợp lệ.");
                    continue;
                }

                if (!seenIngredients.Add(ingredientName))
                {
                    errors.Add($"Nguyên liệu '{ingredientName}' bị trùng trong công thức.");
                }
            }

            return errors;
        }

        private void ShowValidationSummary(IEnumerable<string> errors, string header)
        {
            List<string> errorList = errors.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            if (errorList.Count == 0)
            {
                return;
            }

            ShowMessage(header + "\n- " + string.Join("\n- ", errorList));
        }

        private void SetCurrentAddDishPersisted(bool value)
        {
            if (_isCurrentAddDishPersisted == value)
            {
                return;
            }

            _isCurrentAddDishPersisted = value;
            DishHasBeenAdded = value;
            OnPropertyChanged(nameof(IsAddDishInfoReadOnly));
            OnPropertyChanged(nameof(SaveAndGoToRecipeButtonText));
            CommandManager.InvalidateRequerySuggested();
        }

        private bool CanSaveAndGoToRecipe()
        {
            return CurrentAddDishStep == AddDishStep.DishInfo;
        }

        private void SaveAndGoToRecipe()
        {
            if (_isCurrentAddDishPersisted)
            {
                CurrentAddDishStep = AddDishStep.Recipe;
                return;
            }

            List<string> errors = CollectDishInfoErrors();
            if (errors.Count > 0)
            {
                ShowValidationSummary(errors, "Không thể lưu thông tin món. Vui lòng sửa các lỗi:");
                return;
            }

            try
            {
                Models.MenuItem newDish = CloneMenuItem(AddItem);
                MenuDP.Flag.AddDish(newDish);
                MenuItems.Add(newDish);

                _activeAddDishId = newDish.ID;
                _activeAddDishName = newDish.FoodName;
                OnPropertyChanged(nameof(ActiveAddDishSummary));

                SetCurrentAddDishPersisted(true);
                Ingredients_ForDishes = new ObservableCollection<ChiTietMon>();
                Deleted_Ingredients.Clear();

                CurrentAddDishStep = AddDishStep.Recipe;
            }
            catch (SqlException)
            {
                ShowMessage("Mã món đã tồn tại hoặc dữ liệu không hợp lệ.");
            }
        }

        private void ResetAddDishFlow()
        {
            _activeAddDishId = string.Empty;
            _activeAddDishName = string.Empty;
            OnPropertyChanged(nameof(ActiveAddDishSummary));

            SetCurrentAddDishPersisted(false);
            AddItem = CreateDefaultAddItem();
            Selected_Ingredient = null;
            IngreFilterText = string.Empty;
            Ingredients_ForDishes = new ObservableCollection<ChiTietMon>();
            Deleted_Ingredients.Clear();
            CurrentAddDishStep = AddDishStep.DishInfo;
        }

        private void FinishCreateDish()
        {
            if (!_isCurrentAddDishPersisted || string.IsNullOrWhiteSpace(_activeAddDishId))
            {
                ShowMessage("Bạn cần lưu món ở bước 1 trước khi hoàn tất.");
                return;
            }

            SaveCurrentDishRecipe(true);
        }

        private bool CanFinishCreateDish()
        {
            return CurrentAddDishStep == AddDishStep.Recipe
                && _isCurrentAddDishPersisted;
        }

        private bool CanSaveDishIngredients()
        {
            return !string.IsNullOrWhiteSpace(GetCurrentDishId());
        }

        private bool CanOpenLegacyIngredientWindow()
        {
            if (EditView == Visibility.Visible)
            {
                return !string.IsNullOrWhiteSpace(MenuItem?.ID);
            }

            return _isCurrentAddDishPersisted;
        }

        private Models.Kho? ResolveIngredient(object? parameter)
        {
            if (parameter is Models.Kho ingredient)
            {
                return ingredient;
            }

            return Selected_Ingredient;
        }

        private bool CanAddIngredientToCurrentDish()
        {
            if (EditView == Visibility.Visible)
            {
                return !string.IsNullOrWhiteSpace(MenuItem?.ID);
            }

            return AddView == Visibility.Visible
                && _isCurrentAddDishPersisted
                && CurrentAddDishStep == AddDishStep.Recipe;
        }

        private string GetCurrentDishId()
        {
            if (EditView == Visibility.Visible)
            {
                return MenuItem?.ID ?? string.Empty;
            }

            return _activeAddDishId;
        }

        private string GetCurrentDishName()
        {
            if (EditView == Visibility.Visible)
            {
                return MenuItem?.FoodName ?? string.Empty;
            }

            return _activeAddDishName;
        }

        private void AddIngredientToCurrentDish(Models.Kho ingredient)
        {
            string dishId = GetCurrentDishId();
            if (string.IsNullOrWhiteSpace(dishId))
            {
                ShowMessage("Vui lòng chọn hoặc tạo món trước khi thêm nguyên liệu.");
                return;
            }

            if (IsListedInIngredientList(ingredient.TenSanPham))
            {
                ShowMessage("Nguyên liệu này đã có trong công thức.");
                return;
            }

            Ingredients_ForDishes.Add(new ChiTietMon(ingredient.TenSanPham, dishId, 1));
        }

        private void SaveCurrentDishRecipe(bool completeAddFlow)
        {
            string dishId = GetCurrentDishId();
            string dishName = GetCurrentDishName();
            if (string.IsNullOrWhiteSpace(dishId))
            {
                ShowMessage("Không xác định được món cần lưu công thức.");
                return;
            }

            List<string> recipeErrors = CollectRecipeErrors();
            if (recipeErrors.Count > 0)
            {
                ShowValidationSummary(recipeErrors, "Không thể lưu công thức. Vui lòng sửa các lỗi:");
                return;
            }

            string message;
            try
            {
                foreach (ChiTietMon ctm in Ingredients_ForDishes)
                {
                    ctm.MaMon = dishId;
                    MenuDP.Flag.SaveIngredients(ctm);
                }

                if (EditView == Visibility.Visible)
                {
                    foreach (ChiTietMon ctm in Deleted_Ingredients)
                    {
                        MenuDP.Flag.RemoveIngredients(ctm);
                    }
                }

                message = $"Đã lưu công thức cho món {dishName}.";
            }
            catch (SqlException)
            {
                foreach (ChiTietMon ctm in Ingredients_ForDishes)
                {
                    ctm.MaMon = dishId;
                    int n = MenuDP.Flag.UpdateIngredients(ctm);
                    if (n == 0)
                    {
                        MenuDP.Flag.SaveIngredients(ctm);
                    }
                }

                if (EditView == Visibility.Visible)
                {
                    foreach (ChiTietMon ctm in Deleted_Ingredients)
                    {
                        MenuDP.Flag.RemoveIngredients(ctm);
                    }
                }

                message = $"Đã cập nhật công thức cho món {dishName}.";
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
                return;
            }

            Deleted_Ingredients.Clear();
            ShowMessage(message);

            if (completeAddFlow)
            {
                ResetAddDishFlow();
            }
        }

        private void ShowMessage(string message)
        {
            MyMessageBox msb = new MyMessageBox(message);
            msb.Show();
        }

        public bool IsListedInIngredientList(string tenNl)
        {
            if (Ingredients_ForDishes.Count == 0)
            {
                return false;
            }

            foreach (ChiTietMon ctm in Ingredients_ForDishes)
            {
                if (string.Equals(ctm.TenNL, tenNl, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsListedInMenuList(string maMon)
        {
            if (MenuItems.Count == 0)
            {
                return false;
            }

            foreach (Models.MenuItem mi in MenuItems)
            {
                if (string.Equals(mi.ID, maMon, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public bool CheckIfIngredientListInclude0InQuantity()
        {
            foreach (ChiTietMon ctm in Ingredients_ForDishes)
            {
                if (ctm.SoLuong <= 0)
                {
                    return true;
                }
            }
            return false;
        }

        public void MenuItems_Filter(object sender, FilterEventArgs e)
        {
            if (string.IsNullOrEmpty(FilterText))
            {
                e.Accepted = true;
                return;
            }

            Models.MenuItem? item = e.Item as Models.MenuItem;
            string foodName = item?.FoodName ?? string.Empty;
            e.Accepted = foodName.RemoveDiacritics().ToLower().Contains(FilterText.RemoveDiacritics().ToLower());
        }

        private void Ingredients_Filter(object sender, FilterEventArgs e)
        {
            if (string.IsNullOrEmpty(IngreFilterText))
            {
                e.Accepted = true;
                return;
            }

            Models.Kho? item = e.Item as Models.Kho;
            string ingredientName = item?.TenSanPham ?? string.Empty;
            e.Accepted = ingredientName.RemoveDiacritics().ToLower().Contains(IngreFilterText.RemoveDiacritics().ToLower());
        }

        private async Task LoadMenu()
        {
            ObservableCollection<Models.MenuItem> loadedItems = await MenuDP.Flag.ConvertToCollection();
            MenuItems = loadedItems;
        }
        #endregion
    }
}