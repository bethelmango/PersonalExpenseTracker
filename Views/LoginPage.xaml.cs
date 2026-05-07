using Microsoft.Maui.Controls;

namespace PersonalExpenseTracker.Views
{
    public partial class LoginPage : ContentPage
    {
        public LoginPage()
        {
            InitializeComponent();
            // No BindingContext wired for now – login is not functional without Firebase.
        }
    }
}