//MainWindow_Opstart.cs

// Metoder og funktioner der bruges som er en del af pakker.
using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Microsoft.EntityFrameworkCore;
using ProjectR.data;

namespace ProjectR;

// Vinduet er en del af class: Mainwindow - den er linket til vores mainwindow. 
// dette er det samme for vores andre cs filer. 
public partial class MainWindow : Window
{

    // de her variabler holder styr på vigtige ting i programmet
    // de gør, at vi kan bruge databasen, login og robotten igen og igen
    // uden dem skulle vi starte forfra hver gang der trykkes på en knap
    // de bruges af mange forskellige dele af vinduet, derfor ligger de samlet øverst i filen


    // DbContext er forbindelsen til databasen via Entity Framework.
    private AppDbContext _db = null!;
    private DbService _dbService = null!;

    // Service der håndterer login og oprettelse af users.
    private AccountService _accountService = null!;

    // Robot forbindelsen (først sat når man trykker Connect).
    private Robot? _robot;

    // Gemmer hvem der er logget ind (bruges i logs og database).
    private string? _currentUsername;

   
    // GUI kontrolpanel: handles
    // de her variabler peger på tingene i vinduet, som knapper, tekstfelter og faner
    // de gør, at koden kan se og styre det useren klikker på og skriver ind
    // vi bruger dem til at læse input fra useren og vise beskeder og status
    // de gør det muligt at slå knapper til og fra og vise eller skjule faner
    // uden dem kunne vi ikke få koden og GUI (brugerfladen) til at arbejde sammen

    // Variablerne står til at være = null.
    // dette er fordi at variabler først er sat når vinduet starter
    // xaml bliver loaded først, og derefter finder vi knapper og felter med FindControl
    // derfor kan de ikke få en værdi med det samme, når klassen bliver lavet
    // = null betyder bare "ingen værdi endnu", ikke at noget mangler
    // når programmet kører, bliver de alle sat korrekt og brugt som normalt

    
    // Tabs + login og om man er admin eller ej.
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

    // connect til robot via ip. IP boksen.
    private TextBox _ipBox = null!;

    // Robot fanen på GUI
    private Button _startButton = null!;
    private Button _stopButton = null!;
    private Button _resetButton = null!;
    private Button _conveyorButton = null!;
    private TextBlock _statusText = null!;
    private TextBlock _statusHintText = null!;
    private TextBlock _conveyorStatusText = null!;
    private TextBox _robotLogBox = null!;

    // Batchdelen på robotfanen
    private TextBox _batchInput = null!;
    private Button _runBatchButton = null!;
    private TextBlock _resultText = null!;
    private TextBlock _targetCyclesText = null!;

    // State (systemets tilstand)
    // koderne er variabler og konstanter der siger noget om tilstand ift. projektet.
    // de bruges til at huske hvad systemet laver lige nu
    // booleans bruges til at holde styr på true/false tilstande som kører/stopper
    // integer bruges til at gemme et tal fra user (batch antal)
    // konstanten bruges til at beskrive den fysiske wiring mellem pc og robot (D06 -DI6)

// conveyorRunning holder styr på om transportbåndet kører, så start/stop knappen virker korrekt
// batchActive fortæller om systemet er i gang med en batch kørsel
// batchTarget gemmer hvor mange cyklusser batchen skal køre
// StopDoIndex er den digitale udgang på pc’en, som er forbundet til robotten
// når vi sætter denne udgang til true, stopper robotten via den fysiske wiring

    private bool _conveyorRunning; 
    private bool _batchActive;     
    private int _batchTarget;      
    private const int StopDoIndex = 6;

    // denne kode kører automatisk, når programmet starter
    // først bliver vinduets udseende indlæst fra xaml
    // derefter kobles knapper og felter sammen med koden
    // databasen gøres klar, og login vises som det første
    // når programmet lukkes, rydder vi op i forbindelserne

    public MainWindow()
    {
        // Loader XAML så vinduets layout og knapper(controls) findes
        InitializeComponent();

        // Finder alle controls fra XAML og gemmer dem i felter
        WireControls();

        // GUI starter og login tab skal vises
        HideAllTabs();
        ShowLoginTab();

        // Opretter database
        InitServices();

        // Når GUI er færdig loaded, klargør vi DB (tabeller + seed users)
        Loaded += async (_, __) => await SafeInitDbAsync();

        // Når vinduet lukker, rydder vi op (DB forbindelsen)
        Closed += (_, __) => Cleanup();
    }
    
    // wiring koder
    // FindControl() bruges så codebehind kan bruge XAML controls
    // denne kode bruges til at forbinde GUI (xaml) med c# koden
    // hver linje finder en knap, tekstboks eller fane ud fra dens navn i xaml
    // på den måde kan vi læse hvad user skriver og reagere på klik
    // uden denne kode kan programmet ikke styre eller ændre noget i vinduet
    // xaml viser tingene, og denne kode gør dem brugbare i programmet

    private void WireControls()
    {
        // hjælpefunktion så FindControllinjer bliver kortere og mere læselige
        T Find<T>(string name) where T : Control => this.FindControl<T>(name)!;

        _tabControl = Find<TabControl>("TabControl");
        _loginTab = Find<TabItem>("LoginTab");
        _robotTab = Find<TabItem>("RobotTab");
        _usersTab = Find<TabItem>("UsersTab");
        _databaseTab = Find<TabItem>("DatabaseTab");
        _logoutButton = Find<Button>("LogoutButton");
        _logOutput = Find<TextBlock>("LogOutput");

        _ipBox = Find<TextBox>("IpAddress");

        _loginUser = Find<TextBox>("LoginUsername");
        _loginPass = Find<TextBox>("LoginPassword");
        _createUser = Find<TextBox>("CreateUserUsername");
        _createPass = Find<TextBox>("CreateUserPassword");
        _createIsAdmin = Find<CheckBox>("CreateUserIsAdmin");

        _startButton = Find<Button>("StartButton");
        _stopButton = Find<Button>("StopButton");
        _resetButton = Find<Button>("ResetButton");
        _conveyorButton = Find<Button>("ConveyorButton");
        _statusText = Find<TextBlock>("StatusText");
        _statusHintText = Find<TextBlock>("StatusHintText");
        _conveyorStatusText = Find<TextBlock>("ConveyorStatusText");
        _robotLogBox = Find<TextBox>("LogBox");

        _batchInput = Find<TextBox>("BatchCountBox");
        _runBatchButton = Find<Button>("RunBatchButton");
        _resultText = Find<TextBlock>("BatchResultText");
        _targetCyclesText = Find<TextBlock>("TargetCyclesText");

        // GUI starttilstand
        // her sætter vi gui i en fast starttilstand
        // transportbåndet vises som stoppet
        // resultatfeltet nulstilles, så der ikke står gamle data
        // batchteksten ryddes, så user starter fra en ren tilstand

        //Setconveyer hedder Ui, fordi vi tidligere havde lavet en funktion med GUI hvor den drillede, vi blev derfor nød til at lave nye nogle gange.
        SetConveyorUi(false);
        SetResult("—");
        SetTargetCyclesText("");
    }
// Initservices (database) funktioner
// her opretter vi de services programmet bruger til database og login
// hvis der allerede findes en database forbindelse, lukkes den først
// derefter oprettes en ny forbindelse til databasen
// services samler funktioner, så koden bliver mere overskuelig
// passwordhasher bruges for at gemme kodeord sikkert

    private void InitServices()
    {
        // Luk tidligere DbContext hvis vi genopretter DB
        _db?.Dispose();

        _db = new AppDbContext();
        _dbService = new DbService(_db);

        // PasswordHasher bruges for sikkerhed (hash + salt)
        _accountService = new AccountService(_db, new PasswordHasher());
    }

    // denne kode sørger for at databasen er klar, når programmet starter
    // hvis databasen eller tabellerne ikke findes, bliver de oprettet automatisk
    // der oprettes fast admin, så systemet altid kan bruges
    // try bruges for at køre koden sikkert, og catch fanger fejl hvis noget går galt
    // fejl og status bliver skrevet i loggen i stedet for at programmet crasher

    private async Task SafeInitDbAsync()
    {
        try
        {
            // Opretter database og tabeller hvis de ikke findes
            await _db.Database.EnsureCreatedAsync();

            // vi bruger seed for at sikre, at databasen altid har nogle users fra start
            // det gør at systemet kan testes og bruges med det samme
            // uden seed kunne databasen være tom, og man kunne ikke logge ind
            
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

    // denne kode kører, når programmet lukkes
    // den lukker forbindelsen til databasen
    // det er god praksis for at undgå åbne forbindelser
    // try og catch bruges, så programmet lukker pænt uden fejl

    private void Cleanup()
    {
        try { _db?.Dispose(); } catch { }
    }
    
    // Tab og flowet i hvordan vi alternater imellem dem:
    // de her funktioner styrer hvilke faner der vises i GUI
    // først kan alle faner skjules, så vi starter fra en ren tilstand
    // login fanen vises, når useren ikke er logget ind
    // robot fanen vises, når useren er logget ind
    // dette har vi gjort fordi det giver et simpelt og overskueligt flow i Gui.

    // foreach bruges til at gennemgå alle elementer i en samling
    // her gennemgår vi alle faner i tabkontrollen
    // for hver fane tjekker vi om det er en TabItem
    // hvis det er det, skjuler vi fanen
    // på den måde kan vi skjule alle faner med få linjer kode
    private void HideAllTabs()
    {
        foreach (var item in _tabControl.Items)
            if (item is TabItem t) t.IsVisible = false;
    }

    private void ShowLoginTab()
    {
        _loginTab.IsVisible = true;
        _loginTab.IsSelected = true;
    }

    private void ShowRobotTab()
    {
        _robotTab.IsVisible = true;
        _robotTab.IsSelected = true;
    }
}
