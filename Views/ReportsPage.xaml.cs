using Microsoft.Maui.Controls;
using PersonalExpenseTracker.ViewModels;

namespace PersonalExpenseTracker.Views
{
    public partial class ReportsPage : ContentPage
    {
        private readonly ReportsViewModel _viewModel;

        public ReportsPage()
        {
            InitializeComponent();
            _viewModel = new ReportsViewModel();
            BindingContext = _viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Refresh whenever we navigate to this page
            _ = _viewModel.ReloadAsync();
        }
    }
}