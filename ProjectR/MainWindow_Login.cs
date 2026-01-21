// MainWindow_Login.cs 

// Metoder og funktioner der bruges som er en del af pakker.
using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ProjectR.data;

namespace ProjectR;

// Vinduet er en del af class: Mainwindow - den er linket til vores mainwindow. 
// dette er det samme for vores andre cs filer. 
public partial class MainWindow : Window
{ 
    // denne logic kører, når user trykker på login
    // den tjekker om username og kodeord er korrekte
    // hvis login er korrekt, åbnes robot vinduet
    // admin users får adgang til ekstra faner
    // fejl bliver vist i loggen, så programmet ikke crasher
    public async void LoginButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var username = _loginUser.Text ?? "";
            var password = _loginPass.Text ?? "";

            if (!await _accountService.UsernameExistsAsync(username))
            {
                Log("Username does not exist.");
                return;
            }

            if (!await _accountService.CredentialsCorrectAsync(username, password))
            {
                Log("Password wrong.");
                return;
            }

            var account = await _accountService.GetAccountAsync(username);
            _currentUsername = account.Username;

            _logoutButton.IsVisible = true;

            _loginTab.IsVisible = false;
            ShowRobotTab();
            
            _usersTab.IsVisible = account.IsAdmin;
            _databaseTab.IsVisible = account.IsAdmin;

            Log($"{account.Username} logged in.");

            _loginUser.Text = "";
            _loginPass.Text = "";
        }
        catch (Exception ex)
        {
            Log("Login error: " + ex.Message);
        }
    }
    
// logik der kører når useren trykker på logout
// alle faner skjules, og loginfanen vises igen
// den aktuelle user fjernes
// handlingen skrives i loggen

    public void LogoutButton_OnClick(object? sender, RoutedEventArgs e)
    {
        HideAllTabs();
        ShowLoginTab();

        _logoutButton.IsVisible = false;
        _currentUsername = null;

        Log("Logged out.");
    }

    // kører når useren trykker på clear log
    // alt indhold i logfeltet fjernes

    public void ClearLogButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _logOutput.Text = "";
    }
    
// kører når admin opretter en ny user
// henter username, kodeord og adminvalg fra gui
// tjekker om username allerede findes i databasen
// try bruges til at køre koden sikkert, og catch fanger fejl så programmet ikke crasher
// fejl og succes bliver skrevet i loggen.

    public async void CreateUserButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var username = _createUser.Text ?? "";
            var password = _createPass.Text ?? "";
            var isAdmin = _createIsAdmin.IsChecked ?? false;

            if (await _accountService.UsernameExistsAsync(username))
            {
                Log($"Username {username} exists.");
                return;
            }

            await _accountService.NewAccountAsync(username, password, isAdmin);
            Log($"Created user: {username} (admin={isAdmin}).");
        }
        catch (Exception ex)
        {
            Log("CreateUser error: " + ex.Message);
        }
    }
}
