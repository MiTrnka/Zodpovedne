using Zodpovedne.MAUIApp.ViewModels;

namespace Zodpovedne.MAUIApp.Views;

public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _viewModel;

    /// <summary>
    /// Konstruktor str�nky. Dependency Injection sem automaticky dod�
    /// instanci ChatViewModelu, kterou jsme zaregistrovali v MauiProgram.cs.
    /// </summary>
    public ChatPage(ChatViewModel viewModel)
    {
        InitializeComponent();

        // Ulo��me si ViewModel a nastav�me ho jako BindingContext.
        // T�m se propoj� data z ViewModelu s UI prvky v XAMLu.
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    /// <summary>
    /// Tato metoda se zavol� v�dy, kdy� se str�nka zobraz� na obrazovce.
    /// Je to ide�ln� m�sto pro na�ten� dat a spu�t�n� odb�r�.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            // Pokus�me se inicializovat ViewModel
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            // Pokud cokoliv b�hem inicializace sel�e (nap�. s�ov� p�ipojen�),
            // zachyt�me chybu a zobraz�me ji u�ivateli v alertu.
            // To je mnohem lep�� ne� tich� p�d aplikace.
            await DisplayAlert("Chyba p�i inicializaci", $"Nepoda�ilo se p�ipojit k serveru. Zkontrolujte pros�m sv� p�ipojen�.\n\nDetail: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Tato metoda se zavol� v�dy, kdy� u�ivatel opust� str�nku.
    /// Je naprosto kl��ov� zde ukon�it odb�ry a uzav��t spojen�,
    /// abychom p�ede�li �nik�m pam�ti a zbyte�n�mu zat�en�.
    /// </summary>
    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await _viewModel.CleanupAsync();
    }
}