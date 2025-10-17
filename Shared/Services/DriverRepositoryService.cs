using DriverDeploy.Shared.Models;
using Newtonsoft.Json;

public class DriverRepositoryService {
  private readonly HttpClient _httpClient;
  private readonly string _repositoryBaseUrl;

  // Теперь указываем URL репозитория явно
  public DriverRepositoryService(string repositoryUrl = "http://localhost:8080") {
    _httpClient = new HttpClient();
    _repositoryBaseUrl = repositoryUrl.TrimEnd('/');
    _httpClient.Timeout = TimeSpan.FromSeconds(30);
  }

  public async Task<RepoDriverMapping> LoadDriverMappingAsync() {
    try {
      // Загружаем из отдельного сервера
      var json = await _httpClient.GetStringAsync($"{_repositoryBaseUrl}/drivers.json");
      var mapping = JsonConvert.DeserializeObject<RepoDriverMapping>(json);
      return mapping ?? new RepoDriverMapping();
    }
    catch (Exception ex) {
      throw new Exception($"Не удалось загрузить drivers.json из {_repositoryBaseUrl}: {ex.Message}");
    }
  }
}