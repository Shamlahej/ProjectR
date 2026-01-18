using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using ProjectR.data;

namespace ProjectR;

public partial class MainWindow : Window
{
    private AppDbContext _db = null!;
    private DbService _dbService = null!;
    private AccountService _accountService = null!;
    private Robot? _robot;

    // Tabs + login/admin
    private TabControl _tabControl = null!;
    private TabItem _loginTab = null!;
    private TabItem _robotTab = null!;
    private TabItem _usersTab = null!;
    private TabItem _databaseTab = null!;
    private Button _logoutButton = null!;
    private TextBlock _logOutput = null!;
    private TextBox _loginUser = null!;
    private TextBox _loginPass = null!;
    private TextBox _createUser = null!;
    private TextBox _createPass = null!;
    private CheckBox _createIsAdmin = null!;

    // IP/connect
    private TextBox _ipBox = null!;

    // Robot GUI
    private Button _startButton = null!;
    private Button _stopButton = null!;
    private Button _resetButton = null!;
    private Button _conveyorButton = null!;
    private TextBlock _statusText = null!;
    private TextBlock _statusHintText = null!;
    private TextBlock _conveyorStatusText = null!;
    private TextBox _robotLogBox = null!;

    // Batch UI (matcher XAML)
    private TextBox _batchInput = null!;
    private Button _runBatchButton = null!;
    private TextBlock _resultText = null!;
    private TextBlock _targetCyclesText = null!;

    private bool _sortingRunning;
    private bool _conveyorRunning;

    private bool _batchActive;
    private int _batchTarget;

    private string? _currentUsername;

    // STOP wiring (PC DO6 -> robot DI6)
    private const int StopDoIndex = 6;

    public MainWindow()
    {
        InitializeComponent();
        WireControls();

        foreach (var item in _tabControl.Items)
            if (item is TabItem t) t.IsVisible = false;

        _loginTab.IsVisible = true;
        _loginTab.IsSelected = true;

        InitServices();
        Loaded += async (_, __) => await SafeInitDbAsync();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void WireControls()
    {
        _tabControl = this.FindControl<TabControl>("TabControl")!;
        _loginTab = this.FindControl<TabItem>("LoginTab")!;
        _robotTab = this.FindControl<TabItem>("RobotTab")!;
        _usersTab = this.FindControl<TabItem>("UsersTab")!;
        _databaseTab = this.FindControl<TabItem>("DatabaseTab")!;
        _logoutButton = this.FindControl<Button>("LogoutButton")!;
        _logOutput = this.FindControl<TextBlock>("LogOutput")!;

        _ipBox = this.FindControl<TextBox>("IpAddress")!;

        _loginUser = this.FindControl<TextBox>("LoginUsername")!;
        _loginPass = this.FindControl<TextBox>("LoginPassword")!;
        _createUser = this.FindControl<TextBox>("CreateUserUsername")!;
        _createPass = this.FindControl<TextBox>("CreateUserPassword")!;
        _createIsAdmin = this.FindControl<CheckBox>("CreateUserIsAdmin")!;

        _startButton = this.FindControl<Button>("StartButton")!;
        _stopButton = this.FindControl<Button>("StopButton")!;
        _resetButton = this.FindControl<Button>("ResetButton")!;
        _conveyorButton = this.FindControl<Button>("ConveyorButton")!;
        _statusText = this.FindControl<TextBlock>("StatusText")!;
        _statusHintText = this.FindControl<TextBlock>("StatusHintText")!;
        _conveyorStatusText = this.FindControl<TextBlock>("ConveyorStatusText")!;
        _robotLogBox = this.FindControl<TextBox>("LogBox")!;

        // Batch panel controls (XAML-navne)
        _batchInput = this.FindControl<TextBox>("BatchCountBox")!;
        _runBatchButton = this.FindControl<Button>("RunBatchButton")!;
        _resultText = this.FindControl<TextBlock>("BatchResultText")!;
        _targetCyclesText = this.FindControl<TextBlock>("TargetCyclesText")!;

        SetConveyorUi(false);
        SetResult("—");
        SetTargetCyclesText("");
    }

    private void InitServices()
    {
        _db?.Dispose();
        _db = new AppDbContext();
        _dbService = new DbService(_db);
        _accountService = new AccountService(_db, new PasswordHasher());
    }

    private async Task SafeInitDbAsync()
    {
        try
        {
            await _db.Database.EnsureCreatedAsync();

            if (!await _accountService.UsernameExistsAsync("admin"))
            {
                await _accountService.NewAccountAsync("admin", "admin", true);
                Log("Seeded missing admin/admin.");
            }

            if (!await _accountService.UsernameExistsAsync("user"))
            {
                await _accountService.NewAccountAsync("user", "user", false);
                Log("Seeded missing user/user.");
            }

            Log("Database ready.");
        }
        catch (Exception ex)
        {
            Log("DB init CRASH: " + ex.Message);
            Log(ex.ToString());
        }
    }

    private void Log(string s)
    {
        var now = DateTime.Now.ToString("yy-MM-dd HH:mm:ss");
        _logOutput.Text += $"{now} | {s}\n";
    }

    private void RobotUiLog(string msg)
    {
        _robotLogBox.Text += $"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}";
    }

    private void SetResult(string s) => _resultText.Text = string.IsNullOrWhiteSpace(s) ? "—" : s;

    private void SetTargetCyclesText(string s) => _targetCyclesText.Text = s;

    // ---------------- LOGIN ----------------

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
            _robotTab.IsVisible = true;
            _robotTab.IsSelected = true;

            if (account.IsAdmin)
            {
                _usersTab.IsVisible = true;
                _databaseTab.IsVisible = true;
            }
            else
            {
                _usersTab.IsVisible = false;
                _databaseTab.IsVisible = false;
            }

            Log($"{account.Username} logged in.");
            _loginUser.Text = "";
            _loginPass.Text = "";
        }
        catch (Exception ex)
        {
            Log("Login error: " + ex.Message);
        }
    }

    public void LogoutButton_OnClick(object? sender, RoutedEventArgs e)
    {
        foreach (var item in _tabControl.Items)
            if (item is TabItem t) t.IsVisible = false;

        _loginTab.IsVisible = true;
        _loginTab.IsSelected = true;
        _logoutButton.IsVisible = false;

        _currentUsername = null;

        Log("Logged out.");
    }

    public void ClearLogButton_OnClick(object? sender, RoutedEventArgs e) => _logOutput.Text = "";

    // ---------------- USERS (ADMIN) ----------------

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

    // ---------------- DATABASE (ADMIN) ----------------

    public async void RecreateDatabaseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await _db.Database.EnsureDeletedAsync();
            InitServices();
            await SafeInitDbAsync();
            Log("Database recreated.");
        }
        catch (Exception ex)
        {
            Log("Recreate DB error: " + ex.Message);
        }
    }

    public async void ShowRunsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Vis de nyeste 50 runs i log
            var runs = await _db.SortingRuns
                .OrderByDescending(r => r.EndedAt)
                .Take(50)
                .ToListAsync();

            Log("---- Latest runs ----");
            if (runs.Count == 0)
            {
                Log("(no runs saved yet)");
            }
            else
            {
                foreach (var r in runs)
                    Log($"{r.EndedAt:yy-MM-dd HH:mm:ss} | ItemsCounted={r.ItemsCounted} | Username={r.Username}");
            }
            Log("---------------------");
        }
        catch (Exception ex)
        {
            Log("Show runs error: " + ex.Message);
        }
    }

    // ---------------- ROBOT CONNECT ----------------

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

            _robot = new Robot(ip, dashboardPort: 29999, urscriptPort: 30003);
            await _robot.ConnectAsync();

            _robot.SetStandardDigitalOut(StopDoIndex, false);
            _robot.SetStandardDigitalOut(7, false);
            SetConveyorUi(false);

            Log($"Connected to {ip}. Dashboard=29999, URScript=30003.");
            RobotUiLog($"Connected to {ip}. Dashboard=29999, URScript=30003.");
        }
        catch (Exception ex)
        {
            Log("Connect error: " + ex.Message);
            RobotUiLog("Connect error: " + ex.Message);
        }
    }

    // Send robot.script med MAX_CYCLES injected
    private bool SendRobotScriptWithCycles(int maxCycles)
    {
        if (_robot == null || !_robot.Connected)
        {
            RobotUiLog("Tryk Connect først.");
            return false;
        }

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

        _robot.SendUrscript(program);
        RobotUiLog($"robot.script sendt (max_cycles={maxCycles}).");
        return true;
    }

    // ---------------- UI helpers ----------------

    private void SetConveyorUi(bool running)
    {
        _conveyorRunning = running;
        _conveyorStatusText.Text = running ? "Running" : "Stopped";
        _conveyorButton.Content = running ? "Stop Conveyor" : "Start Conveyor";
    }

    // ---------------- MANUAL ----------------

    public void StartButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_robot == null || !_robot.Connected)
            {
                RobotUiLog("Tryk Connect først.");
                return;
            }

            _batchActive = false;
            _batchTarget = 0;
            SetTargetCyclesText("");

            _robot.SetStandardDigitalOut(StopDoIndex, false);

            // Manual mode => max_cycles = 0
            if (!SendRobotScriptWithCycles(0))
                return;

            SetConveyorUi(true);

            _sortingRunning = true;
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

    public void StopButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            _sortingRunning = false;
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

    // Clear Result
    public void ResetButton_OnClick(object? sender, RoutedEventArgs e) => SetResult("—");

    // ---------------- BATCH ----------------

    public void RunBatchButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_robot == null || !_robot.Connected)
            {
                RobotUiLog("Tryk Connect først.");
                return;
            }

            var txt = _batchInput.Text?.Trim() ?? "";
            if (!int.TryParse(txt, out var n) || n <= 0)
            {
                SetResult("Write a valid number (>=1).");
                return;
            }

            _batchActive = true;
            _batchTarget = n;
            SetTargetCyclesText($"Target cycles: {n}");

            _robot.SetStandardDigitalOut(StopDoIndex, false);

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

            // Monitor: vent på program stopper, så log + DB + sikkerhed
            _ = Task.Run(async () =>
            {
                try
                {
                    // kræver Robot.cs har IsProgramRunning()
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
                        _sortingRunning = false;

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

    private async Task RunSecurityTimeAndSaveAsync(int targetCycles)
    {
        // Security start
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

        // Security end
        SetResult("Security time finished.");
        RobotUiLog("Security time finished.");
        Log("Security time finished.");

        // Save to DB (samler alt i Username for at undgå schema changes)
        var meta =
            $"{_currentUsername ?? "unknown"} | batchCycles={targetCycles} | securityStartUtc={startedUtc:O} | securityEndUtc={endedUtc:O}";

        await _dbService.SaveRunAsync(itemsCounted: targetCycles, username: meta);

        SetResult($"Batch finished: {targetCycles} components faulty. (saved)");
        RobotUiLog($"Batch finished: {targetCycles} components faulty. (saved)");
        Log($"Saved run: {meta}");
    }

    // ---------------- EMERGENCY ----------------

    public void EmergencyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _sortingRunning = false;
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
    }

    // ---------------- Conveyor manual toggle ----------------

    public void ConveyorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_robot == null || !_robot.Connected)
            {
                RobotUiLog("Conveyor pressed, but robot not connected.");
                return;
            }

            var newState = !_conveyorRunning;
            _robot.SetStandardDigitalOut(7, newState);
            SetConveyorUi(newState);
        }
        catch (Exception ex)
        {
            RobotUiLog("Conveyor error: " + ex.Message);
        }
    }
}
