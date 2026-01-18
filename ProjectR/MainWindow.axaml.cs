using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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
    private TextBox _correctCounterBox = null!;
    private TextBlock _statusText = null!;
    private TextBlock _statusHintText = null!;
    private TextBlock _conveyorStatusText = null!;
    private TextBox _robotLogBox = null!;

    private bool _sortingRunning;
    private bool _conveyorRunning;

    // --- STOP wiring ---
    // PC sætter DO6 -> (wired) -> robot læser DI6 i script
    private const int StopDoIndex = 6;

    // PC sætter DO5 -> (wired) -> robot læser DI7 (conveyor stop input)
    private const int ConveyorStopDoIndex = 5;

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
        _correctCounterBox = this.FindControl<TextBox>("CorrectCounterBox")!;
        _statusText = this.FindControl<TextBlock>("StatusText")!;
        _statusHintText = this.FindControl<TextBlock>("StatusHintText")!;
        _conveyorStatusText = this.FindControl<TextBlock>("ConveyorStatusText")!;
        _robotLogBox = this.FindControl<TextBox>("LogBox")!;

        // Start UI korrekt: conveyor er stopped -> knap skal sige "Start Conveyor"
        SetConveyorUi(false);
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
            var created = await _dbService.EnsureCreatedAndSeedAsync();

            if (created)
            {
                if (!await _accountService.UsernameExistsAsync("admin"))
                    await _accountService.NewAccountAsync("admin", "admin", true);

                if (!await _accountService.UsernameExistsAsync("user"))
                    await _accountService.NewAccountAsync("user", "user", false);

                Log("Database created + seeded (admin/user + counter).");
            }
            else
            {
                Log("Database exists.");
            }

            await RefreshRobotCounterBoxAsync();
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

    public async void ShowCounter_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var c = await _dbService.GetCounterAsync();
            Log($"Counter: Sorted={c.ItemsSortedTotal}, OK={c.ItemsOkTotal}, Reject={c.ItemsRejectedTotal}");
            _correctCounterBox.Text = c.ItemsSortedTotal.ToString();
        }
        catch (Exception ex)
        {
            Log("ShowCounter error: " + ex.Message);
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

            _robot = new Robot(ip);

            // (du må gerne beholde listener hvis du bruger COUNT events i dit robot.script)
            _robot.StartEventListener(5005);

            _robot.RobotEventReceived += (msg) =>
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    RobotUiLog("EVENT: " + msg);

                    if (msg == "COUNT")
                    {
                        await IncrementSortedTotalAsync();
                    }
                });
            };

            await _robot.ConnectAsync();

            // Sæt StopDo til false ved connect (så du ikke starter i stop-tilstand)
            _robot.SetStandardDigitalOut(StopDoIndex, false);

            // Conveyor OFF ved connect + sync UI
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

    // Send robot.script (bruges af Start Sorting)
    private bool SendRobotScriptInternal()
    {
        if (_robot == null || !_robot.Connected)
        {
            Log("Tryk Connect først.");
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
            Log("Fandt ikke robot.script. Læg den i samme mappe som ProjectR.csproj.");
            RobotUiLog("Fandt ikke robot.script. Læg den i samme mappe som ProjectR.csproj.");
            return false;
        }

        _robot.SendUrscriptFile(scriptPath);
        Log("robot.script sendt fra: " + scriptPath);
        RobotUiLog("robot.script sendt fra: " + scriptPath);
        return true;
    }

    // ---------------- ROBOT GUI ----------------

    private void SetConveyorUi(bool running)
    {
        _conveyorRunning = running;
        _conveyorStatusText.Text = running ? "Running" : "Stopped";
        _conveyorButton.Content = running ? "Stop Conveyor" : "Start Conveyor";
    }

    public async void StartButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_robot == null || !_robot.Connected)
            {
                RobotUiLog("Tryk Connect først.");
                return;
            }

            // Nulstil stop-flag: DO6 = False
            _robot.SetStandardDigitalOut(StopDoIndex, false);

            // Nulstil conveyor stop-flag: DO5 = False
            _robot.SetStandardDigitalOut(ConveyorStopDoIndex, false);

            if (!SendRobotScriptInternal())
                return;

            // Sorting starter conveyor med det samme -> opdater UI
            SetConveyorUi(true);

            await RefreshRobotCounterBoxAsync();

            _sortingRunning = true;
            _startButton.IsEnabled = false;
            _stopButton.IsEnabled = true;

            _statusText.Text = "Running";
            _statusHintText.Text = "Sorting in progress...";
            RobotUiLog("Started sorting (script sent).");
        }
        catch (Exception ex)
        {
            RobotUiLog("Start error: " + ex.Message);
        }
    }

    public void StopButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _sortingRunning = false;

        // Pæn stop: sæt DO6 = True (robot.script læser DI6 og stopper efter cyklus)
        try
        {
            if (_robot != null && _robot.Connected)
            {
                _robot.SetStandardDigitalOut(StopDoIndex, true);
                RobotUiLog($"Stop requested (DO{StopDoIndex}=ON). Will stop after current cycle.");
            }
            else
            {
                RobotUiLog("Stop pressed, but robot not connected.");
            }
        }
        catch (Exception ex)
        {
            RobotUiLog("Stop error: " + ex.Message);
        }

        _startButton.IsEnabled = true;
        _stopButton.IsEnabled = false;

        _statusText.Text = "Stopping...";
        _statusHintText.Text = "Finishing current cycle";
    }

    public async void ResetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var c = await _dbService.GetCounterAsync();
            c.ItemsSortedTotal = 0;
            c.ItemsOkTotal = 0;
            c.ItemsRejectedTotal = 0;
            await _db.SaveChangesAsync();

            _correctCounterBox.Text = "0";
            RobotUiLog("Counter reset.");
            Log("Counter reset (DB).");
        }
        catch (Exception ex)
        {
            RobotUiLog("Reset error: " + ex.Message);
        }
    }

    public void EmergencyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _sortingRunning = false;

        try
        {
            // Stop conveyor + stop-flags
            _robot?.SetStandardDigitalOut(7, false);
            _robot?.SetStandardDigitalOut(StopDoIndex, true);
            _robot?.SetStandardDigitalOut(ConveyorStopDoIndex, true);

            // HÅRDT stop NU
            _robot?.EmergencyStop();
        }
        catch (Exception ex)
        {
            RobotUiLog("Emergency error: " + ex.Message);
        }

        // UI skal altid matche: conveyor stoppet
        SetConveyorUi(false);

        _startButton.IsEnabled = true;
        _stopButton.IsEnabled = false;

        _statusText.Text = "EMERGENCY STOP";
        _statusHintText.Text = "System halted";

        RobotUiLog("!!! EMERGENCY STOP !!!");
        Log("Emergency stop activated.");
    }

    public void ConveyorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_robot == null || !_robot.Connected)
            {
                RobotUiLog("Conveyor pressed, but robot not connected.");
                return;
            }

            // Toggle kun conveyor (DO7) – påvirker ikke robot stop/start
            var newState = !_conveyorRunning;

            _robot.SetStandardDigitalOut(7, newState);
            SetConveyorUi(newState);

            RobotUiLog(newState
                ? "Conveyor started (DO7=ON)."
                : "Conveyor stopped (DO7=OFF).");
        }
        catch (Exception ex)
        {
            RobotUiLog("Conveyor error: " + ex.Message);
        }
    }

    // ---- COUNTER: TOTAL SORTED ----

    private async Task IncrementSortedTotalAsync()
    {
        var c = await _dbService.GetCounterAsync();
        c.ItemsSortedTotal += 1;
        await _db.SaveChangesAsync();

        _correctCounterBox.Text = c.ItemsSortedTotal.ToString();
    }

    private async Task RefreshRobotCounterBoxAsync()
    {
        var c = await _dbService.GetCounterAsync();
        _correctCounterBox.Text = c.ItemsSortedTotal.ToString();
    }
}
