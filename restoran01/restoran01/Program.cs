// Restoran.cs
// Один файл. Требует .NET 6+ (рекомендуется .NET 8).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

#region Models
class Table
{
    public int Id { get; set; }
    public string Location { get; set; } = "";
    public int Seats { get; set; }
    public string ToShortString() => $"ID {Id} | {Location} | Мест: {Seats}";
}

class Reservation
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; } = "";
    public string Phone { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Comment { get; set; } = "";
    public int TableId { get; set; }

    public bool Overlaps(DateTime s, DateTime e)
    {
        // Перекрытие [Start, End) и [s, e) - минутная точность (DateTime сравнения)
        return Start < e && s < End;
    }

    public bool Covers(DateTime time) => Start <= time && time < End;

    public string ToShortString() =>
        $"ID {Id} | КлиентID {ClientId} | Стол {TableId} | {ClientName} ({Phone}) | {Start:yyyy-MM-dd HH:mm} — {End:yyyy-MM-dd HH:mm} | Комментарий: {(string.IsNullOrWhiteSpace(Comment) ? "пусто" : Comment)}";
}

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

class Dish
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Composition { get; set; } = "";
    public string Weight { get; set; } = "";
    public decimal Price { get; set; }
    public DishCategory Category { get; set; } = DishCategory.Другое;
    public int CookTimeMinutes { get; set; }
    public string ToShortString() => $"ID {Id} | {Name} | {Category} | {Price:0.00} руб.";
}

class OrderItem
{
    public int DishId { get; set; }
    public int Quantity { get; set; }
}

class Order
{
    public int Id { get; set; }
    public int ClientId { get; set; } // привязка заказа к клиенту
    public int TableId { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public string Comment { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int WaiterId { get; set; } = 0;
    public DateTime? ClosedAt { get; set; } = null;
    public decimal Total { get; set; } = 0m;

    public bool IsClosed => ClosedAt.HasValue;

    public string ToShortString() =>
        $"ID {Id} | КлиентID {ClientId} | Стол {TableId} | Позиции: {Items.Sum(i => i.Quantity)} | Создан: {CreatedAt:yyyy-MM-dd HH:mm} | {(IsClosed ? "Закрыт" : "Открыт")} | Комментарий: {(string.IsNullOrWhiteSpace(Comment) ? "пусто" : Comment)}";
}
#endregion

class Config
{
    public string DataPath { get; set; } = "";
}

class RestoranManager
{
    // config.json располагается рядом с исполняемым файлом
    const string CONFIG_FILE = "config.json";
    Config config = new();

    // data path хранит json-файлы
    string DataPath => string.IsNullOrWhiteSpace(config.DataPath) ? defaultDataPath : config.DataPath;
    string defaultDataPath = @"C:\Restoran_Data";

    string TablesFile => Path.Combine(DataPath, "tables.json");
    string ReservationsFile => Path.Combine(DataPath, "reservations.json");
    string DishesFile => Path.Combine(DataPath, "dishes.json");
    string OrdersFile => Path.Combine(DataPath, "orders.json");

    JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public List<Table> Tables { get; private set; } = new();
    public List<Reservation> Reservations { get; private set; } = new();
    public List<Dish> Dishes { get; private set; } = new();
    public List<Order> Orders { get; private set; } = new();

    int NextTableId => Tables.Any() ? Tables.Max(t => t.Id) + 1 : 1;
    int NextReservationId => Reservations.Any() ? Reservations.Max(r => r.Id) + 1 : 1;
    int NextDishId => Dishes.Any() ? Dishes.Max(d => d.Id) + 1 : 1;
    int NextOrderId => Orders.Any() ? Orders.Max(o => o.Id) + 1 : 1;

    // виртуальное "сейчас" — стартово будет установлено при запуске программы
    public DateTime VirtualNow { get; set; } = DateTime.Now;

    public RestoranManager()
    {
        LoadConfig();
        EnsureDataFolderExists();
        LoadAll();
    }

    void LoadConfig()
    {
        try
        {
            if (File.Exists(CONFIG_FILE))
            {
                var json = File.ReadAllText(CONFIG_FILE);
                config = JsonSerializer.Deserialize<Config>(json) ?? new Config();
                if (string.IsNullOrWhiteSpace(config.DataPath)) config.DataPath = "";
            }
            else
            {
                // no config yet
                config = new Config();
            }
        }
        catch
        {
            config = new Config();
        }
    }

    void SaveConfig()
    {
        var json = JsonSerializer.Serialize(config, jsonOptions);
        File.WriteAllText(CONFIG_FILE, json);
    }

    void EnsureDataFolderExists()
    {
        var path = string.IsNullOrWhiteSpace(config.DataPath) ? defaultDataPath : config.DataPath;
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        // keep config.DataPath as chosen (may be empty)
    }

    public void SetDataPathInteractive()
    {
        Console.WriteLine($"Текущая папка для хранения данных: {(string.IsNullOrWhiteSpace(config.DataPath) ? defaultDataPath : config.DataPath)}");
        Console.Write("Введите полный путь к папке для хранения данных (Enter = использовать по умолчанию C:\\Restoran_Data): ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            config.DataPath = defaultDataPath;
        }
        else
        {
            config.DataPath = input.Trim();
        }
        try
        {
            if (!Directory.Exists(config.DataPath)) Directory.CreateDirectory(config.DataPath);
            SaveConfig();
            Console.WriteLine($"Путь сохранён: {config.DataPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка создания папки: " + ex.Message);
        }
    }

    public void LoadAll()
    {
        Tables = LoadOrCreate<Table>(TablesFile);
        Reservations = LoadOrCreate<Reservation>(ReservationsFile);
        Dishes = LoadOrCreate<Dish>(DishesFile);
        Orders = LoadOrCreate<Order>(OrdersFile);
    }

    List<T> LoadOrCreate<T>(string path)
    {
        try
        {
            if (!File.Exists(path)) return new List<T>();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<T>>(json, jsonOptions) ?? new List<T>();
        }
        catch
        {
            return new List<T>();
        }
    }

    public void SaveAll()
    {
        Save(Tables, TablesFile);
        Save(Reservations, ReservationsFile);
        Save(Dishes, DishesFile);
        Save(Orders, OrdersFile);
    }

    void Save<T>(List<T> list, string path)
    {
        try
        {
            var json = JsonSerializer.Serialize(list, jsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка сохранения: " + ex.Message);
        }
    }

    // ---------- Test data (only on explicit request) ----------
    public void InitDefaults()
    {
        // простая инициализация примеров; вызывается только вручную
        Tables = new List<Table>
        {
            new Table { Id = 1, Location = "у окна", Seats = 4 },
            new Table { Id = 2, Location = "у прохода", Seats = 2 },
            new Table { Id = 3, Location = "в глубине", Seats = 6 },
            new Table { Id = 4, Location = "у выхода", Seats = 4 }
        };

        Dishes = new List<Dish>
        {
            new Dish { Id = 1, Name = "Кофе Американо", Composition="вода, кофе", Weight="200", Price=120m, Category=DishCategory.Напитки, CookTimeMinutes=5 },
            new Dish { Id = 2, Name = "Цезарь с курицей", Composition="салат, курица, соус", Weight="250", Price=420m, Category=DishCategory.Салаты, CookTimeMinutes=15 },
            new Dish { Id = 3, Name = "Суп грибной", Composition="грибы, бульон", Weight="300", Price=280m, Category=DishCategory.Супы, CookTimeMinutes=20 },
            new Dish { Id = 4, Name = "Чизкейк", Composition="сыр, печенье", Weight="120", Price=350m, Category=DishCategory.Десерт, CookTimeMinutes=30 }
        };

        // примеры бронирований (ClientId произвольные)
        Reservations = new List<Reservation>
        {
            new Reservation { Id = 1, ClientId = 101, ClientName="Макс", Phone="88005553535", Start = DateTime.Today.AddHours(12), End = DateTime.Today.AddHours(15), Comment="День рождения", TableId = 3 },
            new Reservation { Id = 2, ClientId = 102, ClientName="Анна", Phone="5745552377", Start = DateTime.Today.AddHours(16), End = DateTime.Today.AddHours(17), Comment="Деловая встреча", TableId = 3 }
        };

        Orders = new List<Order>
        {
            new Order { Id = 1, ClientId = 101, TableId = 3, Items = new List<OrderItem>{ new OrderItem{DishId=2, Quantity=2}, new OrderItem{DishId=3, Quantity=1} }, CreatedAt = DateTime.Now.AddHours(-2), WaiterId = 1, ClosedAt = DateTime.Now.AddHours(-1), Total = 2*420m + 1*280m, Comment="Оплата наличными" },
            new Order { Id = 2, ClientId = 102, TableId = 1, Items = new List<OrderItem>{ new OrderItem{DishId=1, Quantity=3} }, CreatedAt = DateTime.Now.AddMinutes(-30), WaiterId = 2, Comment="" }
        };

        SaveAll();
        Console.WriteLine("Тестовые данные созданы и сохранены в текущей папке данных.");
    }

    // ---------- Tables ----------
    public void ShowAllTables()
    {
        if (!Tables.Any()) { Console.WriteLine("Нет столов."); return; }
        foreach (var t in Tables.OrderBy(t => t.Id))
        {
            Console.WriteLine(t.ToShortString());
            ShowTableScheduleShort(t.Id, "  ");
        }
    }

    void ShowTableScheduleShort(int tableId, string indent = "")
    {
        var now = VirtualNow;
        var tableRes = Reservations.Where(r => r.TableId == tableId).OrderBy(r => r.Start).ToList();
        if (!tableRes.Any()) { Console.WriteLine($"{indent}Нет бронирований."); return; }
        foreach (var r in tableRes)
        {
            var activeMark = r.Covers(now) ? " (СЕЙЧАС ЗАНЯТ)" : "";
            Console.WriteLine($"{indent}{r.Start:yyyy-MM-dd HH:mm} — {r.End:yyyy-MM-dd HH:mm}{activeMark}  | {r.ClientName} ({r.Phone}) | Комментарий: {(string.IsNullOrWhiteSpace(r.Comment) ? "пусто" : r.Comment)}");
        }
    }

    public void AddTable()
    {
        var t = new Table { Id = NextTableId };
        Console.Write("Расположение (пример: у окна): ");
        t.Location = Console.ReadLine() ?? "";
        Console.Write("Количество мест: ");
        if (!int.TryParse(Console.ReadLine(), out int seats)) seats = 4;
        t.Seats = seats;
        Tables.Add(t);
        SaveAll();
        Console.WriteLine($"Добавлен стол ID {t.Id}");
    }

    public void EditTable()
    {
        if (!Tables.Any()) { Console.WriteLine("Нет столов."); return; }
        PrintTablesShort();
        Console.Write("Введите ID стола для редактирования: ");
        if (!int.TryParse(Console.ReadLine(), out int id)) return;
        var t = Tables.FirstOrDefault(x => x.Id == id);
        if (t == null) { Console.WriteLine("Не найден."); return; }

        var now = VirtualNow;
        if (Reservations.Any(r => r.TableId == t.Id && r.Covers(now)))
        {
            Console.WriteLine("Нельзя редактировать стол — по нему есть активное бронирование прямо сейчас.");
            return;
        }

        Console.WriteLine($"Текущее расположение: {t.Location}. Введите новое (или Enter):");
        var newLoc = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(newLoc)) t.Location = newLoc;

        Console.WriteLine($"Текущее число мест: {t.Seats}. Введите новое (или Enter):");
        var s = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(s) && int.TryParse(s, out int ns)) t.Seats = ns;

        SaveAll();
        Console.WriteLine("Стол обновлён.");
    }

    public void DeleteTable()
    {
        if (!Tables.Any()) { Console.WriteLine("Нет столов."); return; }
        PrintTablesShort();
        Console.Write("Введите ID стола для удаления: ");
        if (!int.TryParse(Console.ReadLine(), out int id)) return;
        var t = Tables.FirstOrDefault(x => x.Id == id);
        if (t == null) { Console.WriteLine("Не найден."); return; }

        if (Reservations.Any(r => r.TableId == id))
        {
            Console.WriteLine("Нельзя удалить стол — имеются бронирования, удалите их сначала.");
            return;
        }

        Tables.Remove(t);
        SaveAll();
        Console.WriteLine("Стол удалён.");
    }

    void PrintTablesShort()
    {
        foreach (var t in Tables.OrderBy(t => t.Id))
            Console.WriteLine(t.ToShortString());
    }

    // ---------- Reservations ----------
    public void ShowAllReservations()
    {
        if (!Reservations.Any()) { Console.WriteLine("Нет бронирований."); return; }
        foreach (var r in Reservations.OrderBy(r => r.Start))
        {
            Console.WriteLine(r.ToShortString());
        }
    }

    public void AddReservation()
    {
        if (!Tables.Any()) { Console.WriteLine("Нет столов — создайте стол сначала."); return; }
        Console.WriteLine("Создание брони. Сначала выберите стол из списка:");
        PrintTablesShort();
        Console.Write("ID стола: ");
        if (!int.TryParse(Console.ReadLine(), out int tableId)) { Console.WriteLine("Неверно."); return; }
        if (!Tables.Any(t => t.Id == tableId)) { Console.WriteLine("Стол не найден."); return; }

        Console.Write("ID клиента (число): ");
        if (!int.TryParse(Console.ReadLine(), out int clientId)) { Console.WriteLine("Неверный ID клиента."); return; }

        Console.Write("Имя клиента: ");
        var name = Console.ReadLine() ?? "";
        Console.Write("Телефон: ");
        var phone = Console.ReadLine() ?? "";

        Console.Write("Время начала (yyyy-MM-dd HH:mm): ");
        if (!DateTime.TryParse(Console.ReadLine(), out DateTime start)) { Console.WriteLine("Неверная дата."); return; }
        Console.Write("Время окончания (yyyy-MM-dd HH:mm): ");
        if (!DateTime.TryParse(Console.ReadLine(), out DateTime end)) { Console.WriteLine("Неверная дата."); return; }
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
        res.Comment = Console.ReadLine() ?? "";

        Reservations.Add(res);
        SaveAll();
        Console.WriteLine($"Бронь добавлена. ID {res.Id}");
    }

    public void EditReservation()
    {
        if (!Reservations.Any()) { Console.WriteLine("Нет бронирований."); return; }
        ShowAllReservations();
        Console.Write("Введите ID брони для редактирования: ");
        if (!int.TryParse(Console.ReadLine(), out int id)) return;
        var r = Reservations.FirstOrDefault(x => x.Id == id);
        if (r == null) { Console.WriteLine("Не найдено."); return; }

        Console.WriteLine($"Текущее имя: {r.ClientName}. Новое (Enter оставить): ");
        var s = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(s)) r.ClientName = s;

        Console.WriteLine($"Текущий телефон: {r.Phone}. Новое (Enter оставить): ");
        s = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(s)) r.Phone = s;

        Console.WriteLine($"Текущее начало: {r.Start:yyyy-MM-dd HH:mm}. Новое (yyyy-MM-dd HH:mm) или Enter:");
        s = Console.ReadLine();
        DateTime newStart = r.Start;
        if (!string.IsNullOrWhiteSpace(s) && DateTime.TryParse(s, out DateTime tmpS)) newStart = tmpS;

        Console.WriteLine($"Текущее окончание: {r.End:yyyy-MM-dd HH:mm}. Новое (yyyy-MM-dd HH:mm) или Enter:");
        s = Console.ReadLine();
        DateTime newEnd = r.End;
        if (!string.IsNullOrWhiteSpace(s) && DateTime.TryParse(s, out DateTime tmpE)) newEnd = tmpE;

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
        s = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(s) && int.TryParse(s, out int newTableId))
        {
            if (!Tables.Any(t => t.Id == newTableId)) { Console.WriteLine("Стол не найден, стол не изменён."); }
            else
            {
                var conflicts2 = Reservations.Where(x => x.TableId == newTableId && x.Id != r.Id && x.Overlaps(r.Start, r.End)).ToList();
                if (conflicts2.Any())
                {
                    Console.WriteLine("Нельзя перевести бронь на этот стол — есть конфликты.");
                }
                else r.TableId = newTableId;
            }
        }

        Console.WriteLine($"Комментарий: {r.Comment}. Введите новый (Enter оставить):");
        s = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(s)) r.Comment = s;

        SaveAll();
        Console.WriteLine("Бронь обновлена.");
    }

    public void CancelReservation()
    {
        if (!Reservations.Any()) { Console.WriteLine("Нет бронирований."); return; }
        ShowAllReservations();
        Console.Write("Введите ID брони для отмены: ");
        if (!int.TryParse(Console.ReadLine(), out int id)) return;
        var r = Reservations.FirstOrDefault(x => x.Id == id);
        if (r == null) { Console.WriteLine("Не найдено."); return; }
        Reservations.Remove(r);
        SaveAll();
        Console.WriteLine("Бронь отменена.");
    }

    // Продлить бронь по ID клиента или ID брони
    public void ExtendReservation()
    {
        if (!Reservations.Any()) { Console.WriteLine("Нет бронирований."); return; }
        Console.Write("Введите ID брони или ID клиента: ");
        var s = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(s)) return;

        Reservation r = null;
        if (int.TryParse(s, out int id))
        {
            // сначала по брони
            r = Reservations.FirstOrDefault(x => x.Id == id);
            if (r == null)
            {
                // по клиенту — выбрать активную на VirtualNow или последний по времени
                var byClient = Reservations.Where(x => x.ClientId == id).OrderByDescending(x => x.End).ToList();
                if (byClient.Any()) r = byClient.First();
            }
        }
        if (r == null) { Console.WriteLine("Бронь не найдена."); return; }

        Console.WriteLine($"Текущий интервал: {r.Start:yyyy-MM-dd HH:mm} — {r.End:yyyy-MM-dd HH:mm}");
        Console.Write("Укажите новое время окончания (yyyy-MM-dd HH:mm) или Enter чтобы оставить: ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input)) { Console.WriteLine("Отмена."); return; }
        if (!DateTime.TryParse(input, out DateTime newEnd)) { Console.WriteLine("Неверный формат."); return; }
        if (newEnd == r.End) { Console.WriteLine("Время не изменено."); return; }
        if (newEnd <= r.Start) { Console.WriteLine("Окончание должно быть позже начала."); return; }

        // Проверка конфликта с другими бронированиями на тот же стол
        var conflicts = Reservations.Where(x => x.TableId == r.TableId && x.Id != r.Id && x.Overlaps(r.Start, newEnd)).ToList();
        if (conflicts.Any())
        {
            Console.WriteLine("Нельзя продлить бронь — конфликт с другими бронями:");
            foreach (var c in conflicts) Console.WriteLine("  " + c.ToShortString());
            return;
        }

        // применяем (разрешено как продление, так и сокращение)
        r.End = newEnd;
        SaveAll();
        Console.WriteLine("Бронь обновлена.");
    }

    public void FindReservationByPhoneOrName()
    {
        Console.Write("Введите последние 4 цифры номера телефона или имя клиента: ");
        var q = (Console.ReadLine() ?? "").Trim();
        if (string.IsNullOrWhiteSpace(q)) return;
        var found = Reservations.Where(r =>
            (r.Phone != null && r.Phone.Length >= 4 && r.Phone.EndsWith(q)) ||
            (!string.IsNullOrEmpty(r.ClientName) && r.ClientName.Contains(q, StringComparison.OrdinalIgnoreCase))
        ).ToList();
        if (!found.Any()) { Console.WriteLine("Не найдено."); return; }
        foreach (var r in found) Console.WriteLine(r.ToShortString());
    }

    // ---------- Dishes ----------
    public void ShowMenu()
    {
        if (!Dishes.Any()) { Console.WriteLine("Нет блюд."); return; }
        var groups = Dishes.GroupBy(d => d.Category).OrderBy(g => g.Key);
        foreach (var g in groups)
        {
            Console.WriteLine($"=== {g.Key} ===");
            foreach (var d in g) Console.WriteLine($"  {d.ToShortString()} | Вес: {d.Weight} | Сост.: {d.Composition} | Время: {d.CookTimeMinutes} мин");
        }
    }

    public void AddDish()
    {
        var d = new Dish { Id = NextDishId };
        Console.Write("Название: ");
        d.Name = Console.ReadLine() ?? "";
        Console.Write("Состав: ");
        d.Composition = Console.ReadLine() ?? "";
        Console.Write("Вес: ");
        d.Weight = Console.ReadLine() ?? "";
        Console.Write("Цена (например 120.50): ");
        if (!decimal.TryParse(Console.ReadLine(), out decimal price)) price = 0m;
        d.Price = price;
        Console.WriteLine("Категории: " + string.Join(", ", Enum.GetNames(typeof(DishCategory))));
        Console.Write("Введите категорию (написав точное имя, напр. Супы) или Enter для Другое: ");
        var catStr = Console.ReadLine() ?? "";
        if (!string.IsNullOrWhiteSpace(catStr) && Enum.TryParse<DishCategory>(catStr, true, out var cat)) d.Category = cat;
        Console.Write("Время готовки (минуты): ");
        if (!int.TryParse(Console.ReadLine(), out int tm)) tm = 0;
        d.CookTimeMinutes = tm;

        Dishes.Add(d);
        SaveAll();
        Console.WriteLine($"Блюдо добавлено ID {d.Id}");
    }

    public void EditDish()
    {
        if (!Dishes.Any()) { Console.WriteLine("Нет блюд."); return; }
        ShowMenu();
        Console.Write("Введите ID блюда для редактирования: ");
        if (!int.TryParse(Console.ReadLine(), out int id)) return;
        var d = Dishes.FirstOrDefault(x => x.Id == id);
        if (d == null) { Console.WriteLine("Не найдено."); return; }

        Console.WriteLine($"Название ({d.Name}): ");
        var s = Console.ReadLine(); if (!string.IsNullOrWhiteSpace(s)) d.Name = s;
        Console.WriteLine($"Состав ({d.Composition}): ");
        s = Console.ReadLine(); if (!string.IsNullOrWhiteSpace(s)) d.Composition = s;
        Console.WriteLine($"Вес ({d.Weight}): "); s = Console.ReadLine(); if (!string.IsNullOrWhiteSpace(s)) d.Weight = s;
        Console.WriteLine($"Цена ({d.Price}): "); s = Console.ReadLine(); if (!string.IsNullOrWhiteSpace(s) && decimal.TryParse(s, out decimal p)) d.Price = p;
        Console.WriteLine($"Время готовки ({d.CookTimeMinutes}): "); s = Console.ReadLine(); if (!string.IsNullOrWhiteSpace(s) && int.TryParse(s, out int it)) d.CookTimeMinutes = it;

        SaveAll();
        Console.WriteLine("Блюдо обновлено.");
    }

    public void DeleteDish()
    {
        if (!Dishes.Any()) { Console.WriteLine("Нет блюд."); return; }
        ShowMenu();
        Console.Write("Введите ID блюда для удаления: ");
        if (!int.TryParse(Console.ReadLine(), out int id)) return;
        var d = Dishes.FirstOrDefault(x => x.Id == id);
        if (d == null) { Console.WriteLine("Не найдено."); return; }

        var used = Orders.Any(o => o.Items.Any(i => i.DishId == id));
        if (used)
        {
            Console.WriteLine("ВНИМАНИЕ: блюдо используется в заказах. Удалить? (y/N)");
            var c = Console.ReadLine();
            if (c?.ToLower() != "y") { Console.WriteLine("Отменено."); return; }
        }

        Dishes.Remove(d);
        SaveAll();
        Console.WriteLine("Блюдо удалено.");
    }

    // ---------- Orders ----------
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

    public void CreateOrder()
    {
        if (!Tables.Any()) { Console.WriteLine("Нет столов."); return; }
        if (!Dishes.Any()) { Console.WriteLine("Нет блюд."); return; }

        Console.Write("ID клиента (число): ");
        if (!int.TryParse(Console.ReadLine(), out int clientId)) { Console.WriteLine("Неверный ID клиента."); return; }

        // проверить у клиента есть бронь, покрывающая VirtualNow
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
            var s = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(s)) break;
            if (!int.TryParse(s, out int dishId) || !Dishes.Any(d => d.Id == dishId)) { Console.WriteLine("Блюдо не найдено."); continue; }
            Console.Write("Количество: ");
            if (!int.TryParse(Console.ReadLine(), out int q) || q <= 0) { Console.WriteLine("Неверно."); continue; }
            var ex = order.Items.FirstOrDefault(i => i.DishId == dishId);
            if (ex != null) ex.Quantity += q; else order.Items.Add(new OrderItem { DishId = dishId, Quantity = q });
            Console.Write("Добавить ещё? (y/N): ");
            var c = Console.ReadLine();
            adding = c?.ToLower() == "y";
        }

        Console.Write("Комментарий: ");
        order.Comment = Console.ReadLine() ?? "";

        Orders.Add(order);
        SaveAll();
        Console.WriteLine($"Заказ создан ID {order.Id}");
    }

    public void EditOrder()
    {
        if (!Orders.Any()) { Console.WriteLine("Нет заказов."); return; }
        ShowAllOrders();
        Console.Write("Введите ID заказа для редактирования: ");
        if (!int.TryParse(Console.ReadLine(), out int id)) return;
        var o = Orders.FirstOrDefault(x => x.Id == id);
        if (o == null) { Console.WriteLine("Не найден."); return; }
        if (o.IsClosed) { Console.WriteLine("Нельзя редактировать закрытый заказ."); return; }

        bool loop = true;
        while (loop)
        {
            Console.WriteLine("1. Добавить позицию  2. Удалить позицию  3. Изменить количество  0. Выход");
            var cmd = Console.ReadLine();
            switch (cmd)
            {
                case "1":
                    ShowMenu();
                    Console.Write("ID блюда: ");
                    if (!int.TryParse(Console.ReadLine(), out int did) || !Dishes.Any(d => d.Id == did)) { Console.WriteLine("Неверно."); break; }
                    Console.Write("Кол-во: "); if (!int.TryParse(Console.ReadLine(), out int q) || q <= 0) { Console.WriteLine("Неверно."); break; }
                    var exist = o.Items.FirstOrDefault(i => i.DishId == did);
                    if (exist != null) exist.Quantity += q; else o.Items.Add(new OrderItem { DishId = did, Quantity = q });
                    SaveAll(); Console.WriteLine("Позиция добавлена."); break;
                case "2":
                    Console.Write("ID блюда для удаления из заказа: ");
                    if (!int.TryParse(Console.ReadLine(), out int did2)) { Console.WriteLine("Неверно."); break; }
                    var itm = o.Items.FirstOrDefault(i => i.DishId == did2);
                    if (itm == null) { Console.WriteLine("Позиция не найдена."); break; }
                    o.Items.Remove(itm); SaveAll(); Console.WriteLine("Позиция удалена."); break;
                case "3":
                    Console.Write("ID блюда: ");
                    if (!int.TryParse(Console.ReadLine(), out int did3)) { Console.WriteLine("Неверно."); break; }
                    var it3 = o.Items.FirstOrDefault(i => i.DishId == did3);
                    if (it3 == null) { Console.WriteLine("Позиция не найдена."); break; }
                    Console.Write("Новое количество: ");
                    if (!int.TryParse(Console.ReadLine(), out int nq) || nq <= 0) { Console.WriteLine("Неверно."); break; }
                    it3.Quantity = nq; SaveAll(); Console.WriteLine("Количество обновлено."); break;
                case "0": loop = false; break;
                default: Console.WriteLine("Неизвестно."); break;
            }
        }
    }

    public void CloseOrder()
    {
        if (!Orders.Any()) { Console.WriteLine("Нет заказов."); return; }
        ShowAllOrders();
        Console.Write("Введите ID заказа для закрытия: ");
        if (!int.TryParse(Console.ReadLine(), out int id)) return;
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

    public void DeleteOrder()
    {
        if (!Orders.Any()) { Console.WriteLine("Нет заказов."); return; }
        ShowAllOrders();
        Console.Write("Введите ID заказа для удаления: ");
        if (!int.TryParse(Console.ReadLine(), out int id)) return;
        var o = Orders.FirstOrDefault(x => x.Id == id);
        if (o == null) { Console.WriteLine("Не найден."); return; }
        Orders.Remove(o);
        SaveAll();
        Console.WriteLine("Заказ удалён.");
    }

    // ---------- Statistics & checks ----------
    public void SumClosedOrders()
    {
        var sum = Orders.Where(o => o.IsClosed).Sum(o => o.Total);
        Console.WriteLine($"Сумма всех закрытых заказов: {sum:0.00} руб.");
    }

    // Пункт 21 — Чек клиента
    public void PrintClientCheck()
    {
        Console.Write("Введите ID клиента: ");
        if (!int.TryParse(Console.ReadLine(), out int clientId)) return;

        var clientName = Reservations.FirstOrDefault(r => r.ClientId == clientId)?.ClientName ?? "Неизвестно";
        var clientOrders = Orders.Where(o => o.ClientId == clientId).OrderBy(o => o.CreatedAt).ToList();
        if (!clientOrders.Any()) { Console.WriteLine("У клиента нет заказов."); return; }

        Console.WriteLine($"Имя клиента: {clientName}");
        decimal grandTotal = 0m;
        // собираем все позиции клиента в одном отчёте, группируем по категориям
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

        // учтём позиции с неизвестной категорией/удалённые блюда
        var unknowns = allItems.Where(x => x.dish == null).ToList();
        if (unknowns.Any())
        {
            Console.WriteLine("\nКатегория: Неизвестно (удалённые блюда):");
            decimal subtotal = 0m;
            foreach (var u in unknowns)
            {
                var qty = u.qty;
                var lineTotal = 0m;
                Console.WriteLine($"  Блюдо#{0}  {qty}*0 = 0.00 руб.");
                subtotal += lineTotal;
            }
            Console.WriteLine($"  Под_итог категории: {subtotal:0.00} руб.");
        }

        Console.WriteLine($"\nИтог счета: {grandTotal:0.00} руб.");
    }

    public void StatsDishCounts()
    {
        var counts = new Dictionary<int, int>();
        foreach (var o in Orders)
            foreach (var it in o.Items)
                counts[it.DishId] = counts.GetValueOrDefault(it.DishId) + it.Quantity;

        var sorted = counts.OrderByDescending(kv => kv.Value);
        Console.WriteLine("Статистика по заказанным блюдам:");
        foreach (var kv in sorted)
        {
            var dish = Dishes.FirstOrDefault(d => d.Id == kv.Key);
            Console.WriteLine($"{dish?.Name ?? "Блюдо#" + kv.Key} — {kv.Value} шт.");
        }
    }

    // ---------- Save/Load explicit ----------
    public void SaveDataInteractive()
    {
        try
        {
            SaveAll();
            Console.WriteLine($"Данные сохранены в папку: {DataPath}");
        }
        catch (Exception ex) { Console.WriteLine("Ошибка при сохранении: " + ex.Message); }
    }

    public void LoadDataInteractive()
    {
        try
        {
            LoadAll();
            Console.WriteLine($"Данные загружены из папки: {DataPath}");
        }
        catch (Exception ex) { Console.WriteLine("Ошибка при загрузке: " + ex.Message); }
    }

    // ---------- Utility ----------
    public void ShowNow() => Console.WriteLine($"Текущее виртуальное время: {VirtualNow:yyyy-MM-dd HH:mm}");
    public void SetVirtualNowInteractive()
    {
        Console.Write("Укажите новое текущее время (yyyy-MM-dd HH:mm) или Enter чтобы использовать системное текущее время: ");
        var s = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(s)) VirtualNow = DateTime.Now;
        else if (!DateTime.TryParse(s, out DateTime t)) { Console.WriteLine("Неверный формат."); return; }
        else VirtualNow = t;
        Console.WriteLine($"Сейчас: {VirtualNow:yyyy-MM-dd HH:mm}");
    }
}

class Program
{
    static void Main()
    {
        var mgr = new RestoranManager();

        // При запуске спрашиваем виртуальное время
        Console.WriteLine("=== Restoran (консольное приложение) ===");
        Console.Write("Укажите текущую дату и время (yyyy-MM-dd HH:mm) или Enter для системного времени: ");
        var input = Console.ReadLine();
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
            Console.WriteLine("24. Выбрать место хранения данных (папка)");
            Console.WriteLine("25. Сохранить данные (в текущую папку)");
            Console.WriteLine("26. Загрузить данные (из текущей папки)");
            Console.WriteLine("27. Показать/изменить текущее виртуальное время");
            Console.WriteLine("30. Создать тестовые данные (инициализация по выбору)");
            Console.WriteLine("0. Выход");

            Console.Write("Выберите действие: ");
            var cmd = Console.ReadLine();
            Console.WriteLine();
            try
            {
                switch (cmd)
                {
                    case "1": mgr.ShowAllTables(); break;
                    case "2": mgr.AddTable(); break;
                    case "3": mgr.EditTable(); break;
                    case "4": mgr.DeleteTable(); break;
                    case "5":
                        Console.Write("Введите ID стола: ");
                        if (int.TryParse(Console.ReadLine(), out int tid)) mgr.ShowAllTables(); // ShowAllTables уже показывает расписание
                        break;
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
                    case "30":
                        Console.Write("Вы уверены, что хотите создать тестовые данные (да/нет)? ");
                        var ans = Console.ReadLine();
                        if (ans != null && ans.ToLower().StartsWith("д")) mgr.InitDefaults();
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
