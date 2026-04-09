using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EliteDataCollector.UI.Services;

namespace EliteDataCollector.UI.ViewModels
{
    public partial class NavigationViewModel : ObservableObject
    {
        [ObservableProperty]
        private string selectedPage = "Dashboard";

        [RelayCommand]
        public void NavigateTo(string pageName)
        {
            SelectedPage = pageName;
        }
    }
}

