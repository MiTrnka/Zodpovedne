using Zodpovedne.MAUIApp.ViewModels;

namespace Zodpovedne.MAUIApp.Views;

public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _viewModel;

    /// <summary>
    /// Konstruktor stránky. Dependency Injection sem automaticky dodá
    /// instanci ChatViewModelu, kterou jsme zaregistrovali v MauiProgram.cs.
    /// </summary>
    public ChatPage(ChatViewModel viewModel)
    {
        InitializeComponent();

        // Uloíme si ViewModel a nastavíme ho jako BindingContext.
        // Tím se propojí data z ViewModelu s UI prvky v XAMLu.
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    /// <summary>
    /// Tato metoda se zavolá vdy, kdy se stránka zobrazí na obrazovce.
    /// Je to ideální místo pro naètení dat a spuštìní odbìrù.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            // Pokusíme se inicializovat ViewModel
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            // Pokud cokoliv bìhem inicializace sele (napø. síové pøipojení),
            // zachytíme chybu a zobrazíme ji uivateli v alertu.
            // To je mnohem lepší ne tichı pád aplikace.
            await DisplayAlert("Chyba pøi inicializaci", $"Nepodaøilo se pøipojit k serveru. Zkontrolujte prosím své pøipojení.\n\nDetail: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Tato metoda se zavolá vdy, kdy uivatel opustí stránku.
    /// Je naprosto klíèové zde ukonèit odbìry a uzavøít spojení,
    /// abychom pøedešli únikùm pamìti a zbyteènému zatíení.
    /// </summary>
    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await _viewModel.CleanupAsync();
    }
}