// MainWindow_Robot.cs

// Metoder og funktioner der bruges som er en del af pakker.
using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ProjectR;

// Vinduet er en del af class: Mainwindow - den er linket til vores mainwindow. 
// dette er det samme for vores andre cs filer. 
public partial class MainWindow : Window
{
    // Connecting the robot
    // kører når User trykker på connect til robotten
    // først hentes ipadressen fra tekstfeltet og tjekkes for fejl
    // if tekstfelt står blankt, får vi besked på ip mangler.
    // der oprettes en forbindelse til robotten via de angivne porte
    // robotten sættes i en sikker starttilstand, så intet kører ved opstart
    // try og catch bruges, så fejl vises i loggen uden at programmet crasher
    // hvis noget går galt undervejs, vises fejlen i loggen i stedet for at programmet stopper
    
    public async void ConnectButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var ip = _ipBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(ip))
            {
                Log("IP mangler.");
                RobotUiLog("IP mangler.");
                return;
            }

            _robot = new Robot(ip, dashboardPort: 29999, urscriptPort: 30002);
            await _robot.ConnectAsync();

            _robot.SetStandardDigitalOut(StopDoIndex, false);
            _robot.SetStandardDigitalOut(7, false);
            SetConveyorUi(false);

            Log($"Connected to {ip}. Dashboard=29999, URScript=30002.");
            RobotUiLog($"Connected to {ip}. Dashboard=29999, URScript=30002.");
        }
        catch (Exception ex)
        {
            Log("Connect error: " + ex.Message);
            RobotUiLog("Connect error: " + ex.Message);
        }
    }

    // Sender robot.script med MAX_CYCLES (samme opførsel som før)
    // max cycles er det antal gentagelser robotten skal køre
    // det bruges til at styre hvor mange gange processen må gentage sig
    // i manual mode er max cycles sat til 0, så robotten kører frit
    // i batch mode bestemmer max cycles hvornår robotten stopper

    // først tjekker programmet om robotten er forbundet
    // hvis robotten ikke er forbundet, stopper koden med det samme
    // derefter leder programmet efter filen robot.script forskellige steder i projektet
    // den første sti hvor filen findes, bliver valgt
    // hvis filen ikke findes nogen steder, vises en fejl i robotloggen
    // når filen er fundet, læses indholdet som tekst
    // {{MAX_CYCLES}} i filen bliver erstattet med det antal cyklusser user har valgt
    // det færdige program sendes til robotten
    // til sidst returneres true for at vise at det lykkedes

    private bool SendRobotScriptWithCycles(int maxCycles)
    {
        if (!EnsureRobotConnected())
            return false;

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "robot.script"),
            Path.Combine(Directory.GetCurrentDirectory(), "robot.script"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "robot.script"),
        };

        string? scriptPath = null;

        foreach (var p in candidates)
        {
            var full = Path.GetFullPath(p);
            if (File.Exists(full))
            {
                scriptPath = full;
                break;
            }
        }

        if (scriptPath == null)
        {
            RobotUiLog("Fandt ikke robot.script. Læg den i samme mappe som ProjectR.csproj.");
            return false;
        }

        var raw = File.ReadAllText(scriptPath);
        var program = raw.Replace("{{MAX_CYCLES}}", maxCycles.ToString());

        _robot!.SendUrscript(program);
        RobotUiLog($"robot.script sendt (max_cycles={maxCycles}).");
        return true;
    }

    // Manuel styring
// når user trykker start, tjekker programmet først om robotten er forbundet
// batch bliver slået fra, og mål for cyklusser bliver nulstillet, fordi det er manuel kørsel
// stopsignalet sættes til false, så robotten ikke bliver holdt stoppet
// robot.script sendes med max cycles = 0, så robotten kører i manuel mode
// gui opdateres så conveyor vises som kørende, knapper slås til/fra, og status/resultat vises
// hvis noget går galt, fanges fejlen og skrives i robotloggen

    public void StartButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!EnsureRobotConnected())
                return;

            _batchActive = false;
            _batchTarget = 0;
            SetTargetCyclesText("");

            _robot!.SetStandardDigitalOut(StopDoIndex, false);

            // Manual mode => max_cycles = 0
            if (!SendRobotScriptWithCycles(0))
                return;

            SetConveyorUi(true);

            _startButton.IsEnabled = false;
            _stopButton.IsEnabled = true;
            _runBatchButton.IsEnabled = true;

            _statusText.Text = "Running";
            _statusHintText.Text = "Manual mode";
            SetResult("Running manual...");
        }
        catch (Exception ex)
        {
            RobotUiLog("Start error: " + ex.Message);
        }
    }
// når user trykker stop, afsluttes sortering
// stopsignalet sendes til robotten via den digitale udgang
// gui opdateres så startknappen aktiveres igen og stopknappen slås fra
// status og resultat opdateres, så det er tydeligt at robotten stopper
// hvis der opstår en fejl, skrives den i robotloggen

// når user trykker reset, ryddes resultatfeltet
// dette bruges til at fjerne gamle beskeder før næste kørsel

    public void StopButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            _batchActive = false;

            if (_robot != null && _robot.Connected)
                _robot.SetStandardDigitalOut(StopDoIndex, true);

            _startButton.IsEnabled = true;
            _stopButton.IsEnabled = false;

            _statusText.Text = "Stopping...";
            _statusHintText.Text = "Robot will halt";
            SetResult("Stopping...");
        }
        catch (Exception ex)
        {
            RobotUiLog("Stop error: " + ex.Message);
        }
    }

    // fjerne results (clear)
    public void ResetButton_OnClick(object? sender, RoutedEventArgs e)
        => SetResult("—");

    // Batch mode
// når user trykker run batch, tjekker programmet først om robotten er forbundet
// batchantallet læses fra inputfeltet og kontrolleres, så det er et gyldigt tal
// batchtilstand aktiveres, og målet for antal cyklusser gemmes og vises i gui
// robot.script sendes med det valgte antal cycles, og transportbåndet startes
// gui opdateres så det tydeligt vises, at batchkørslen er i gang
// programmet overvåger robotten i baggrunden og venter på, at batchen er færdig
// når batchen er færdig, køres securitytid, data gemmes i databasen, og gui nulstilles
// hvis der opstår fejl undervejs, bliver de fanget og vist i robotloggen

    public void RunBatchButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!EnsureRobotConnected())
                return;

            var txt = _batchInput.Text?.Trim() ?? "";
            if (!int.TryParse(txt, out var n) || n <= 0)
            {
                SetResult("Write a valid number (>=1).");
                return;
            }

            _batchActive = true;
            _batchTarget = n;
            SetTargetCyclesText($"Target cycles: {n}");

            _robot!.SetStandardDigitalOut(StopDoIndex, false);

            if (!SendRobotScriptWithCycles(n))
                return;

            SetConveyorUi(true);

            _startButton.IsEnabled = false;
            _stopButton.IsEnabled = true;
            _runBatchButton.IsEnabled = false;

            _statusText.Text = "Running (batch)";
            _statusHintText.Text = "Batch in progress";
            SetResult("Running batch...");

            RobotUiLog($"Started batch for {n} cycles.");
            
            _ = Task.Run(async () =>
            {
                try
                {
                    while (_robot != null && _robot.Connected && _robot.IsProgramRunning())
                        await Task.Delay(250);

                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        if (!_batchActive)
                        {
                            _runBatchButton.IsEnabled = true;
                            return;
                        }

                        await RunSecurityTimeAndSaveAsync(_batchTarget);

                        _batchActive = false;

                        _startButton.IsEnabled = true;
                        _stopButton.IsEnabled = false;
                        _runBatchButton.IsEnabled = true;

                        _statusText.Text = "Ready";
                        _statusHintText.Text = "Waiting";
                        SetTargetCyclesText("");
                    });
                }
                catch (Exception ex)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        RobotUiLog("Batch monitor error: " + ex.Message);
                        SetResult("Batch monitor error.");
                        _runBatchButton.IsEnabled = true;
                    });
                }
            });
        }
        catch (Exception ex)
        {
            RobotUiLog("RunBatch error: " + ex.Message);
        }
    }
    
// denne kode kører, når en batch er færdig
// systemet venter 60 sekunder som en sikkerhedsperiode
// imens bliver transportbåndet tændt og slukket igen
// oplysninger om tid og batch gemmes i databasen
// til sidst vises det i gui og i loggen, at batchen er afsluttet

// var bruges for at lade c# selv finde ud af hvilken type variablen er
// det gør koden kortere og nemmere at læse
// try bruges til at køre kode, der kan give fejl, uden at programmet crasher
// await bruges til at vente på noget, der tager tid, fx en pause eller databasekald
// imens await venter, fryser gui’en ikke

    private async Task RunSecurityTimeAndSaveAsync(int targetCycles)
    {
        SetResult("Security time started...");
        RobotUiLog("Security time started (60s).");
        Log("Security time started (60s).");

        try { _robot?.SetStandardDigitalOut(7, true); } catch { }
        SetConveyorUi(true);

        var startedUtc = DateTime.UtcNow;
        await Task.Delay(TimeSpan.FromMinutes(1));
        var endedUtc = DateTime.UtcNow;

        try { _robot?.SetStandardDigitalOut(7, false); } catch { }
        SetConveyorUi(false);

        SetResult("Security time finished.");
        RobotUiLog("Security time finished.");
        Log("Security time finished.");

        var meta =
            $"{_currentUsername ?? "unknown"} | batchCycles={targetCycles} | securityStartUtc={startedUtc:O} | securityEndUtc={endedUtc:O}";

        await _dbService.SaveRunAsync(itemsCounted: targetCycles, username: meta);

        SetResult($"Batch finished: {targetCycles} components faulty. (saved)");
        RobotUiLog($"Batch finished: {targetCycles} components faulty. (saved)");
        Log($"Saved run: {meta}");
    }

    // EmergencyButton
    // kører når user trykker på emergency stop
    // batch stoppes med det samme, så systemet ikke fortsætter noget
    // der sendes stopsignal til robotten, og nødstop aktiveres
    // try og catch bruges, så fejl håndteres uden at programmet crasher
    // transportbåndet stoppes og gui nulstilles til sikker tilstand
    // status i gui opdateres tydeligt, så det kan ses at systemet er stoppet
    public void EmergencyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _batchActive = false;

        try
        {
            _robot?.SetStandardDigitalOut(7, false);
            _robot?.SetStandardDigitalOut(StopDoIndex, true);
            _robot?.EmergencyStop();
        }
        catch (Exception ex)
        {
            RobotUiLog("Emergency error: " + ex.Message);
        }

        SetConveyorUi(false);

        _startButton.IsEnabled = true;
        _stopButton.IsEnabled = false;
        _runBatchButton.IsEnabled = true;

        _statusText.Text = "EMERGENCY STOP";
        _statusHintText.Text = "System halted";
        SetResult("EMERGENCY STOP");
        SetTargetCyclesText("");

        // her gemmes information om emergency stop i databasen
        // username og tidspunktet for trykket bliver gemt
        // data samles i en tekst, så formatet er ens hver gang
        // der gemmes med en specialværdi, så det kan skelnes fra normale batchkørsler
        // hvis noget går galt, fanges fejlen og vises i loggen

        try
        {
            var who = _currentUsername ?? "unknown";
            var pressedUtc = DateTime.UtcNow;

            var meta = $"EMERGENCY| user={who} | pressedUtc={pressedUtc:O}";

            _ = _dbService.SaveRunAsync(itemsCounted: -1, username: meta);

            Log($"Emergency clicked by {who} (saved).");
            RobotUiLog($"Emergency clicked by {who} (saved).");
        }
        catch (Exception ex)
        {
            Log("Emergency DB log error: " + ex.Message);
        }
    }

    // Conveyerknappen 
    // kører når useren trykker på conveyorknappen
    // først tjekkes det om robotten er forbundet
    // transportbåndets tilstand skiftes mellem start og stop
    // signalet sendes til robotten, og gui opdateres så det passer
    // fejl fanges og vises i robotloggen uden at programmet crasher
    
    public void ConveyorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!EnsureRobotConnected())
            {
                RobotUiLog("Conveyor pressed, but robot not connected.");
                return;
            }

            var newState = !_conveyorRunning;
            _robot!.SetStandardDigitalOut(7, newState);
            SetConveyorUi(newState);
        }
        catch (Exception ex)
        {
            RobotUiLog("Conveyor error: " + ex.Message);
        }
    }
}
