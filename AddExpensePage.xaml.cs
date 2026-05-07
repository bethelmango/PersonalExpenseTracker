using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PersonalExpenseTracker;
using PersonalExpenseTracker.Data;
using PersonalExpenseTracker.Models;
using PersonalExpenseTracker.ViewModels;

namespace PersonalExpenseTracker;

public partial class AddExpensePage : ContentPage
{
    public AddExpensePage()
    {
        InitializeComponent();
        BindingContext = new AddExpensePageViewModel();
    }
}