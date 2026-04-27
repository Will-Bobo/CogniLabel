using CogniLabel.Presentation.ViewModels;
using System.Windows;

namespace CogniLabel.Presentation.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}

