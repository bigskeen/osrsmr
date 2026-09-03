using System.Windows.Media;

namespace OsrsMr.Core;

public class DataItem : System.ComponentModel.INotifyPropertyChanged
{
    private string _key = "";
    private string _value = "";

    public string Key { get => _key; set { if (_key != value) { _key = value; OnPropertyChanged(nameof(Key)); } } }
    public string Value { get => _value; set { if (_value != value) { _value = value; OnPropertyChanged(nameof(Value)); } } }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

public class NpcItem : System.ComponentModel.INotifyPropertyChanged
{
    private string _id = "";
    private string _name = "";
    private string _combatLevel = "-";
    private string _distance = "";
    private string _health = "";
    private string _category = "NPC";

    public string Id { get => _id; set { if (_id != value) { _id = value; OnPropertyChanged(nameof(Id)); } } }
    public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } } }
    public string CombatLevel { get => _combatLevel; set { if (_combatLevel != value) { _combatLevel = value; OnPropertyChanged(nameof(CombatLevel)); } } }
    public string Distance { get => _distance; set { if (_distance != value) { _distance = value; OnPropertyChanged(nameof(Distance)); } } }
    public string Health { get => _health; set { if (_health != value) { _health = value; OnPropertyChanged(nameof(Health)); } } }
    public string Category { get => _category; set { if (_category != value) { _category = value; OnPropertyChanged(nameof(Category)); } } }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

public class TreeItem : System.ComponentModel.INotifyPropertyChanged
{
    private string _id = "";
    private string _name = "";
    private string _distance = "";
    private string _location = "";
    private string _status = "Available";

    public string Id { get => _id; set { if (_id != value) { _id = value; OnPropertyChanged(nameof(Id)); } } }
    public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } } }
    public string Distance { get => _distance; set { if (_distance != value) { _distance = value; OnPropertyChanged(nameof(Distance)); } } }
    public string Location { get => _location; set { if (_location != value) { _location = value; OnPropertyChanged(nameof(Location)); } } }
    public string Status { get => _status; set { if (_status != value) { _status = value; OnPropertyChanged(nameof(Status)); } } }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

public class SceneObjectItem : System.ComponentModel.INotifyPropertyChanged
{
    private string _id = "";
    private string _name = "";
    private string _distance = "";
    private string _location = "";

    public string Id { get => _id; set { if (_id != value) { _id = value; OnPropertyChanged(nameof(Id)); } } }
    public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } } }
    public string Distance { get => _distance; set { if (_distance != value) { _distance = value; OnPropertyChanged(nameof(Distance)); } } }
    public string Location { get => _location; set { if (_location != value) { _location = value; OnPropertyChanged(nameof(Location)); } } }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

public class GroundItem : System.ComponentModel.INotifyPropertyChanged
{
    private string _id = "";
    private string _name = "";
    private string _quantity = "";
    private string _distance = "";
    private string _location = "";

    public string Id { get => _id; set { if (_id != value) { _id = value; OnPropertyChanged(nameof(Id)); } } }
    public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } } }
    public string Quantity { get => _quantity; set { if (_quantity != value) { _quantity = value; OnPropertyChanged(nameof(Quantity)); } } }
    public string Distance { get => _distance; set { if (_distance != value) { _distance = value; OnPropertyChanged(nameof(Distance)); } } }
    public string Location { get => _location; set { if (_location != value) { _location = value; OnPropertyChanged(nameof(Location)); } } }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

public class AttackingEnemyItem : System.ComponentModel.INotifyPropertyChanged
{
    private string _name = "";
    private string _combatLevel = "";
    private string _health = "100%";
    private string _distance = "0";
    private string _prayer = "None";
    private string _attackStyle = "Melee";

    public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } } }
    public string CombatLevel { get => _combatLevel; set { if (_combatLevel != value) { _combatLevel = value; OnPropertyChanged(nameof(CombatLevel)); } } }
    public string Health { get => _health; set { if (_health != value) { _health = value; OnPropertyChanged(nameof(Health)); } } }
    public string Distance { get => _distance; set { if (_distance != value) { _distance = value; OnPropertyChanged(nameof(Distance)); } } }
    public string Prayer { get => _prayer; set { if (_prayer != value) { _prayer = value; OnPropertyChanged(nameof(Prayer)); } } }
    public string AttackStyle { get => _attackStyle; set { if (_attackStyle != value) { _attackStyle = value; OnPropertyChanged(nameof(AttackStyle)); } } }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

public class ContainerItem : System.ComponentModel.INotifyPropertyChanged
{
    private string _id = "";
    private string _name = "";
    private string _quantity = "";

    public string Id { get => _id; set { if (_id != value) { _id = value; OnPropertyChanged(nameof(Id)); } } }
    public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } } }
    public string Quantity { get => _quantity; set { if (_quantity != value) { _quantity = value; OnPropertyChanged(nameof(Quantity)); } } }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

public class ShortcutItem : System.ComponentModel.INotifyPropertyChanged
{
    private string _id = "";
    private string _name = "";
    private string _reqLevel = "1";
    private string _distance = "";
    private string _location = "";

    public string Id { get => _id; set { if (_id != value) { _id = value; OnPropertyChanged(nameof(Id)); } } }
    public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } } }
    public string ReqLevel { get => _reqLevel; set { if (_reqLevel != value) { _reqLevel = value; OnPropertyChanged(nameof(ReqLevel)); } } }
    public string Distance { get => _distance; set { if (_distance != value) { _distance = value; OnPropertyChanged(nameof(Distance)); } } }
    public string Location { get => _location; set { if (_location != value) { _location = value; OnPropertyChanged(nameof(Location)); } } }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

public class AgilityObstacleItem : System.ComponentModel.INotifyPropertyChanged
{
    private string _id = "";
    private string _name = "";
    private string _course = "None";
    private string _distance = "";
    private string _location = "";

    public string Id { get => _id; set { if (_id != value) { _id = value; OnPropertyChanged(nameof(Id)); } } }
    public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } } }
    public string Course { get => _course; set { if (_course != value) { _course = value; OnPropertyChanged(nameof(Course)); } } }
    public string Distance { get => _distance; set { if (_distance != value) { _distance = value; OnPropertyChanged(nameof(Distance)); } } }
    public string Location { get => _location; set { if (_location != value) { _location = value; OnPropertyChanged(nameof(Location)); } } }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

public class FishingSpotItem : System.ComponentModel.INotifyPropertyChanged
{
    private string _id = "";
    private string _name = "";
    private string _spotType = "";
    private string _distance = "";
    private string _location = "";

    public string Id { get => _id; set { if (_id != value) { _id = value; OnPropertyChanged(nameof(Id)); } } }
    public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } } }
    public string SpotType { get => _spotType; set { if (_spotType != value) { _spotType = value; OnPropertyChanged(nameof(SpotType)); } } }
    public string Distance { get => _distance; set { if (_distance != value) { _distance = value; OnPropertyChanged(nameof(Distance)); } } }
    public string Location { get => _location; set { if (_location != value) { _location = value; OnPropertyChanged(nameof(Location)); } } }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

public class PlayerItem : System.ComponentModel.INotifyPropertyChanged
{
    private string _id = "";
    private string _name = "";
    private string _distance = "";
    private string _combatLevel = "";

    public string Id { get => _id; set { if (_id != value) { _id = value; OnPropertyChanged(nameof(Id)); } } }
    public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } } }
    public string Distance { get => _distance; set { if (_distance != value) { _distance = value; OnPropertyChanged(nameof(Distance)); } } }
    public string CombatLevel { get => _combatLevel; set { if (_combatLevel != value) { _combatLevel = value; OnPropertyChanged(nameof(CombatLevel)); } } }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

public class PrayerViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private static readonly Brush _activeBgBrush = CreateFrozen(Color.FromArgb(80, 0, 180, 216));
    private static readonly Brush _inactiveBgBrush = CreateFrozen(Color.FromRgb(37, 37, 38));
    private static readonly Brush _activeBorderBrush = CreateFrozen(Color.FromRgb(0, 229, 255));
    private static readonly Brush _inactiveBorderBrush = CreateFrozen(Color.FromRgb(63, 63, 70));
    private static readonly Brush _activeStatusBrush = CreateFrozen(Color.FromRgb(0, 255, 128));
    private static readonly Brush _inactiveStatusBrush = CreateFrozen(Color.FromRgb(90, 90, 90));
    private static readonly Brush _activeTextBrush = CreateFrozen(Color.FromRgb(255, 255, 255));
    private static readonly Brush _inactiveTextBrush = CreateFrozen(Color.FromRgb(160, 160, 160));

    private static SolidColorBrush CreateFrozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private string _name = "";
    private bool _isActive;

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(BackgroundBrush));
                OnPropertyChanged(nameof(BorderBrush));
                OnPropertyChanged(nameof(StatusBrush));
                OnPropertyChanged(nameof(TextBrush));
            }
        }
    }

    public Brush BackgroundBrush => _isActive ? _activeBgBrush : _inactiveBgBrush;
    public Brush BorderBrush => _isActive ? _activeBorderBrush : _inactiveBorderBrush;
    public Brush StatusBrush => _isActive ? _activeStatusBrush : _inactiveStatusBrush;
    public Brush TextBrush => _isActive ? _activeTextBrush : _inactiveTextBrush;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}
