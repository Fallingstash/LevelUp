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
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DriverDeploy.Server {
  public partial class MainWindow : Window {
    public ObservableCollection<MachineInfo> Machines { get; } = new ObservableCollection<MachineInfo>();
    public ObservableCollection<DriverInfo> CurrentMachineDrivers { get; } = new ObservableCollection<DriverInfo>();
    public ObservableCollection<DeviceDescriptor> CurrentMachineDevices { get; } = new();

    private MachineInfo _selectedMachine;
    private LocalDriverService _driverRepoService;
    private Dictionary<string, string> _updatedDeviceVersions = new Dictionary<string, string>();

    private string localIP;

    public MainWindow() {

      InitializeComponent();

            localIP = Dns.GetHostAddresses(Dns.GetHostName())
                    .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString();
            var repoUrl = "http://localhost:5000"; 

            Console.WriteLine($"🎯 Используем репозиторий: {repoUrl}");
            _driverRepoService = new LocalDriverService(repoUrl); 

            MachinesListView.ItemsSource = Machines;
      DevicesListView.ItemsSource = CurrentMachineDevices;

      _ = LoadDriverRepositoryAsync();
    }

        private async Task LoadDriverRepositoryAsync()
        {
            try
            {
                ResultText.Text = "📥 Загружаем базу драйверов из репозитория...";
                Console.WriteLine("🔄 Начинаем загрузку драйверов из репозитория...");

                var mapping = await _driverRepoService.LoadDriverMappingAsync();

                var source = mapping.Drivers?.Any(d => d.Url.Contains("downloadmirror.intel.com")) == true
                    ? "FALLBACK (из кода)"
                    : "HTTP РЕПОЗИТОРИЙ";

                Console.WriteLine($"📊 Источник драйверов: {source}");
                Console.WriteLine($"✅ Загружено драйверов: {mapping.Drivers?.Count ?? 0}");

                RepoStatusText.Text = $"✅ {mapping.Drivers?.Count ?? 0} драйверов ({source})";
                ResultText.Text = $"✅ База драйверов загружена из {source}. Доступно {mapping.Drivers?.Count ?? 0} драйверов";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка загрузки базы драйверов: {ex}");
                RepoStatusText.Text = "❌ Ошибка загрузки";
                ResultText.Text = $"❌ Ошибка загрузки базы драйверов: {ex.Message}";
            }
        }
        private async void RefreshRepoButton_Click(object sender, RoutedEventArgs e) {
      await LoadDriverRepositoryAsync();
    }
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

        private async Task<bool> AutoUpdateDriversForMachine(MachineInfo machine)
        {
            try
            {
                ResultText.Text = $"🎯 Начинаем автоматическое обновление драйверов для {machine.MachineName}...";

                await _driverRepoService.RefreshCacheIfNeededAsync();

                if (!CurrentMachineDevices.Any())
                {
                    ResultText.Text = $"🔍 Сканируем устройства на {machine.MachineName}...";
                    await ScanDevicesForMachine(machine);
                }

                ResultText.Text = $"🔧 Анализируем {CurrentMachineDevices.Count} устройств...";
                var devicesNeedingDrivers = new List<DeviceDescriptor>();
                var driverPackagesMap = new Dictionary<DeviceDescriptor, DriverPackage>();

                foreach (var device in CurrentMachineDevices)
                {
                    Console.WriteLine($"🔍 Анализ устройства: {device.Name}");
                    Console.WriteLine($"   Версия драйвера: '{device.DriverVersion}'");
                    Console.WriteLine($"   HardwareIDs: {string.Join(", ", device.HardwareIds)}");

                    var repoDriver = _driverRepoService.FindDriverForDevice(device);
                    if (repoDriver != null)
                    {
                        Console.WriteLine($"✅ Найден драйвер для обновления: {repoDriver.Name}");
                        devicesNeedingDrivers.Add(device);
                        driverPackagesMap[device] = _driverRepoService.ConvertToDriverPackage(repoDriver);
                    }
                    else
                    {
                        Console.WriteLine($"❌ Драйвер не найден или не требует обновления");
                    }
                }

                Console.WriteLine($"📊 Итог: {devicesNeedingDrivers.Count} устройств требуют обновления");

                if (!devicesNeedingDrivers.Any())
                {
                    ResultText.Text = $"✅ На {machine.MachineName} все драйверы актуальны!";
                    return true;
                }

                ResultText.Text = $"🚀 Устанавливаем {devicesNeedingDrivers.Count} драйверов...";
                int successCount = 0;
                int totalCount = devicesNeedingDrivers.Count;

                for (int i = 0; i < devicesNeedingDrivers.Count; i++)
                {
                    var device = devicesNeedingDrivers[i];
                    var driverPackage = driverPackagesMap[device];

                    ResultText.Text = $"📦 [{i + 1}/{totalCount}] Устанавливаем {driverPackage.Name} для {device.Name}...";

                    var success = await DeployDriverToMachine(machine, driverPackage);
                    if (success)
                    {
                        successCount++;
                        device.DriverVersion = driverPackage.Version;
                    }

                    await Task.Delay(1000);
                }

                DevicesListView.Items.Refresh();

                ResultText.Text = $"🎉 Автообновление завершено! Успешно: {successCount}/{totalCount} драйверов";
                return successCount > 0;
            }
            catch (Exception ex)
            {
                ResultText.Text = $"❌ Ошибка автообновления: {ex.Message}";
                return false;
            }
        }  

    private async void ScanDevicesButton_Click(object sender, RoutedEventArgs e) {
      if (MachinesListView.SelectedItem is MachineInfo selectedMachine) {
        await ScanDevicesForMachine(selectedMachine);
      } else {
        ResultText.Text = "❌ Сначала выберите машину из списка.";
      }
    }

        private async Task ScanDevicesForMachine(MachineInfo machine)
        {
            try
            {
                ResultText.Text = $"🧭 Получаем список устройств на {machine.MachineName}...";
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                var response = await client.GetAsync($"http://{machine.IpAddress}:8080/api/devices");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var devices = JsonConvert.DeserializeObject<DeviceDescriptor[]>(json);

                    Application.Current.Dispatcher.Invoke(() => {
                        CurrentMachineDevices.Clear();
                        foreach (var device in devices)
                        {
                            if (_updatedDeviceVersions.TryGetValue(device.PnpDeviceId, out var updatedVersion))
                            {
                                device.DriverVersion = updatedVersion;
                                Console.WriteLine($"🔄 Восстановлена обновленная версия для {device.Name}: {updatedVersion}");
                            }

                            CurrentMachineDevices.Add(device);
                        }
                        DevicesStatusText.Text = $"Устройств: {devices.Length}";

                        UpdateDevicesDisplay();
                    });

                    ResultText.Text = $"✅ Найдено {devices.Length} устройств на {machine.MachineName}";
                }
                else
                {
                    ResultText.Text = $"❌ Ошибка получения устройств: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                ResultText.Text = $"❌ Ошибка: {ex.Message}";
            }
        }

        private async Task<bool> DeployDriverToMachine(MachineInfo machine, DriverPackage driverPackage)
        {
            try
            {
                ResultText.Text = $"🚀 Устанавливаем {driverPackage.Name} на {machine.MachineName}...";
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(60);

                var json = JsonConvert.SerializeObject(driverPackage);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await client.PostAsync($"http://{machine.IpAddress}:8080/api/drivers/install", content);
                if (response.IsSuccessStatusCode)
                {
                    var resultJson = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<InstallationResult>(resultJson);

                    if (result.Success)
                    {
                        ResultText.Text = $"✅ {result.Message}";

                        await UpdateDeviceDriverVersion(machine, driverPackage);
                        return true;
                    }
                    else
                    {
                        ResultText.Text = $"⚠️ {result.Message}";
                        return false;
                    }
                }
                else
                {
                    ResultText.Text = $"❌ Ошибка установки: {response.StatusCode}";
                    return false;
                }
            }
            catch (Exception ex)
            {
                ResultText.Text = $"❌ Ошибка развертывания: {ex.Message}";
                return false;
            }
        }

        private async Task UpdateDeviceDriverVersion(MachineInfo machine, DriverPackage driverPackage)
        {
            try
            {
                var targetDevice = CurrentMachineDevices.FirstOrDefault(device =>
                    _driverRepoService.FindDriverForDevice(device)?.Name == driverPackage.Name);

                if (targetDevice != null)
                {
                    Console.WriteLine($"🔄 Обновляем статус устройства: {targetDevice.Name} -> {driverPackage.Version}");

                    _updatedDeviceVersions[targetDevice.PnpDeviceId] = driverPackage.Version;
                    Console.WriteLine($"💾 Сохранена версия в кэш: {targetDevice.PnpDeviceId} -> {driverPackage.Version}");

                    targetDevice.DriverVersion = driverPackage.Version;
                    targetDevice.NeedsUpdate = false;

                    DevicesListView.Items.Refresh();
                    ResultText.Text += $"\n✅ Обновлен статус устройства: {targetDevice.Name}";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка обновления статуса: {ex.Message}");
            }
        }

        private async void MachinesListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
      if (MachinesListView.SelectedItem is MachineInfo machine) {
        _selectedMachine = machine;
        SelectedMachineText.Text = $"{machine.MachineName} ({machine.IpAddress})";

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

    private async Task ScanDriversForMachine(MachineInfo machine) { /* ... */ }
    private async Task CheckForDriverUpdates(MachineInfo machine) { /* ... */ }
    private void InstallDriverButton_Click(object sender, RoutedEventArgs e) { /* ... */ }
    private void DebugButton_Click(object sender, RoutedEventArgs e) { /* ... */ }

        private void UpdateDevicesDisplay()
        {
            foreach (var device in CurrentMachineDevices)
            {
                var repoDriver = _driverRepoService.FindDriverForDevice(device);

                if (_updatedDeviceVersions.ContainsKey(device.PnpDeviceId))
                {
                    device.NeedsUpdate = false;
                    Console.WriteLine($"✅ Устройство {device.Name} уже обновлялось, статус: АКТУАЛЬНЫЙ");
                }
                else
                {
                    device.NeedsUpdate = (repoDriver != null);
                    Console.WriteLine($"🔍 Устройство {device.Name} требует обновления: {device.NeedsUpdate}");
                }
            }
            DevicesListView.Items.Refresh();
        }

        public class BoolToColorConverter : IValueConverter
        {
            public static BoolToColorConverter Instance = new BoolToColorConverter();

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is bool needsUpdate && needsUpdate)
                {
                    return new SolidColorBrush(Color.FromRgb(0x80, 0x04, 0xFF)); 
                }
                return new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x77));
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        public class BoolToTextConverter : IValueConverter
        {
            public static BoolToTextConverter Instance = new BoolToTextConverter();

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is bool needsUpdate && needsUpdate)
                {
                    return "ТРЕБУЕТ ОБНОВЛЕНИЯ";
                }
                return "АКТУАЛЬНЫЙ";
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }
    }
}