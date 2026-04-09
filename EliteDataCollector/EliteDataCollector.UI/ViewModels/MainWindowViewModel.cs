using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EliteDataCollector.UI.Services;
using System.Collections.ObjectModel;

namespace EliteDataCollector.UI.ViewModels
{
    /// <summary>
    /// Main window view model coordinating navigation and window layout.
    /// </summary>
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly NavigationViewModel _navigationViewModel;

        [ObservableProperty]
        private string appTitle = "Elite Data Collector";

        [ObservableProperty]
        private string commanderName = "Commander";

        public NavigationViewModel NavigationViewModel => _navigationViewModel;

        public MainWindowViewModel(NavigationViewModel navigationViewModel)
        {
            _navigationViewModel = navigationViewModel;
        }

        public void SetCommanderName(string name)
        {
            CommanderName = name;
        }
    }
}

