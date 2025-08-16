using Zodpovedne.MAUIApp.ViewModels;

namespace Zodpovedne.MAUIApp;

public partial class MainPage : ContentPage
{
    public MainPage(ChatViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Zajistíme, že se kód pro registraci spustí, až když je stránka viditelná
        if (BindingContext is ChatViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}