// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OsrsLauncher.Core.Session;

namespace OsrsLauncher.Harness.Views;

public partial class CharacterPickerView : UserControl
{
    private readonly Action<JagexCharacter> _onChosen;

    public CharacterPickerView(IReadOnlyList<JagexCharacter> characters, Action<JagexCharacter> onChosen)
    {
        _onChosen = onChosen;
        InitializeComponent();

        var list = this.FindControl<ItemsControl>("CharacterList")!;
        list.ItemsSource = characters;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnCharacterButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: JagexCharacter character })
        {
            _onChosen(character);
        }
    }
}
