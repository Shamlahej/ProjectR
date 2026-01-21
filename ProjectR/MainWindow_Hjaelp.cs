// MainWindow_Hjælp.cs

// indeholder hjælpefunktioner til mainwindow. 
// den bruges til logning, visning af status og opdatering af gui
// funktionerne samler gentagen kode, så resten af programmet bliver mere overskueligt

// Metoder og funktioner der bruges som er en del af pakker. 
using System;
using Avalonia.Controls;

namespace ProjectR;

// Vinduet er en del af class: Mainwindow - den er linket til vores mainwindow. 
// dette er det samme for vores andre cs filer. 
public partial class MainWindow : Window
{
    // Log skriver en tekstbesked til log området i gui sammen med et tidspunkt.
    // det bruges til at vise hvad der sker i systemet, så den kan følge hændelser og forklare dem.
    
    private void Log(string message)
    {
        var now = DateTime.Now.ToString("yy-MM-dd HH:mm:ss");
        _logOutput.Text += $"{now} | {message}\n";
    }

    // RobotUiLog skriver en besked i robot loggen med tidspunkt.
    // den bruges til at vise robotrelaterede hændelser separat fra den almindelige log.

    private void RobotUiLog(string message)
    {
        _robotLogBox.Text += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
    }

    // SetResult opdaterer resultatfeltet i gui
    // hvis teksten er tom eller ugyldig, vises et standardtegn, så UI ikke ser tomt eller forkert ud.
    private void SetResult(string text)
    {
        _resultText.Text = string.IsNullOrWhiteSpace(text) ? "—" : text;
    }

    // viser batch target (antal cycles)
    private void SetTargetCyclesText(string text)
    {
        _targetCyclesText.Text = text;
    }

    // SetConveyorUi opdaterer gui(kører den eller ej) for transportbåndet baseret på om det kører eller er stoppet.
    // den gemmer også tilstanden internt, så knappen kan fungere korrekt som en start/stop toggle.
    private void SetConveyorUi(bool running)
    {
        _conveyorRunning = running;
        _conveyorStatusText.Text = running ? "Running" : "Stopped";
        _conveyorButton.Content = running ? "Stop Conveyor" : "Start Conveyor";
    }

    // EnsureRobotConnected tjekker om robotten er oprettet og forbundet.
    // hvis robotten ikke er forbundet, vises en besked, og metoden returnerer false, så handlingen stoppes.
    private bool EnsureRobotConnected()
    {
        if (_robot == null || !_robot.Connected)
        {
            RobotUiLog("Tryk Connect først.");
            return false;
        }
        return true;
    }
}