// Program.cs
// Restoran — консольное приложение (один файл)

// Требует .NET 6+ (рекомендуется .NET 8)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Класс Table — описывает стол в ресторане.
/// Свойства полностью инициализированы, чтобы не было предупреждений компилятора.
/// </summary>
class Table
{
    public int Id { get; set; } = 0;
    public string Location { get; set; } = string.Empty; // например "у окна"
    public int Seats { get; set; } = 0;

    // Краткая строка для списков
    public string ToShortString() => $"ID {Id} | {Location} | Мест: {Seats}";
}

/// <summary>
/// Класс Reservation — описание брони.
/// Хранит ClientId, имя, телефон, интервал брони, комментарий и назначенный стол.
/// Методы проверки перекрытия/покрытия используются в логике менеджера.
/// </summary>
class Reservation
{
    public int Id { get; set; } = 0;
    public int ClientId { get; set; } = 0;
    public string ClientName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime Start { get; set; } = DateTime.MinValue;
    public DateTime End { get; set; } = DateTime.MinValue;
    public string Comment { get; set; } = string.Empty;
    public int TableId { get; set; } = 0;

    /// <summary>
    /// Проверка перекрытия двух интервалов [Start, End) и [s, e).
    /// Точная до минут (DateTime сравнение).
    /// Возвращает true если перекрываются.
    /// </summary>
    public bool Overlaps(DateTime s, DateTime e)
    {
        return Start < e && s < End;
    }

    /// <summary>
    /// Проверяет покрывает ли интервал текущее время t (Start <= t &lt; End).
    /// </summary>
    public bool Covers(DateTime t) => Start <= t && t < End;

    /// <summary>
    /// Краткий формат для вывода брони.
    /// Комментарий заменяется на "пусто", если пустая строка.
    /// </summary>
    public string ToShortString()
    {
        var comment = string.IsNullOrWhiteSpace(Comment) ? "пусто" : Comment;
        return $"ID {Id} | КлиентID {ClientId} | Стол {TableId} | {ClientName} ({Phone}) | {Start:yyyy-MM-dd HH:mm} — {End:yyyy-MM-dd HH:mm} | Комментарий: {comment}";
    }
}

/// <summary>
/// Перечисление категорий блюд.
/// Сериализуется в JSON строкой благодаря JsonStringEnumConverter.
/// </summary>
enum DishCategory
{
    Напитки,
    Салаты,
    ХолодныеЗакуски,
    ГорячиеЗакуски,
    Супы,
    ГорячиеБлюда,
    Десерт,
    Другое
}

/// <summary>
/// Класс Dish — блюдо меню.
/// Удалены теги по требованию; все поля инициализированы.
/// </summary>
class Dish
{
    public int Id { get; set; } = 0;
    public string Name { get; set; } = string.Empty;
    public string Composition { get; set; } = string.Empty;
    public string Weight { get; set; } = string.Empty;
    public decimal Price { get; set; } = 0m;
    public DishCategory Category { get; set; } = DishCategory.Другое;
    public int CookTimeMinutes { get; set; } = 0;

    public string ToShortString() => $"ID {Id} | {Name} | {Category} | {Price:0.00} руб.";
}

/// <summary>
/// Элемент заказа: ссылка на DishId и количество.
/// </summary>
class OrderItem
{
    public int DishId { get; set; } = 0;
    public int Quantity { get; set; } = 0;
}

/// <summary>
/// Класс Order — заказ, привязанный к ClientId и столу.
/// Может быть открыт или закрыт (ClosedAt != null).
/// Total вычисляется при закрытии.
/// </summary>
class Order
{
    public int Id { get; set; } = 0;
    public int ClientId { get; set; } = 0;
    public int TableId { get; set; } = 0;
    public List<OrderItem> Items { get; set; } = new List<OrderItem>();
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.MinValue;
    public int WaiterId { get; set; } = 0;
    public DateTime? ClosedAt { get; set; } = null;
    public decimal Total { get; set; } = 0m;

    public bool IsClosed => ClosedAt.HasValue;

    public string ToShortString()
    {
        var comment = string.IsNullOrWhiteSpace(Comment) ? "пусто" : Comment;
        return $"ID {Id} | КлиентID {ClientId} | Стол {TableId} | Позиции: {Items.Sum(i => i.Quantity)} | Создан: {CreatedAt:yyyy-MM-dd HH:mm} | {(IsClosed ? "Закрыт" : "Открыт")} | Комментарий: {comment}";
    }
}

/// <summary>
/// Класс Config хранит путь к папке данных.
/// Сохраняется в config.json рядом с исполняемым файлом.
/// </summary>
class Config
{
    public string DataPath { get; set; } = string.Empty;
}

/// <summary>
/// RestoranManager — основной менеджер приложения.
/// Хранит списки таблиц/бронирований/блюд/заказов, читает/пишет JSON,
/// реализует логику добавления/редактирования/удаления, проверки конфликтов и статистики.
/// Включает виртуальное время VirtualNow, используемое в логике (можно менять).
/// </summary>
class RestoranManager
{
    // config.json хранится рядом с исполняемым файлом
    private const string CONFIG_FILE = "config.json";

    // В конфиге хранится DataPath; если пустой, используется defaultDataPath
    private Config config = new Config();
    private readonly string defaultDataPath = @"C:\Restoran_Data";

    // Путь к файлам данных вычисляется относительно выбранной папки данных
    private string DataPath => string.IsNullOrWhiteSpace(config.DataPath) ? defaultDataPath : config.DataPath;
    private string TablesFile => Path.Combine(DataPath, "tables.json");
    private string ReservationsFile => Path.Combine(DataPath, "reservations.json");
    private string DishesFile => Path.Combine(DataPath, "dishes.json");
    private string OrdersFile => Path.Combine(DataPath, "orders.json");

    // Json опции: отступы, и enum как строка
    private readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    // Коллекции данных — инициализированы, чтобы не было warning CS8618
    public List<Table> Tables { get; private set; } = new List<Table>();
    public List<Reservation> Reservations { get; private set; } = new List<Reservation>();
    public List<Dish> Dishes { get; private set; } = new List<Dish>();
    public List<Order> Orders { get; private set; } = new List<Order>();

    // Следующие ID вычисляются от текущих коллекций
    private int NextTableId => Tables.Any() ? Tables.Max(t => t.Id) + 1 : 1;
    private int NextReservationId => Reservations.Any() ? Reservations.Max(r => r.Id) + 1 : 1;
    private int NextDishId => Dishes.Any() ? Dishes.Max(d => d.Id) + 1 : 1;
    private int NextOrderId => Orders.Any() ? Orders.Max(o => o.Id) + 1 : 1;

    // Виртуальное "сейчас" — используется для логики (можно менять пользователем)
    public DateTime VirtualNow { get; set; } = DateTime.Now;

    /// <summary>
    /// Конструктор менеджера: загружает конфиг, создаёт папку данных (если нужно) и загружает данные.
    /// </summary>
    public RestoranManager()
    {
        LoadConfig();
        EnsureDataFolderExists();
        LoadAll();
    }

    #region Config & Data folder

    /// <summary>
    /// Загружает config.json (если есть). В противном случае оставляет дефолтную конфигурацию.
    /// </summary>
    private void LoadConfig()
    {
        try
        {
            if (File.Exists(CONFIG_FILE))
            {
                var json = File.ReadAllText(CONFIG_FILE);
                var cfg = JsonSerializer.Deserialize<Config>(json, jsonOptions);
                config = cfg ?? new Config();
            }
            else
            {
                config = new Config();
            }
        }
        catch
        {
            // В случае ошибки конфигурация остаётся пустой
            config = new Config();
        }
    }

    /// <summary>
    /// Сохраняет config.json рядом с исполняемым файлом.
    /// </summary>
    private void SaveConfig()
    {
        try
        {
            var json = JsonSerializer.Serialize(config, jsonOptions);
            File.WriteAllText(CONFIG_FILE, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка сохранения config: " + ex.Message);
        }
    }

    /// <summary>
    /// Создаёт папку данных, если её нет.
    /// </summary>
    private void EnsureDataFolderExists()
    {
        try
        {
            if (!Directory.Exists(DataPath))
                Directory.CreateDirectory(DataPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка создания папки данных: " + ex.Message);
        }
    }

    /// <summary>
    /// Интерактивный выбор папки данных.
    /// Спрашивает новый путь, предлагает перенести данные туда,
    /// и предлагает удалить старые файлы после копирования (оба варианта реализованы).
    /// </summary>
    public void SetDataPathInteractive()
    {
        Console.WriteLine($"Текущая папка для хранения данных: {DataPath}");
        Console.Write("Введите полный путь к папке для хранения данных (Enter = использовать по умолчанию C:\\Restoran_Data): ");
        var input = Console.ReadLine() ?? string.Empty;

        string newPath;
        if (string.IsNullOrWhiteSpace(input))
        {
            newPath = defaultDataPath;
        }
        else
        {
            newPath = input.Trim();
        }

        try
        {
            // создаём папку если нужно
            if (!Directory.Exists(newPath))
                Directory.CreateDirectory(newPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка создания новой папки: " + ex.Message);
            return;
        }

        // Если папка отличается от текущей, спрашиваем про перенос
        var currentPath = DataPath;
        if (!string.Equals(Path.GetFullPath(newPath).TrimEnd('\\'), Path.GetFullPath(currentPath).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            Console.Write($"Хотите перенести существующие данные из \"{currentPath}\" в \"{newPath}\"? (да/нет): ");
            var ans = ReadYesNo();
            if (ans)
            {
                // перенос файлов
                var files = new[] { ("tables.json", TablesFile), ("reservations.json", ReservationsFile), ("dishes.json", DishesFile), ("orders.json", OrdersFile) };
                // копирование происходит по файлам: если файл существует в старом месте, копируем в новое
                foreach (var (name, destPath) in files)
                {
                    var srcPath = Path.Combine(currentPath, name);
                    var destFull = Path.Combine(newPath, name);
                    try
                    {
                        if (File.Exists(srcPath))
                        {
                            File.Copy(srcPath, destFull, overwrite: true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка копирования {name}: {ex.Message}");
                    }
                }

                Console.Write("Удалить старые файлы после успешного копирования? (да/нет): ");
                var deleteOld = ReadYesNo();
                if (deleteOld)
                {
                    foreach (var name in new[] { "tables.json", "reservations.json", "dishes.json", "orders.json" })
                    {
                        var srcPath = Path.Combine(currentPath, name);
                        try
                        {
                            if (File.Exists(srcPath))
                                File.Delete(srcPath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Не удалось удалить {name}: {ex.Message}");
                        }
                    }
                }
            }
        }

        // сохраняем новый путь в конфиге и применяем
        config.DataPath = newPath;
        SaveConfig();

        // убеждаемся что папка создается и файлы (если нужны) существуют
        EnsureDataFolderExists();

        Console.WriteLine("Папка данных установлена: " + DataPath);

        // Перезагрузим данные из новой папки
        LoadAll();
    }

    #endregion

    #region Load/Save

    /// <summary>
    /// Загружает все коллекции из JSON-файлов (если файлы существуют).
    /// Если файла нет — коллекция остаётся пустой.
    /// </summary>
    public void LoadAll()
    {
        Tables = LoadOrCreate<Table>(TablesFile);
        Reservations = LoadOrCreate<Reservation>(ReservationsFile);
        Dishes = LoadOrCreate<Dish>(DishesFile);
        Orders = LoadOrCreate<Order>(OrdersFile);
    }

    /// <summary>
    /// Универсальная загрузка списка T из файла path.
    /// Возвращает пустой список при любой ошибке.
    /// </summary>
    private List<T> LoadOrCreate<T>(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new List<T>();
            var json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<T>>(json, jsonOptions);
            return list ?? new List<T>();
        }
        catch
        {
            return new List<T>();
        }
    }

    /// <summary>
    /// Сохраняет все коллекции в JSON-файлы в папке DataPath.
    /// </summary>
    public void SaveAll()
    {
        EnsureDataFolderExists();
        Save(Tables, TablesFile);
        Save(Reservations, ReservationsFile);
        Save(Dishes, DishesFile);
        Save(Orders, OrdersFile);
    }

    /// <summary>
    /// Универсальная сохранение списка T в путь path.
    /// Оборачиваем в try/catch — в случае ошибки сообщаем пользователю.
    /// </summary>
    private void Save<T>(List<T> list, string path)
    {
        try
        {
            var json = JsonSerializer.Serialize(list, jsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка сохранения " + Path.GetFileName(path) + ": " + ex.Message);
        }
    }

    #endregion

    #region Test data (инициализация только по выбору пользователя)

    /// <summary>
    /// Создаёт тестовые данные — вызывается только вручную через пункт меню.
    /// Данные сохраняются в текущую папку данных.
    /// </summary>
    public void InitDefaults()
    {
        Tables = new List<Table>
        {
            new Table { Id = 1, Location = "у окна", Seats = 4 },
            new Table { Id = 2, Location = "у прохода", Seats = 2 },
            new Table { Id = 3, Location = "в глубине", Seats = 6 },
            new Table { Id = 4, Location = "у выхода", Seats = 4 },
        };

        Dishes = new List<Dish>
        {
            new Dish { Id = 1, Name = "Кофе Американо", Composition="вода, кофе", Weight="200", Price=120m, Category=DishCategory.Напитки, CookTimeMinutes=5 },
            new Dish { Id = 2, Name = "Цезарь с курицей", Composition="салат, курица, соус", Weight="250", Price=420m, Category=DishCategory.Салаты, CookTimeMinutes=15 },
            new Dish { Id = 3, Name = "Суп грибной", Composition="грибы, бульон", Weight="300", Price=280m, Category=DishCategory.Супы, CookTimeMinutes=20 },
            new Dish { Id = 4, Name = "Чизкейк", Composition="сыр, печенье", Weight="120", Price=350m, Category=DishCategory.Десерт, CookTimeMinutes=30 }
        };

        Reservations = new List<Reservation>
        {
            new Reservation { Id = 1, ClientId = 101, ClientName="Макс", Phone="88005553535", Start = DateTime.Today.AddHours(12), End = DateTime.Today.AddHours(15), Comment="День рождения", TableId = 3 },
            new Reservation { Id = 2, ClientId = 102, ClientName="Анна", Phone="5745552377", Start = DateTime.Today.AddHours(16), End = DateTime.Today.AddHours(17), Comment="Деловая встреча", TableId = 3 }
        };

        Orders = new List<Order>
        {
            new Order
            {
                Id = 1,
                ClientId = 101,
                TableId = 3,
                Items = new List<OrderItem> { new OrderItem{ DishId = 2, Quantity = 2 }, new OrderItem{ DishId = 3, Quantity = 1 } },
                CreatedAt = DateTime.Now.AddHours(-2),
                WaiterId = 1,
                ClosedAt = DateTime.Now.AddHours(-1),
                Total = 2 * 420m + 1 * 280m,
                Comment = "Оплата наличными"
            },
            new Order
            {
                Id = 2,
                ClientId = 102,
                TableId = 1,
                Items = new List<OrderItem> { new OrderItem{ DishId = 1, Quantity = 3 } },
                CreatedAt = DateTime.Now.AddMinutes(-30),
                WaiterId = 2,
                Comment = ""
            }
        };

        SaveAll();
        Console.WriteLine("Тестовые данные созданы и сохранены в папку данных.");
    }

    #endregion

    #region Tables (столы)

    /// <summary>
    /// Показывает список всех столов.
    /// Для каждого стола также выводится расписание броней; если бронь активна сейчас,
    /// рядом будет ID клиента и имя.
    /// </summary>
    public void ShowAllTables()
    {
        if (!Tables.Any())
        {
            Console.WriteLine("Нет столов.");
            return;
        }

        foreach (var t in Tables.OrderBy(t => t.Id))
        {
            Console.WriteLine(t.ToShortString());
            ShowTableScheduleShort(t.Id, "  ");
        }
    }

    /// <summary>
    /// Короткий вывод расписания столов — используется в ShowAllTables.
    /// Для активных броней (по VirtualNow) выводит также ID клиента.
    /// </summary>
    private void ShowTableScheduleShort(int tableId, string indent = "")
    {
        var now = VirtualNow;
        var tableRes = Reservations.Where(r => r.TableId == tableId).OrderBy(r => r.Start).ToList();
        if (!tableRes.Any())
        {
            Console.WriteLine($"{indent}Нет бронирований.");
            return;
        }

        foreach (var r in tableRes)
        {
            var activeMark = r.Covers(now) ? " (СЕЙЧАС ЗАНЯТ)" : string.Empty;
            var clientInfo = r.Covers(now) ? $" | КлиентID: {r.ClientId} ({r.ClientName})" : string.Empty;
            var comment = string.IsNullOrWhiteSpace(r.Comment) ? "пусто" : r.Comment;
            Console.WriteLine($"{indent}{r.Start:yyyy-MM-dd HH:mm} — {r.End:yyyy-MM-dd HH:mm}{activeMark}  | {r.ClientName} ({r.Phone}){clientInfo} | Комментарий: {comment}");
        }
    }

    /// <summary>
    /// Запрашивает ID стола и показывает подробную информацию только по выбранному столу.
    /// Если ID не найден — сообщает об этом.
    /// </summary>
    public void ShowTableInfoByIdInteractive()
    {
        Console.Write("Введите ID стола: ");
        var s = Console.ReadLine() ?? string.Empty;
        if (!int.TryParse(s, out int id))
        {
            Console.WriteLine("Неверный ID.");
            return;
        }
        ShowTableInfo(id);
    }

    /// <summary>
    /// Показ информации о столе по ID: расположение, места и расписание.
    /// Показывает ID брони и ID клиента для каждой брони.
    /// </summary>
    public void ShowTableInfo(int id)
    {
        var t = Tables.FirstOrDefault(x => x.Id == id);
        if (t == null)
        {
            Console.WriteLine("Стол не найден.");
            return;
        }

        Console.WriteLine($"ID: {t.Id}");
        Console.WriteLine($"Расположение: {t.Location}");
        Console.WriteLine($"Количество мест: {t.Seats}");
        Console.WriteLine("Расписание бронирований:");
        var tableRes = Reservations.Where(r => r.TableId == t.Id).OrderBy(r => r.Start).ToList();
        if (!tableRes.Any())
        {
            Console.WriteLine("  Нет бронирований.");
        }
        else
        {
            foreach (var r in tableRes)
            {
                var activeMark = r.Covers(VirtualNow) ? " (СЕЙЧАС ЗАНЯТ)" : string.Empty;
                var comment = string.IsNullOrWhiteSpace(r.Comment) ? "пусто" : r.Comment;
                Console.WriteLine($"  {r.Start:yyyy-MM-dd HH:mm} — {r.End:yyyy-MM-dd HH:mm}{activeMark}  | ID брони {r.Id} | КлиентID {r.ClientId} | {r.ClientName} | Тел: {r.Phone} | Комментарий: {comment}");
            }
        }
    }

    /// <summary>
    /// Добавляет стол: запрашивает расположение и количество мест.
    /// </summary>
    public void AddTable()
    {
        var t = new Table { Id = NextTableId };
        Console.Write("Расположение (пример: у окна): ");
        t.Location = Console.ReadLine() ?? string.Empty;
        Console.Write("Количество мест: ");
        var s = Console.ReadLine() ?? string.Empty;
        if (!int.TryParse(s, out int seats)) seats = 4;
        t.Seats = seats;
        Tables.Add(t);
        SaveAll();
        Console.WriteLine($"Добавлен стол ID {t.Id}");
    }

    /// <summary>
    /// Редактирование стола по ID. Нельзя редактировать если сейчас по столу есть активная бронь.
    /// </summary>
    public void EditTable()
    {
        if (!Tables.Any())
        {
            Console.WriteLine("Нет столов.");
            return;
        }

        PrintTablesShort();
        Console.Write("Введите ID стола для редактирования: ");
        var s = Console.ReadLine() ?? string.Empty;
        if (!int.TryParse(s, out int id)) return;
        var t = Tables.FirstOrDefault(x => x.Id == id);
        if (t == null)
        {
            Console.WriteLine("Не найден.");
            return;
        }

        var now = VirtualNow;
        if (Reservations.Any(r => r.TableId == t.Id && r.Covers(now)))
        {
            Console.WriteLine("Нельзя редактировать стол — по нему есть активное бронирование прямо сейчас.");
            return;
        }

        Console.WriteLine($"Текущее расположение: {t.Location}. Введите новое (или Enter чтобы оставить):");
        var newLoc = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(newLoc)) t.Location = newLoc;

        Console.WriteLine($"Текущее число мест: {t.Seats}. Введите новое (или Enter):");
        var s2 = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(s2) && int.TryParse(s2, out int ns)) t.Seats = ns;

        SaveAll();
        Console.WriteLine("Стол обновлён.");
    }

    /// <summary>
    /// Удаление стола с проверкой на наличие броней.
    /// </summary>
    public void DeleteTable()
    {
        if (!Tables.Any())
        {
            Console.WriteLine("Нет столов.");
            return;
        }

        PrintTablesShort();
        Console.Write("Введите ID стола для удаления: ");
        var s = Console.ReadLine() ?? string.Empty;
        if (!int.TryParse(s, out int id)) return;
        var t = Tables.FirstOrDefault(x => x.Id == id);
        if (t == null)
        {
            Console.WriteLine("Не найден.");
            return;
        }

        if (Reservations.Any(r => r.TableId == id))
        {
            Console.WriteLine("Нельзя удалить стол — имеются бронирования, удалите их сначала.");
            return;
        }

        Tables.Remove(t);
        SaveAll();
        Console.WriteLine("Стол удалён.");
    }

    /// <summary>
    /// Печатает кратко список столов (ID/расположение/места).
    /// </summary>
    private void PrintTablesShort()
    {
        foreach (var t in Tables.OrderBy(t => t.Id))
            Console.WriteLine(t.ToShortString());
    }

    #endregion

    #region Reservations (бронирования)

    /// <summary>
    /// Выводит все бронирования с комментариями. Если комментарий пуст, выводит "пусто".
    /// </summary>
    public void ShowAllReservations()
    {
        if (!Reservations.Any())
        {
            Console.WriteLine("Нет бронирований.");
            return;
        }

        foreach (var r in Reservations.OrderBy(r => r.Start))
        {
            Console.WriteLine(r.ToShortString());
        }
    }

    /// <summary>
    /// Добавляет бронь: запрос столa, ID клиента, имя, телефон, время начала и окончания, комментарий.
    /// Проверка пересечений по минутам — не допускается перекрытие.
    /// </summary>
    public void AddReservation()
    {
        if (!Tables.Any())
        {
            Console.WriteLine("Нет столов — создайте стол сначала.");
            return;
        }

        Console.WriteLine("Создание брони. Сначала выберите стол из списка:");
        PrintTablesShort();
        Console.Write("ID стола: ");
        var s1 = Console.ReadLine() ?? string.Empty;
        if (!int.TryParse(s1, out int tableId)) { Console.WriteLine("Неверно."); return; }
        if (!Tables.Any(t => t.Id == tableId)) { Console.WriteLine("Стол не найден."); return; }

        Console.Write("ID клиента (число): ");
        var sClient = Console.ReadLine() ?? string.Empty;
        if (!int.TryParse(sClient, out int clientId)) { Console.WriteLine("Неверный ID клиента."); return; }

        Console.Write("Имя клиента: ");
        var name = Console.ReadLine() ?? string.Empty;
        Console.Write("Телефон: ");
        var phone = Console.ReadLine() ?? string.Empty;

        Console.Write("Время начала (yyyy-MM-dd HH:mm): ");
        var sStart = Console.ReadLine() ?? string.Empty;
        if (!DateTime.TryParse(sStart, out DateTime start)) { Console.WriteLine("Неверная дата."); return; }
        Console.Write("Время окончания (yyyy-MM-dd HH:mm): ");
        var sEnd = Console.ReadLine() ?? string.Empty;
        if (!DateTime.TryParse(sEnd, out DateTime end)) { Console.WriteLine("Неверная дата."); return; }
        if (end <= start) { Console.WriteLine("Окончание должно быть позже начала."); return; }

        // Проверка занятости стола (по минутам)
        var conflicts = Reservations.Where(r => r.TableId == tableId && r.Overlaps(start, end)).ToList();
        if (conflicts.Any())
        {
            Console.WriteLine("Стол занят в это время. Существующие брони:");
            foreach (var c in conflicts) Console.WriteLine("  " + c.ToShortString());
            return;
        }

        var res = new Reservation
        {
            Id = NextReservationId,
            ClientId = clientId,
            ClientName = name,
            Phone = phone,
            Start = start,
            End = end,
            TableId = tableId
        };

        Console.Write("Комментарий (опционально): ");
        res.Comment = Console.ReadLine() ?? string.Empty;

        Reservations.Add(res);
        SaveAll();
        Console.WriteLine($"Бронь добавлена. ID {res.Id}");
    }

    /// <summary>
    /// Редактирование брони по её ID. Позволяет менять имя, телефон, начало, конец, стол, комментарий.
    /// При изменении интервала/стола проверяет конфликты с другими бронями.
    /// </summary>
    public void EditReservation()
    {
        if (!Reservations.Any())
        {
            Console.WriteLine("Нет бронирований.");
            return;
        }

        ShowAllReservations();
        Console.Write("Введите ID брони для редактирования: ");
        var s = Console.ReadLine() ?? string.Empty;
        if (!int.TryParse(s, out int id)) return;
        var r = Reservations.FirstOrDefault(x => x.Id == id);
        if (r == null) { Console.WriteLine("Не найдено."); return; }

        Console.WriteLine($"Текущее имя: {r.ClientName}. Новое (Enter оставить): ");
        var newName = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(newName)) r.ClientName = newName;

        Console.WriteLine($"Текущий телефон: {r.Phone}. Новое (Enter оставить): ");
        var newPhone = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(newPhone)) r.Phone = newPhone;

        Console.WriteLine($"Текущее начало: {r.Start:yyyy-MM-dd HH:mm}. Новое (yyyy-MM-dd HH:mm) или Enter:");
        var sStart = Console.ReadLine() ?? string.Empty;
        DateTime newStart = r.Start;
        if (!string.IsNullOrWhiteSpace(sStart) && DateTime.TryParse(sStart, out DateTime tmpS)) newStart = tmpS;

        Console.WriteLine($"Текущее окончание: {r.End:yyyy-MM-dd HH:mm}. Новое (yyyy-MM-dd HH:mm) или Enter:");
        var sEnd = Console.ReadLine() ?? string.Empty;
        DateTime newEnd = r.End;
        if (!string.IsNullOrWhiteSpace(sEnd) && DateTime.TryParse(sEnd, out DateTime tmpE)) newEnd = tmpE;

        if (newEnd <= newStart) { Console.WriteLine("Неверный интервал."); return; }

        // Проверка на конфликт с другими бронями того же стола
        var conflicts = Reservations.Where(x => x.TableId == r.TableId && x.Id != r.Id && x.Overlaps(newStart, newEnd)).ToList();
        if (conflicts.Any())
        {
            Console.WriteLine("Нельзя применить изменения — конфликт с существующими бронями:");
            foreach (var c in conflicts) Console.WriteLine("  " + c.ToShortString());
            return;
        }

        r.Start = newStart;
        r.End = newEnd;

        Console.WriteLine($"Текущий стол: {r.TableId}. Ввести новый ID стола или Enter чтобы оставить:");
        var sTable = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(sTable) && int.TryParse(sTable, out int newTableId))
        {
            if (!Tables.Any(t => t.Id == newTableId)) { Console.WriteLine("Стол не найден, стол не изменён."); }
            else
            {
                var conflicts2 = Reservations.Where(x => x.TableId == newTableId && x.Id != r.Id && x.Overlaps(r.Start, r.End)).ToList();
                if (conflicts2.Any()) Console.WriteLine("Нельзя перевести бронь на этот стол — есть конфликты.");
                else r.TableId = newTableId;
            }
        }

        Console.WriteLine($"Комментарий: {r.Comment}. Введите новый (Enter оставить):");
        var sCom = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(sCom)) r.Comment = sCom;

        SaveAll();
        Console.WriteLine("Бронь обновлена.");
    }

    /// <summary>
    /// Отмена (удаление) брони по ID.
    /// </summary>
    public void CancelReservation()
    {
        if (!Reservations.Any())
        {
            Console.WriteLine("Нет бронирований.");
            return;
        }

        ShowAllReservations();
        Console.Write("Введите ID брони для отмены: ");
        var s = Console.ReadLine() ?? string.Empty;
        if (!int.TryParse(s, out int id)) return;
        var r = Reservations.FirstOrDefault(x => x.Id == id);
        if (r == null) { Console.WriteLine("Не найдено."); return; }
        Reservations.Remove(r);
        SaveAll();
        Console.WriteLine("Бронь отменена.");
    }

    /// <summary>
    /// Продление/сокращение брони. Пользователь вводит ID брони или ID клиента.
    /// Запрашивает новое время окончания; при конфликте изменений отклоняется.
    /// </summary>
    public void ExtendReservation()
    {
        if (!Reservations.Any())
        {
            Console.WriteLine("Нет бронирований.");
            return;
        }

        Console.Write("Введите ID брони или ID клиента: ");
        var s = Console.ReadLine() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(s)) return;

        Reservation r = null;
        if (int.TryParse(s, out int id))
        {
            r = Reservations.FirstOrDefault(x => x.Id == id);
            if (r == null)
            {
                // если не бронь, пробуем по клиенту: возьмём последнюю бронь клиента
                var byClient = Reservations.Where(x => x.ClientId == id).OrderByDescending(x => x.End).ToList();
                if (byClient.Any()) r = byClient.First();
            }
        }

        if (r == null)
        {
            Console.WriteLine("Бронь не найдена.");
            return;
        }

        Console.WriteLine($"Текущий интервал: {r.Start:yyyy-MM-dd HH:mm} — {r.End:yyyy-MM-dd HH:mm}");
        Console.Write("Укажите новое время окончания (yyyy-MM-dd HH:mm) или Enter чтобы оставить: ");
        var input = Console.ReadLine() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input)) { Console.WriteLine("Отмена."); return; }
        if (!DateTime.TryParse(input, out DateTime newEnd)) { Console.WriteLine("Неверный формат."); return; }
        if (newEnd == r.End) { Console.WriteLine("Время не изменено."); return; }
        if (newEnd <= r.Start) { Console.WriteLine("Окончание должно быть позже начала."); return; }

        // Проверка конфликта с другими бронями на тот же стол
        var conflicts = Reservations.Where(x => x.TableId == r.TableId && x.Id != r.Id && x.Overlaps(r.Start, newEnd)).ToList();
        if (conflicts.Any())
        {
            Console.WriteLine("Нельзя продлить/сократить бронь — конфликт с другими бронями:");
            foreach (var c in conflicts) Console.WriteLine("  " + c.ToShortString());
            return;
        }

        // применяем
        r.End = newEnd;
        SaveAll();
        Console.WriteLine("Бронь обновлена.");
    }

    /// <summary>
    /// Поиск брони по последним 4 цифрам телефона или по имени клиента.
    /// Результат выводится (включая комментарий).
    /// </summary>
    public void FindReservationByPhoneOrName()
    {
        Console.Write("Введите последние 4 цифры номера телефона или имя клиента: ");
        var q = (Console.ReadLine() ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(q)) return;

        var found = Reservations.Where(r =>
            (!string.IsNullOrEmpty(r.Phone) && r.Phone.Length >= 4 && r.Phone.EndsWith(q)) ||
            (!string.IsNullOrEmpty(r.ClientName) && r.ClientName.Contains(q, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        if (!found.Any()) { Console.WriteLine("Не найдено."); return; }
        foreach (var r in found) Console.WriteLine(r.ToShortString());
    }

    #endregion

    #region Dishes (блюда)

    /// <summary>
    /// Вывод меню — блюда группируются по категориям.
    /// Полная информация о блюде выводится в строке.
    /// </summary>
    public void ShowMenu()
    {
        if (!Dishes.Any()) { Console.WriteLine("Нет блюд."); return; }
        var groups = Dishes.GroupBy(d => d.Category).OrderBy(g => g.Key);
        foreach (var g in groups)
        {
            Console.WriteLine($"=== {g.Key} ===");
            foreach (var d in g)
                Console.WriteLine($"  {d.ToShortString()} | Вес: {d.Weight} | Сост.: {d.Composition} | Время: {d.CookTimeMinutes} мин");
        }
    }

    /// <summary>
    /// Добавление блюда (запрос полей).
    /// </summary>
    public void AddDish()
    {
        var d = new Dish { Id = NextDishId };
        Console.Write("Название: ");
        d.Name = Console.ReadLine() ?? string.Empty;
        Console.Write("Состав: ");
        d.Composition = Console.ReadLine() ?? string.Empty;
        Console.Write("Вес: ");
        d.Weight = Console.ReadLine() ?? string.Empty;
        Console.Write("Цена (например 120.50): ");
        var s = Console.ReadLine() ?? string.Empty;
        if (!decimal.TryParse(s, out decimal price)) price = 0m;
        d.Price = price;
        Console.WriteLine("Категории: " + string.Join(", ", Enum.GetNames(typeof(DishCategory))));
        Console.Write("Введите категорию (например Супы) или Enter для Другое: ");
        var catStr = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(catStr) && Enum.TryParse<DishCategory>(catStr, true, out var cat)) d.Category = cat;
        Console.Write("Время готовки (минуты): ");
        var s2 = Console.ReadLine() ?? string.Empty;
        if (!int.TryParse(s2, out int tm)) tm = 0;
        d.CookTimeMinutes = tm;

        Dishes.Add(d);
        SaveAll();
        Console.WriteLine($"Блюдо добавлено ID {d.Id}");
    }

    /// <summary>
    /// Редактирование блюда по ID.
    /// </summary>
    public void EditDish()
    {
        if (!Dishes.Any()) { Console.WriteLine("Нет блюд."); return; }
        ShowMenu();
        Console.Write("Введите ID блюда для редактирования: ");
        var s = Console.ReadLine() ?? string.Empty;
        if (!int.TryParse(s, out int id)) return;
        var d = Dishes.FirstOrDefault(x => x.Id == id);
        if (d == null) { Console.WriteLine("Не найдено."); return; }

        Console.WriteLine($"Название ({d.Name}): ");
        var s1 = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(s1)) d.Name = s1;

        Console.WriteLine($"Состав ({d.Composition}): ");
        var s2 = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(s2)) d.Composition = s2;

        Console.WriteLine($"Вес ({d.Weight}): ");
        var s3 = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(s3)) d.Weight = s3;

        Console.WriteLine($"Цена ({d.Price}): ");
        var s4 = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(s4) && decimal.TryParse(s4, out decimal p)) d.Price = p;

        Console.WriteLine($"Время готовки ({d.CookTimeMinutes}): ");
        var s5 = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(s5) && int.TryParse(s5, out int it)) d.CookTimeMinutes = it;

        SaveAll();
        Console.WriteLine("Блюдо обновлено.");
    }

    /// <summary>
    /// Удаление блюда с предупреждением, если блюдо используется в заказах.
    /// </summary>
    public void DeleteDish()
    {
        if (!Dishes.Any()) { Console.WriteLine("Нет блюд."); return; }
        ShowMenu();
        Console.Write("Введите ID блюда для удаления: ");
        var s = Console.ReadLine() ?? string.Empty;
        if (!int.TryParse(s, out int id)) return;
        var d = Dishes.FirstOrDefault(x => x.Id == id);
        if (d == null) { Console.WriteLine("Не найдено."); return; }

        var used = Orders.Any(o => o.Items.Any(i => i.DishId == id));
        if (used)
        {
            Console.Write("ВНИМАНИЕ: блюдо используется в заказах. Удалить? (да/нет): ");
            var c = ReadYesNo();
            if (!c) { Console.WriteLine("Отменено."); return; }
        }

        Dishes.Remove(d);
        SaveAll();
        Console.WriteLine("Блюдо удалено.");
    }

    #endregion

    #region Orders (заказы)

    /// <summary>
    /// Показ всех заказов; для каждой позиции выводится название блюда и сумма.
    /// Комментарий выводится (если пуст — "пусто").
    /// </summary>
    public void ShowAllOrders()
    {
        if (!Orders.Any()) { Console.WriteLine("Нет заказов."); return; }

        foreach (var o in Orders.OrderBy(o => o.CreatedAt))
        {
            Console.WriteLine(o.ToShortString());
            foreach (var it in o.Items)
            {
                var dish = Dishes.FirstOrDefault(d => d.Id == it.DishId);
                Console.WriteLine($"  {it.Quantity} x {(dish?.Name ?? $"Блюдо#{it.DishId}")} = {(dish != null ? dish.Price * it.Quantity : 0):0.00} руб.");
            }
            if (o.IsClosed) Console.WriteLine($"  Итого: {o.Total:0.00} руб. (закрыт {o.ClosedAt:yyyy-MM-dd HH:mm})");
        }
    }

    /// <summary>
    /// Создание заказа: запрашивает ID клиента (заказ привязан к клиенту).
    /// Разрешается только если у клиента есть бронь, покрывающая VirtualNow.
    /// </summary>
    public void CreateOrder()
    {
        if (!Tables.Any()) { Console.WriteLine("Нет столов."); return; }
        if (!Dishes.Any()) { Console.WriteLine("Нет блюд."); return; }

        Console.Write("ID клиента (число): ");
        var s = Console.ReadLine() ?? string.Empty;
        if (!int.TryParse(s, out int clientId)) { Console.WriteLine("Неверный ID клиента."); return; }

        // проверка есть ли у клиента бронь, покрывающая VirtualNow
        var clientRes = Reservations.FirstOrDefault(r => r.ClientId == clientId && r.Covers(VirtualNow));
        if (clientRes == null)
        {
            Console.WriteLine("У клиента нет активной брони на текущее время. Заказ разрешён только для клиента, находящегося в пределах своей брони.");
            return;
        }

        Console.WriteLine($"Клиент: {clientRes.ClientName}. Бронь: {clientRes.Start:yyyy-MM-dd HH:mm} — {clientRes.End:yyyy-MM-dd HH:mm} | Стол {clientRes.TableId}");
        var order = new Order { Id = NextOrderId, ClientId = clientId, TableId = clientRes.TableId, CreatedAt = VirtualNow, WaiterId = 0 };

        bool adding = true;
        while (adding)
        {
            ShowMenu();
            Console.Write("Введите ID блюда для добавления (или Enter чтобы закончить): ");
            var ss = Console.ReadLine() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(ss)) break;
            if (!int.TryParse(ss, out int dishId) || !Dishes.Any(d => d.Id == dishId)) { Console.WriteLine("Блюдо не найдено."); continue; }
            Console.Write("Количество: ");
            var sc = Console.ReadLine() ?? string.Empty;
            if (!int.TryParse(sc, out int q) || q <= 0) { Console.WriteLine("Неверно."); continue; }
            var ex = order.Items.FirstOrDefault(i => i.DishId == dishId);
            if (ex != null) ex.Quantity += q; else order.Items.Add(new OrderItem { DishId = dishId, Quantity = q });
            Console.Write("Добавить ещё? (да/нет): ");
            var c = ReadYesNo();
            adding = c;
        }

        Console.Write("Комментарий: ");
        order.Comment = Console.ReadLine() ?? string.Empty;

        Orders.Add(order);
        SaveAll();
        Console.WriteLine($"Заказ создан ID {order.Id}");
    }

    /// <summary>
    /// Редактирование заказа: добавить/удалить позицию, изменить количество.
    /// Нельзя редактировать закрытый заказ.
    /// </summary>
    public void EditOrder()
    {
        if (!Orders.Any()) { Console.WriteLine("Нет заказов."); return; }
        ShowAllOrders();
        Console.Write("Введите ID заказа для редактирования: ");
        var s = Console.ReadLine() ?? string.Empty;
        if (!int.TryParse(s, out int id)) return;
        var o = Orders.FirstOrDefault(x => x.Id == id);
        if (o == null) { Console.WriteLine("Не найден."); return; }
        if (o.IsClosed) { Console.WriteLine("Нельзя редактировать закрытый заказ."); return; }

        bool loop = true;
        while (loop)
        {
            Console.WriteLine("1. Добавить позицию  2. Удалить позицию  3. Изменить количество  0. Выход");
            var cmd = Console.ReadLine() ?? string.Empty;
            switch (cmd)
            {
                case "1":
                    ShowMenu();
                    Console.Write("ID блюда: ");
                    var s1 = Console.ReadLine() ?? string.Empty;
                    if (!int.TryParse(s1, out int did) || !Dishes.Any(d => d.Id == did)) { Console.WriteLine("Неверно."); break; }
                    Console.Write("Кол-во: ");
                    var s2 = Console.ReadLine() ?? string.Empty;
                    if (!int.TryParse(s2, out int q) || q <= 0) { Console.WriteLine("Неверно."); break; }
                    var exist = o.Items.FirstOrDefault(i => i.DishId == did);
                    if (exist != null) exist.Quantity += q; else o.Items.Add(new OrderItem { DishId = did, Quantity = q });
                    SaveAll(); Console.WriteLine("Позиция добавлена."); break;
                case "2":
                    Console.Write("ID блюда для удаления из заказа: ");
                    var s3 = Console.ReadLine() ?? string.Empty;
                    if (!int.TryParse(s3, out int did2)) { Console.WriteLine("Неверно."); break; }
                    var itm = o.Items.FirstOrDefault(i => i.DishId == did2);
                    if (itm == null) { Console.WriteLine("Позиция не найдена."); break; }
                    o.Items.Remove(itm); SaveAll(); Console.WriteLine("Позиция удалена."); break;
                case "3":
                    Console.Write("ID блюда: ");
                    var s4 = Console.ReadLine() ?? string.Empty;
                    if (!int.TryParse(s4, out int did3)) { Console.WriteLine("Неверно."); break; }
                    var it3 = o.Items.FirstOrDefault(i => i.DishId == did3);
                    if (it3 == null) { Console.WriteLine("Позиция не найдена."); break; }
                    Console.Write("Новое количество: ");
                    var s5 = Console.ReadLine() ?? string.Empty;
                    if (!int.TryParse(s5, out int nq) || nq <= 0) { Console.WriteLine("Неверно."); break; }
                    it3.Quantity = nq; SaveAll(); Console.WriteLine("Количество обновлено."); break;
                case "0": loop = false; break;
                default: Console.WriteLine("Неизвестно."); break;
            }
        }
    }

    /// <summary>
    /// Закрытие заказа: вычисление суммы Total и установка ClosedAt = VirtualNow.
    /// </summary>
    public void CloseOrder()
    {
        if (!Orders.Any()) { Console.WriteLine("Нет заказов."); return; }
        ShowAllOrders();
        Console.Write("Введите ID заказа для закрытия: ");
        var s = Console.ReadLine() ?? string.Empty;
        if (!int.TryParse(s, out int id)) return;
        var o = Orders.FirstOrDefault(x => x.Id == id);
        if (o == null) { Console.WriteLine("Не найден."); return; }
        if (o.IsClosed) { Console.WriteLine("Уже закрыт."); return; }

        decimal total = 0m;
        foreach (var it in o.Items)
        {
            var dish = Dishes.FirstOrDefault(d => d.Id == it.DishId);
            if (dish != null) total += dish.Price * it.Quantity;
        }
        o.Total = total;
        o.ClosedAt = VirtualNow;
        SaveAll();
        Console.WriteLine($"Заказ закрыт. Итог: {total:0.00} руб.");
    }

    /// <summary>
    /// Удаление заказа по ID.
    /// </summary>
    public void DeleteOrder()
    {
        if (!Orders.Any()) { Console.WriteLine("Нет заказов."); return; }
        ShowAllOrders();
        Console.Write("Введите ID заказа для удаления: ");
        var s = Console.ReadLine() ?? string.Empty;
        if (!int.TryParse(s, out int id)) return;
        var o = Orders.FirstOrDefault(x => x.Id == id);
        if (o == null) { Console.WriteLine("Не найден."); return; }
        Orders.Remove(o);
        SaveAll();
        Console.WriteLine("Заказ удалён.");
    }

    #endregion

    #region Stats & Client check

    /// <summary>
    /// Сумма всех закрытых заказов.
    /// </summary>
    public void SumClosedOrders()
    {
        var sum = Orders.Where(o => o.IsClosed).Sum(o => o.Total);
        Console.WriteLine($"Сумма всех закрытых заказов: {sum:0.00} руб.");
    }

    /// <summary>
    /// Печать "чека клиента" — группировка по категориям с подитогами и итогом.
    /// Ищет все заказы клиента по ClientId.
    /// </summary>
    public void PrintClientCheck()
    {
        Console.Write("Введите ID клиента: ");
        var s = Console.ReadLine() ?? string.Empty;
        if (!int.TryParse(s, out int clientId)) return;

        var clientName = Reservations.FirstOrDefault(r => r.ClientId == clientId)?.ClientName ?? "Неизвестно";
        var clientOrders = Orders.Where(o => o.ClientId == clientId).OrderBy(o => o.CreatedAt).ToList();
        if (!clientOrders.Any()) { Console.WriteLine("У клиента нет заказов."); return; }

        Console.WriteLine($"Имя клиента: {clientName}");
        decimal grandTotal = 0m;

        // собираем все позиции и группируем по категории
        var allItems = new List<(Dish dish, int qty)>();
        foreach (var o in clientOrders)
        {
            foreach (var it in o.Items)
            {
                var dish = Dishes.FirstOrDefault(d => d.Id == it.DishId);
                allItems.Add((dish, it.Quantity));
            }
        }

        var grouped = allItems.Where(x => x.dish != null).GroupBy(x => x.dish.Category);
        foreach (var g in grouped)
        {
            Console.WriteLine($"\nКатегория {g.Key}:");
            decimal subtotal = 0m;
            foreach (var pair in g)
            {
                var name = pair.dish.Name;
                var qty = pair.qty;
                var lineTotal = pair.dish.Price * qty;
                Console.WriteLine($"  {name}  {qty}*{pair.dish.Price:0.00} = {lineTotal:0.00} руб.");
                subtotal += lineTotal;
            }
            Console.WriteLine($"  Под_итог категории: {subtotal:0.00} руб.");
            grandTotal += subtotal;
        }

        var unknowns = allItems.Where(x => x.dish == null).ToList();
        if (unknowns.Any())
        {
            Console.WriteLine("\nКатегория: Неизвестно (удалённые блюда):");
            Console.WriteLine("  Есть позиции с удалёнными блюдами, которые не учтены в сумме.");
        }

        Console.WriteLine($"\nИтог счета: {grandTotal:0.00} руб.");
    }

    /// <summary>
    /// Статистика по количеству заказанных блюд (всех времени).
    /// </summary>
    public void StatsDishCounts()
    {
        var counts = new Dictionary<int, int>();
        foreach (var o in Orders)
        {
            foreach (var it in o.Items)
            {
                counts[it.DishId] = counts.GetValueOrDefault(it.DishId) + it.Quantity;
            }
        }

        var sorted = counts.OrderByDescending(kv => kv.Value);
        Console.WriteLine("Статистика по заказанным блюдам:");
        foreach (var kv in sorted)
        {
            var dish = Dishes.FirstOrDefault(d => d.Id == kv.Key);
            Console.WriteLine($"{dish?.Name ?? "Блюдо#" + kv.Key} — {kv.Value} шт.");
        }
    }

    #endregion

    #region Save/Load interactive

    /// <summary>
    /// Явное сохранение данных (вызывается из меню).
    /// </summary>
    public void SaveDataInteractive()
    {
        try
        {
            SaveAll();
            Console.WriteLine($"Данные сохранены в папку: {DataPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка при сохранении: " + ex.Message);
        }
    }

    /// <summary>
    /// Явная загрузка данных (вызывается из меню).
    /// </summary>
    public void LoadDataInteractive()
    {
        try
        {
            LoadAll();
            Console.WriteLine($"Данные загружены из папки: {DataPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка при загрузке: " + ex.Message);
        }
    }

    #endregion

    #region Clear all data

    /// <summary>
    /// Очистка всех данных (удаление JSON-файлов и очищение списков).
    /// Спрашивает подтверждение, затем удаляет файлы и пересоздаёт пустые.
    /// Программа остаётся в меню после операции.
    /// </summary>
    public void ClearAllDataInteractive()
    {
        Console.Write("Вы уверены, что хотите удалить все данные и JSON-файлы? (да/нет): ");
        var ans = ReadYesNo();
        if (!ans)
        {
            Console.WriteLine("Отменено.");
            return;
        }

        try
        {
            var files = new[] { TablesFile, ReservationsFile, DishesFile, OrdersFile };
            foreach (var f in files)
            {
                try
                {
                    if (File.Exists(f))
                        File.Delete(f);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Не удалось удалить {Path.GetFileName(f)}: {ex.Message}");
                }
            }

            Tables.Clear();
            Reservations.Clear();
            Dishes.Clear();
            Orders.Clear();

            // пересоздаём пустые файлы
            SaveAll();
            Console.WriteLine("Все данные удалены.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка при очистке данных: " + ex.Message);
        }
    }

    #endregion

    #region Virtual Now utilities

    /// <summary>
    /// Вывод текущего виртуального времени.
    /// </summary>
    public void ShowNow() => Console.WriteLine($"Текущее виртуальное время: {VirtualNow:yyyy-MM-dd HH:mm}");

    /// <summary>
    /// Установка виртуального времени интерактивно.
    /// Если нажали Enter — берётся системное текущее время.
    /// </summary>
    public void SetVirtualNowInteractive()
    {
        Console.Write("Укажите новое текущее время (yyyy-MM-dd HH:mm) или Enter чтобы использовать системное текущее время: ");
        var s = Console.ReadLine() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(s))
            VirtualNow = DateTime.Now;
        else if (!DateTime.TryParse(s, out DateTime t))
        {
            Console.WriteLine("Неверный формат.");
            return;
        }
        else
            VirtualNow = t;

        Console.WriteLine($"Сейчас: {VirtualNow:yyyy-MM-dd HH:mm}");
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Универсальное чтение да/нет из консоли. 
    /// Возвращает true для 'да' (регистр не важен), false для 'нет' или пустой строки.
    /// </summary>
    public static bool ReadYesNo()
    {
        var s = Console.ReadLine() ?? string.Empty;
        s = s.Trim().ToLowerInvariant();
        if (s == "да" || s == "д" || s == "yes" || s == "y") return true;
        return false;
    }

    #endregion
}

/// <summary>
/// Главный класс программы Program с методом Main.
/// Содержит меню и обработку команд, вызывает методы RestoranManager.
/// </summary>
class Program
{
    static void Main()
    {
        var mgr = new RestoranManager();

        // При запуске спрашиваем виртуальное время
        Console.WriteLine("=== Restoran (консольное приложение) ===");
        Console.Write("Укажите текущую дату и время (yyyy-MM-dd HH:mm) или Enter для системного времени: ");
        var input = Console.ReadLine() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            mgr.VirtualNow = DateTime.Now;
        }
        else if (!DateTime.TryParse(input, out DateTime dt))
        {
            Console.WriteLine("Неверный формат, используется системное время.");
            mgr.VirtualNow = DateTime.Now;
        }
        else
        {
            mgr.VirtualNow = dt;
        }
        Console.WriteLine($"Сейчас: {mgr.VirtualNow:yyyy-MM-dd HH:mm}");
        Console.WriteLine();

        bool exit = false;
        while (!exit)
        {
            Console.WriteLine("\nГлавное меню:");
            Console.WriteLine("1. Столы — просмотр");
            Console.WriteLine("2. Добавить стол");
            Console.WriteLine("3. Редактировать стол");
            Console.WriteLine("4. Удалить стол");
            Console.WriteLine("5. Показать информацию о столе (с расписанием)");
            Console.WriteLine("6. Бронирования — список");
            Console.WriteLine("7. Добавить бронирование");
            Console.WriteLine("8. Редактировать бронирование");
            Console.WriteLine("9. Отменить бронирование");
            Console.WriteLine("10. Найти бронирование по телефону/имени");
            Console.WriteLine("11. Меню (блюда) — показать");
            Console.WriteLine("12. Добавить блюдо");
            Console.WriteLine("13. Редактировать блюдо");
            Console.WriteLine("14. Удалить блюдо");
            Console.WriteLine("15. Заказы — список");
            Console.WriteLine("16. Создать заказ (запрашивает ID клиента)");
            Console.WriteLine("17. Редактировать заказ");
            Console.WriteLine("18. Закрыть заказ");
            Console.WriteLine("19. Удалить заказ");
            Console.WriteLine("20. Статистика: сумма всех закрытых заказов");
            Console.WriteLine("21. Чек клиента (по ID клиента)");
            Console.WriteLine("22. Статистика: количество заказанных блюд");
            Console.WriteLine("23. Продлить/изменить бронь");
            Console.WriteLine("24. Выбрать место хранения данных (папка) — с переносом данных");
            Console.WriteLine("25. Сохранить данные (в текущую папку)");
            Console.WriteLine("26. Загрузить данные (из текущей папки)");
            Console.WriteLine("27. Показать/изменить текущее виртуальное время");
            Console.WriteLine("28. Очистить все данные");
            Console.WriteLine("30. Создать тестовые данные (инициализация по выбору)");
            Console.WriteLine("0. Выход");

            Console.Write("Выберите действие: ");
            var cmd = Console.ReadLine() ?? string.Empty;
            Console.WriteLine();
            try
            {
                switch (cmd)
                {
                    case "1": mgr.ShowAllTables(); break;
                    case "2": mgr.AddTable(); break;
                    case "3": mgr.EditTable(); break;
                    case "4": mgr.DeleteTable(); break;
                    case "5": mgr.ShowTableInfoByIdInteractive(); break;
                    case "6": mgr.ShowAllReservations(); break;
                    case "7": mgr.AddReservation(); break;
                    case "8": mgr.EditReservation(); break;
                    case "9": mgr.CancelReservation(); break;
                    case "10": mgr.FindReservationByPhoneOrName(); break;
                    case "11": mgr.ShowMenu(); break;
                    case "12": mgr.AddDish(); break;
                    case "13": mgr.EditDish(); break;
                    case "14": mgr.DeleteDish(); break;
                    case "15": mgr.ShowAllOrders(); break;
                    case "16": mgr.CreateOrder(); break;
                    case "17": mgr.EditOrder(); break;
                    case "18": mgr.CloseOrder(); break;
                    case "19": mgr.DeleteOrder(); break;
                    case "20": mgr.SumClosedOrders(); break;
                    case "21": mgr.PrintClientCheck(); break;
                    case "22": mgr.StatsDishCounts(); break;
                    case "23": mgr.ExtendReservation(); break;
                    case "24": mgr.SetDataPathInteractive(); break;
                    case "25": mgr.SaveDataInteractive(); break;
                    case "26": mgr.LoadDataInteractive(); break;
                    case "27": mgr.SetVirtualNowInteractive(); break;
                    case "28": mgr.ClearAllDataInteractive(); break;
                    case "30":
                        Console.Write("Вы уверены, что хотите создать тестовые данные (да/нет)? ");
                        var ans = RestoranManager.ReadYesNo();
                        if (ans) mgr.InitDefaults();
                        else Console.WriteLine("Отменено.");
                        break;
                    case "0": exit = true; break;
                    default: Console.WriteLine("Неизвестная команда."); break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка: " + ex.Message);
            }
        }

        Console.WriteLine("Выход. Данные сохранены.");
        mgr.SaveAll();
    }
}
