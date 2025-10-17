using DriverDeploy.Shared.Models;
using DriverDeploy.Shared.Services;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DriverDeploy.Server.Services;
using System.Windows;

namespace DriverDeploy.Server {
  public partial class MainWindow : Window {
    public ObservableCollection<MachineInfo> Machines { get; } = new ObservableCollection<MachineInfo>();
    public ObservableCollection<DriverInfo> CurrentMachineDrivers { get; } = new ObservableCollection<DriverInfo>();
    public ObservableCollection<DeviceDescriptor> CurrentMachineDevices { get; } = new();

    private MachineInfo _selectedMachine;
    private LocalDriverService _driverRepoService;

    private string localIP;

    public MainWindow() {

      InitializeComponent();

       localIP = Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString();

      // Инициализация сервиса репозитория драйверов
      _driverRepoService = new LocalDriverService(localIP); // Замени на IP VM3

      MachinesListView.ItemsSource = Machines;
      DriversListView.ItemsSource = CurrentMachineDrivers;
      DevicesListView.ItemsSource = CurrentMachineDevices;

      // Загружаем базу драйверов при запуске
      _ = LoadDriverRepositoryAsync();
    }

    private async Task LoadDriverRepositoryAsync() {
      try {
        ResultText.Text = "📥 Загружаем базу драйверов из репозитория...";

        // Добавляем отладочную информацию
        Console.WriteLine("🔄 Начинаем загрузку драйверов из репозитория...");

        var mapping = await _driverRepoService.LoadDriverMappingAsync();

        // Логируем результат
        Console.WriteLine($"✅ Загружено драйверов: {mapping.Drivers?.Count ?? 0}");
        if (mapping.Drivers != null) {
          foreach (var driver in mapping.Drivers) {
            Console.WriteLine($"   - {driver.Name} (HWIDs: {string.Join(", ", driver.HardwareIds)})");
          }
        }

        RepoStatusText.Text = $"✅ {mapping.Drivers?.Count ?? 0} драйверов";
        ResultText.Text = $"✅ База драйверов успешно загружена. Доступно {mapping.Drivers?.Count ?? 0} драйверов";
      }
      catch (Exception ex) {
        // Детальное логирование ошибки
        Console.WriteLine($"❌ Ошибка загрузки базы драйверов: {ex}");
        RepoStatusText.Text = "❌ Ошибка загрузки базы драйверов";
        ResultText.Text = $"❌ Ошибка загрузки базы драйверов: {ex.Message}";
      }
    }
    private async void RefreshRepoButton_Click(object sender, RoutedEventArgs e) {
      await LoadDriverRepositoryAsync();
    }

    // === СЕТЕВОЕ СКАНИРОВАНИЕ ===
    private async void ScanButton_Click(object sender, RoutedEventArgs e) {
      ScanButton.IsEnabled = false;
      ResultText.Text = "🔍 Сканирование сети...";
      ScanProgress.Visibility = Visibility.Visible;
      Machines.Clear();

      try {
        var ipRange = MachineScanner.GetIPRangeWithLocalhost();
        ResultText.Text = $"🔍 Сканируем {ipRange.Count} адресов...";

        int foundCount = 0;

        var options = new ParallelOptions { MaxDegreeOfParallelism = 10 };
        await Task.Run(() =>
        {
          Parallel.ForEach(ipRange, options, ip =>
          {
            var pingTask = MachineScanner.IsMachineOnline(ip);
            pingTask.Wait();

            if (pingTask.Result) {
              var checkTask = CheckForAgent(ip);
              checkTask.Wait();

              if (checkTask.Result != null) {
                Application.Current.Dispatcher.Invoke(() =>
                {
                  Machines.Add(checkTask.Result);
                  foundCount++;
                  MachinesStatusText.Text = $"Найдено машин: {foundCount}";
                  ResultText.Text = $"🔍 Найдено машин: {foundCount}. Проверяем {ip}...";
                });
              }
            }
          });
        });

        ResultText.Text = $"✅ Сканирование завершено. Найдено {foundCount} машин с агентом.";
      }
      catch (Exception ex) {
        ResultText.Text = $"❌ Ошибка сканирования: {ex.Message}";
      }
      finally {
        ScanProgress.Visibility = Visibility.Collapsed;
        ScanButton.IsEnabled = true;
      }
    }

    // === АВТОМАТИЧЕСКОЕ ОБНОВЛЕНИЕ ДРАЙВЕРОВ ===
    private async void UpdateDrivers_Click(object sender, RoutedEventArgs e) {
      if (MachinesListView.SelectedItem is MachineInfo selectedMachine) {
        UpdateDriversButton.IsEnabled = false;
        ScanProgress.Visibility = Visibility.Visible;

        try {
          await AutoUpdateDriversForMachine(selectedMachine);
        }
        finally {
          UpdateDriversButton.IsEnabled = true;
          ScanProgress.Visibility = Visibility.Collapsed;
        }
      } else {
        ResultText.Text = "❌ Выберите машину из списка для автоматического обновления драйверов";
      }
    }

    private async Task<bool> AutoUpdateDriversForMachine(MachineInfo machine) {
      try {
        ResultText.Text = $"🎯 Начинаем автоматическое обновление драйверов для {machine.MachineName}...";

        // Обновляем кэш репозитория
        await _driverRepoService.RefreshCacheIfNeededAsync();

        // Сканируем устройства если еще не сканировали
        if (!CurrentMachineDevices.Any()) {
          ResultText.Text = $"🔍 Сканируем устройства на {machine.MachineName}...";
          await ScanDevicesForMachine(machine);
        }

        // Анализируем устройства и находим нужные драйверы
        ResultText.Text = $"🔧 Анализируем {CurrentMachineDevices.Count} устройств...";
        var devicesNeedingDrivers = new List<DeviceDescriptor>();
        var driverPackagesMap = new Dictionary<DeviceDescriptor, DriverPackage>();

        foreach (var device in CurrentMachineDevices) {
          // Пропускаем устройства которые уже имеют драйвер
          if (!string.IsNullOrEmpty(device.DriverVersion) && device.DriverVersion != "Unknown")
            continue;

          // Ищем драйвер в репозитории по HardwareID
          var repoDriver = _driverRepoService.FindDriverForDevice(device);
          if (repoDriver != null) {
            devicesNeedingDrivers.Add(device);
            driverPackagesMap[device] = _driverRepoService.ConvertToDriverPackage(repoDriver);
          }
        }

        if (!devicesNeedingDrivers.Any()) {
          ResultText.Text = $"✅ На {machine.MachineName} все драйверы актуальны!";
          return true;
        }

        // Устанавливаем найденные драйверы
        ResultText.Text = $"🚀 Устанавливаем {devicesNeedingDrivers.Count} драйверов...";
        int successCount = 0;
        int totalCount = devicesNeedingDrivers.Count;

        for (int i = 0; i < devicesNeedingDrivers.Count; i++) {
          var device = devicesNeedingDrivers[i];
          var driverPackage = driverPackagesMap[device];

          ResultText.Text = $"📦 [{i + 1}/{totalCount}] Устанавливаем {driverPackage.Name} для {device.Name}...";

          var success = await DeployDriverToMachine(machine, driverPackage);
          if (success) {
            successCount++;
            // Обновляем статус устройства после успешной установки
            device.DriverVersion = driverPackage.Version;
          }

          // Небольшая пауза между установками
          await Task.Delay(1000);
        }

        // Обновляем отображение
        DevicesListView.Items.Refresh();

        ResultText.Text = $"🎉 Автообновление завершено! Успешно: {successCount}/{totalCount} драйверов";
        return successCount > 0;
      }
      catch (Exception ex) {
        ResultText.Text = $"❌ Ошибка автообновления: {ex.Message}";
        return false;
      }
    }

    // === МАССОВОЕ ОБНОВЛЕНИЕ ВСЕХ МАШИН ===
    private async void UpdateAllMachinesButton_Click(object sender, RoutedEventArgs e) {
      if (!Machines.Any()) {
        ResultText.Text = "❌ Сначала выполните сканирование сети";
        return;
      }

      UpdateAllMachinesButton.IsEnabled = false;
      ScanProgress.Visibility = Visibility.Visible;

      try {
        int totalMachines = Machines.Count;
        int updatedMachines = 0;

        foreach (var machine in Machines) {
          ResultText.Text = $"🔄 Обновляем {machine.MachineName} ({updatedMachines + 1}/{totalMachines})...";

          var success = await AutoUpdateDriversForMachine(machine);
          if (success) {
            updatedMachines++;
          }

          // Пауза между машинами
          await Task.Delay(2000);
        }

        ResultText.Text = $"🎉 Массовое обновление завершено! Обработано {updatedMachines}/{totalMachines} машин";
      }
      finally {
        UpdateAllMachinesButton.IsEnabled = true;
        ScanProgress.Visibility = Visibility.Collapsed;
      }
    }

    // === СКАНИРОВАНИЕ УСТРОЙСТВ ===
    private async void ScanDevicesButton_Click(object sender, RoutedEventArgs e) {
      if (MachinesListView.SelectedItem is MachineInfo selectedMachine) {
        await ScanDevicesForMachine(selectedMachine);
      } else {
        ResultText.Text = "❌ Сначала выберите машину из списка.";
      }
    }

    private async Task ScanDevicesForMachine(MachineInfo machine) {
      try {
        ResultText.Text = $"🧭 Получаем список устройств на {machine.MachineName}...";
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        var response = await client.GetAsync($"http://{machine.IpAddress}:8080/api/devices");
        if (response.IsSuccessStatusCode) {
          var json = await response.Content.ReadAsStringAsync();
          var devices = JsonConvert.DeserializeObject<DeviceDescriptor[]>(json);

          Application.Current.Dispatcher.Invoke(() =>
          {
            CurrentMachineDevices.Clear();
            foreach (var device in devices) {
              CurrentMachineDevices.Add(device);
            }
            DevicesStatusText.Text = $"Устройств: {devices.Length}";
          });

          ResultText.Text = $"✅ Найдено {devices.Length} устройств на {machine.MachineName}";
        } else {
          ResultText.Text = $"❌ Ошибка получения устройств: {response.StatusCode}";
        }
      }
      catch (Exception ex) {
        ResultText.Text = $"❌ Ошибка: {ex.Message}";
      }
    }

    // === УСТАНОВКА ДРАЙВЕРА НА МАШИНУ ===
    private async Task<bool> DeployDriverToMachine(MachineInfo machine, DriverPackage driverPackage) {
      try {
        ResultText.Text = $"🚀 Устанавливаем {driverPackage.Name} на {machine.MachineName}...";
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(60); // Увеличиваем таймаут для скачивания и установки

        var json = JsonConvert.SerializeObject(driverPackage);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"http://{machine.IpAddress}:8080/api/drivers/install", content);
        if (response.IsSuccessStatusCode) {
          var resultJson = await response.Content.ReadAsStringAsync();
          var result = JsonConvert.DeserializeObject<InstallationResult>(resultJson);

          if (result.Success) {
            ResultText.Text = $"✅ {result.Message}";
            return true;
          } else {
            ResultText.Text = $"⚠️ {result.Message}";
            return false;
          }
        } else {
          ResultText.Text = $"❌ Ошибка установки: {response.StatusCode}";
          return false;
        }
      }
      catch (Exception ex) {
        ResultText.Text = $"❌ Ошибка развертывания: {ex.Message}";
        return false;
      }
    }

    // === ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ===
    private async void MachinesListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
      if (MachinesListView.SelectedItem is MachineInfo machine) {
        _selectedMachine = machine;
        SelectedMachineText.Text = $"{machine.MachineName} ({machine.IpAddress})";

        // Автоматически загружаем устройства при выборе машины
        await ScanDevicesForMachine(machine);
      }
    }

    private async Task<MachineInfo?> CheckForAgent(string ip) {
      try {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(2);

        var response = await client.GetAsync($"http://{ip}:8080/api/ping");
        if (response.IsSuccessStatusCode) {
          var json = await response.Content.ReadAsStringAsync();
          var machineInfo = JsonConvert.DeserializeObject<MachineInfo>(json);
          return machineInfo;
        }
      }
      catch {
        // Игнорируем ошибки - значит агента нет на этой машине
      }
      return null;
    }

    private async void RefreshDriversButton_Click(object sender, RoutedEventArgs e) {
      if (_selectedMachine != null) {
        await ScanDriversForMachine(_selectedMachine);
      } else {
        ResultText.Text = "❌ Сначала выберите машину из списка";
      }
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e) {
      if (_selectedMachine != null) {
        await CheckForDriverUpdates(_selectedMachine);
      } else {
        ResultText.Text = "❌ Сначала выберите машину из списка";
      }
    }

    // Старые методы для совместимости (можно удалить позже)
    private async Task ScanDriversForMachine(MachineInfo machine) { /* ... */ }
    private async Task CheckForDriverUpdates(MachineInfo machine) { /* ... */ }
    private void InstallDriverButton_Click(object sender, RoutedEventArgs e) { /* ... */ }
    private void DebugButton_Click(object sender, RoutedEventArgs e) { /* ... */ }
  }
}