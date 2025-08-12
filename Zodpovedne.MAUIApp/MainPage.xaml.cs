using Zodpovedne.MAUIApp.ViewModels;

namespace Zodpovedne.MAUIApp;

public partial class MainPage : ContentPage
{
    // Konstruktor nyní přijímá ViewModel jako parametr.
    // MAUI se postará o jeho automatické předání díky registraci v MauiProgram.cs.
    public MainPage(ChatViewModel viewModel)
    {
        InitializeComponent();

        // Nastavíme 'BindingContext' stránky na náš ViewModel.
        // Tím se propojí všechny {Binding} v XAML s vlastnostmi a příkazy ve ViewModelu.
        BindingContext = viewModel;
    }
}