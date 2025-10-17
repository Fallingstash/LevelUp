using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Настройка порта
app.Urls.Add("http://localhost:5000");

// Создаем папку Drivers если не существует
var driversPath = Path.Combine(Directory.GetCurrentDirectory(), "Drivers");
if (!Directory.Exists(driversPath)) {
  Directory.CreateDirectory(driversPath);
  Console.WriteLine($"📁 Создана папка для драйверов: {driversPath}");
}

// Проверяем существование drivers.json
var driversJsonPath = Path.Combine(driversPath, "drivers.json");
Console.WriteLine($"🔍 Проверяем файл: {driversJsonPath}");
Console.WriteLine($"📄 Файл существует: {File.Exists(driversJsonPath)}");

if (!File.Exists(driversJsonPath)) {
  Console.WriteLine("⚠️ Файл drivers.json не найден! Создаем тестовый...");
  var testDrivers = new {
    drivers = new[]
      {
            new
            {
                name = "NVIDIA Graphics Driver (Test)",
                version = "456.71",
                description = "Тестовый драйвер NVIDIA для демонстрации",
                hardwareIds = new[] { "PCI\\\\VEN_10DE&DEV_1C03", "PCI\\\\VEN_10DE&DEV_1C82" },
                url = "/nvidia/test_driver.exe",
                installArgs = "/S /quiet",
                sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                osRequirements = new[] { "Windows 10", "Windows 11" },
                architecture = new[] { "x64" }
            }
        }
  };

  File.WriteAllText(driversJsonPath,
      System.Text.Json.JsonSerializer.Serialize(testDrivers, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
  Console.WriteLine($"✅ Создан тестовый drivers.json");
}

// Настройка статических файлов
app.UseStaticFiles(new StaticFileOptions {
  FileProvider = new PhysicalFileProvider(driversPath),
  RequestPath = ""
});

// Эндпоинт для drivers.json с диагностикой
app.MapGet("/drivers.json", () => {
  try {
    Console.WriteLine($"📥 Запрос к /drivers.json");
    var jsonPath = Path.Combine(driversPath, "drivers.json");

    if (!File.Exists(jsonPath)) {
      Console.WriteLine("❌ Файл drivers.json не найден!");
      return Results.NotFound("drivers.json not found");
    }

    var content = File.ReadAllText(jsonPath);
    Console.WriteLine($"✅ Файл найден, размер: {content.Length} символов");
    return Results.File(jsonPath, "application/json");
  }
  catch (Exception ex) {
    Console.WriteLine($"❌ Ошибка при чтении drivers.json: {ex.Message}");
    return Results.Problem($"Error reading drivers.json: {ex.Message}");
  }
});

app.MapGet("/", () => "Driver Repository Server is running!");
app.MapGet("/health", () => new { status = "OK", version = "8.0", service = "Driver Repository" });

Console.WriteLine("🚀 Driver Repository запущен на .NET 8.0");
Console.WriteLine("📍 Адрес: http://localhost:5000");
Console.WriteLine("📁 Папка драйверов: " + driversPath);

app.Run();
