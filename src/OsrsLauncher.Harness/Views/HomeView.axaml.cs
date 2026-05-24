// SPDX-License-Identifier: GPL-3.0-or-later
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OsrsLauncher.Core.App;
using OsrsLauncher.Core.Launch;
using OsrsLauncher.Core.Persistence;

namespace OsrsLauncher.Harness.Views;

public partial class HomeView : UserControl
{
    private readonly MainWindow _owner;
    private readonly HomeViewModel _vm;

    public HomeView(MainWindow owner, ICredentialStore store, RuneLiteLauncher launcher)
    {
        _owner = owner;
        _vm    = new HomeViewModel(store, launcher);

        InitializeComponent();
        Render();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void Render()
    {
        var statusLabel         = this.FindControl<TextBlock>("StatusLabel")!;
        var primaryBtn          = this.FindControl<Button>("PrimaryButton")!;
        var switchCharacterBtn  = this.FindControl<Button>("SwitchCharacterButton")!;
        var switchBtn           = this.FindControl<Button>("SwitchButton")!;

        if (_vm.IsLoggedIn)
        {
            statusLabel.Text           = $"Ready to play as {_vm.CharacterName}";
            primaryBtn.Content         = "Play";
            switchCharacterBtn.IsVisible = _vm.CanSwitchCharacter;
            switchBtn.IsVisible        = true;
        }
        else
        {
            statusLabel.Text           = "Not logged in";
            primaryBtn.Content         = "Log in";
            switchCharacterBtn.IsVisible = false;
            switchBtn.IsVisible        = false;
        }
    }

    private void OnPrimaryButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.IsLoggedIn)
        {
            var inlineStatus = this.FindControl<TextBlock>("InlineStatus")!;
            try
            {
                _vm.Play();
                inlineStatus.IsVisible = true;
                inlineStatus.Foreground = Avalonia.Media.Brushes.Green;
                inlineStatus.Text = "Launched — you can close this window.";
            }
            catch (RuneLiteNotFoundException ex)
            {
                inlineStatus.IsVisible = true;
                inlineStatus.Foreground = Avalonia.Media.Brushes.Red;
                inlineStatus.Text = ex.Message;
            }
            catch (Exception ex)
            {
                inlineStatus.IsVisible = true;
                inlineStatus.Foreground = Avalonia.Media.Brushes.Red;
                inlineStatus.Text = $"Launch error: {ex.Message}";
            }
        }
        else
        {
            _owner.ShowLogin();
        }
    }

    private void OnSwitchCharacterButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _owner.ShowStoredCharacterPicker();

    private void OnSwitchButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _owner.ShowLogin();

    /// <summary>Display an inline status message (e.g. after returning from a failed login).</summary>
    public void SetStatus(string message, bool isError = false)
    {
        var inlineStatus = this.FindControl<TextBlock>("InlineStatus")!;
        inlineStatus.IsVisible  = !string.IsNullOrEmpty(message);
        inlineStatus.Foreground = isError ? Avalonia.Media.Brushes.Red : Avalonia.Media.Brushes.Gray;
        inlineStatus.Text       = message;
    }
}
