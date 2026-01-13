using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Config;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using MenuManager;

namespace C4Effects;

[MinimumApiVersion(80)]
public class C4Effects : BasePlugin, IPluginConfig<C4EffectsConfig>
{
    public override string ModuleName => "C4 Effects";
    public override string ModuleAuthor => "DoctorishHD";
    public override string ModuleVersion => "5.0.0";
    public override string ModuleDescription => "C4 bomb particle effects";

    private CEnvParticleGlow? _currentParticle;
    private readonly ILogger<C4Effects> _logger;
    private IMenuApi? _menuApi;
    private PlayerEffectsData _playerData = new();
    private string _playerDataPath = string.Empty;
    private readonly object _saveLock = new object();

    public C4EffectsConfig Config { get; set; } = new();

    private void LogInfo(string message)
    {
        if (Config.EnablePluginLogs)
            _logger.LogInformation(message);
    }

    private void LogWarning(string message)
    {
        _logger.LogWarning(message);
    }

    private void LogError(string message)
    {
        _logger.LogError(message);
    }

    public C4Effects(ILogger<C4Effects> logger)
    {
        _logger = logger;
    }

    public void OnConfigParsed(C4EffectsConfig config)
    {
        Config = config;
        LogInfo("C4 Effects config loaded");
    }

    public override void Load(bool hotReload)
    {
        LogInfo("C4 Effects plugin loaded!");
        LogInfo($"GameDirectory: {Server.GameDirectory}");
        
        // Инициализация пути для данных игроков
        _playerDataPath = GetPlayerDataPath();
        LogInfo($"Player data path: {_playerDataPath}");
        LogInfo($"Full player data path: {Path.GetFullPath(_playerDataPath)}");
        
        // Загрузка данных игроков
        LoadPlayerData();
        
        // Проверяем, существует ли конфиг, если нет - создаем
        string configPath = GetConfigPath();
        if (!File.Exists(configPath))
        {
            LogInfo("Config file not found, creating default...");
            SaveConfig();
        }

        // Register event handlers
        RegisterEventHandler<EventBombPlanted>(OnBombPlanted);
        RegisterEventHandler<EventBombExploded>(OnBombExploded);

        // Register listener for precache
        RegisterListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        try
        {
            PluginCapability<IMenuApi?> pluginCapability = new("menu:nfcore");
            _menuApi = pluginCapability.Get();
            LogInfo("MenuManager API loaded successfully");
        }
        catch (KeyNotFoundException)
        {
            LogWarning("MenuManager API not found, menus will not be available");
        }
    }

    [ConsoleCommand("css_c4menu", "Open C4 effects menu")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnC4MenuCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        // Проверяем доступ через флаг @css/c4bomb
        if (!AdminManager.PlayerHasPermissions(player, "@css/c4bomb"))
        {
            LogInfo($"Player {player.PlayerName} denied access to C4 menu (no @css/c4bomb flag)");
            player.PrintToChat(Localizer["c4effects.menu.no_access"]);
            return;
        }

        LogInfo($"Player {player.PlayerName} granted access to C4 menu");
        OpenEffectsMenu(player);
    }

    private void OpenEffectsMenu(CCSPlayerController player)
    {
        if (_menuApi == null)
        {
            player.PrintToChat(Localizer["c4effects.menu.disabled"]);
            return;
        }

        IMenu menu = _menuApi.GetMenu(Localizer["c4effects.menu.title"]);
        
        // Получаем текущий эффект игрока
        ulong steamId = player.AuthorizedSteamID?.SteamId64 ?? 0;
        string currentEffect;
        lock (_playerDataLock)
        {
            currentEffect = _playerData.GetPlayerEffect(steamId);
        }

        // Add "No effect" option
        string noEffectDisplay = Localizer["c4effects.menu.no_effect"];
        if (string.IsNullOrEmpty(currentEffect))
            noEffectDisplay += Localizer["c4effects.menu.active_suffix"];
        menu.AddMenuOption(noEffectDisplay, (player, _) =>
        {
            SelectEffect(player, "");
        });

        // Add all particle effects
        foreach (var kvp in Config.ParticleList)
        {
            string displayName = kvp.Key;
            if (kvp.Key == currentEffect)
                displayName += Localizer["c4effects.menu.active_suffix"];

            menu.AddMenuOption(displayName, (player, _) =>
            {
                SelectEffect(player, kvp.Key);
            });
        }

        menu.Open(player);
    }

    private void SelectEffect(CCSPlayerController player, string effectKey)
    {
        // If effectKey is empty, it's "no effect"
        if (!string.IsNullOrEmpty(effectKey) && !Config.ParticleList.ContainsKey(effectKey))
        {
            player.PrintToChat($"[C4Effects] Effect '{effectKey}' not found");
            return;
        }

        ulong steamId = player.AuthorizedSteamID?.SteamId64 ?? 0;
        if (steamId == 0)
        {
            player.PrintToChat("[C4Effects] Cannot identify your SteamID");
            return;
        }

        // Сохраняем эффект для игрока
        lock (_playerDataLock)
        {
            _playerData.SetPlayerEffect(steamId, effectKey);
        }
        SavePlayerData();
        
        LogInfo($"Player {player.PlayerName} (SteamID: {steamId}) selected effect {effectKey}");

        string message = string.IsNullOrEmpty(effectKey)
            ? Localizer["c4effects.menu.no_effect"]
            : effectKey;
        player.PrintToChat(Localizer["c4effects.menu.select_effect", message]);

        // Закройте меню и снова откройте, чтобы обновить метку "активно".
        if (_menuApi != null)
        {
            _menuApi.CloseMenu(player);
            OpenEffectsMenu(player);
        }
    }

    [ConsoleCommand("css_c4effects_reload", "Reload C4 Effects config")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnReloadConfig(CCSPlayerController? player, CommandInfo command)
    {
        try
        {
            LogInfo("Reloading C4 Effects config...");
            
            // Ручная перезагрузка конфига
            string configPath = GetConfigPath();
            LogInfo($"Reloading config from {configPath}");
            
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                var newConfig = JsonSerializer.Deserialize<C4EffectsConfig>(json) ?? new C4EffectsConfig();
                
                // Применяем новую конфигурацию
                Config = newConfig;
                OnConfigParsed(Config);
                
                LogInfo("Config reloaded successfully from file");
                command.ReplyToCommand(Localizer["c4effects.command.reload_success"]);
            }
            else
            {
                LogError("Config file not found");
                command.ReplyToCommand(Localizer["c4effects.command.reload_file_not_found"]);
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to reload config: {ex.Message}");
            command.ReplyToCommand(Localizer["c4effects.command.reload_failed"]);
        }
    }

    [ConsoleCommand("css_c4effects_reset", "Reset your C4 effect to default")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnResetEffect(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        ulong steamId = player.AuthorizedSteamID?.SteamId64 ?? 0;
        if (steamId == 0)
            return;

        lock (_playerDataLock)
        {
            _playerData.RemovePlayerEffect(steamId);
        }
        SavePlayerData();
        
        player.PrintToChat(Localizer["c4effects.command.reset_success"]);
        LogInfo($"Player {player.PlayerName} reset their C4 effect");
    }

    private string GetConfigPath()
    {
        // Используем стандартный путь CounterStrikeSharp для конфигов
        // configs/plugins/C4Effects/C4Effects.json относительно корня игры
        string basePath = Server.GameDirectory;
        
        // Если путь заканчивается на "game", это корневая директория CS2
        // Конфиги хранятся в csgo/addons/counterstrikesharp/configs/plugins/C4Effects/C4Effects.json
        if (basePath.EndsWith("game", StringComparison.OrdinalIgnoreCase))
        {
            // Заменяем "game" на "csgo" для пути к конфигам
            basePath = Path.Combine(Path.GetDirectoryName(basePath) ?? basePath, "csgo");
        }
        
        return Path.Combine(basePath, "addons/counterstrikesharp/configs/plugins/C4Effects/C4Effects.json");
    }

    private string GetPlayerDataPath()
    {
        // Данные игроков храним в корневой папке плагина
        string pluginDirectory = ModuleDirectory;
        LogInfo($"GetPlayerDataPath: ModuleDirectory = {pluginDirectory}");
        
        string path = Path.Combine(pluginDirectory, "player_effects.json");
        string fullPath = Path.GetFullPath(path);
        LogInfo($"GetPlayerDataPath: final path = {path}");
        LogInfo($"GetPlayerDataPath: full path = {fullPath}");
        return fullPath; // Возвращаем абсолютный путь
    }

    private async Task SaveConfigAsync()
    {
        try
        {
            string configPath = GetConfigPath();
            string? directory = Path.GetDirectoryName(configPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(configPath, json);
            LogInfo("Config saved successfully");
        }
        catch (Exception ex)
        {
            LogError($"Failed to save config: {ex.Message}");
        }
    }

    private void SaveConfig()
    {
        // Запускаем асинхронное сохранение в фоне
        _ = SaveConfigAsync();
    }

    private void LoadPlayerData()
    {
        // Синхронный вызов асинхронного метода
        LoadPlayerDataAsync().GetAwaiter().GetResult();
    }

    private void EnsureDataDirectory()
    {
        try
        {
            string? directory = Path.GetDirectoryName(_playerDataPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                LogInfo($"EnsureDataDirectory created: {directory}");
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to ensure data directory: {ex.Message}");
        }
    }

    private readonly SemaphoreSlim _saveSemaphore = new SemaphoreSlim(1, 1);
    private readonly object _playerDataLock = new object();

    private async Task SavePlayerDataAsync()
    {
        await _saveSemaphore.WaitAsync();
        string dataCopy;
        try
        {
            // Копируем данные для сериализации под lock, чтобы избежать изменения во время сериализации
            lock (_playerDataLock)
            {
                dataCopy = JsonSerializer.Serialize(_playerData, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        finally
        {
            _saveSemaphore.Release();
        }
        
        try
        {
            LogInfo($"SavePlayerDataAsync started, path: {_playerDataPath}");
            string? directory = Path.GetDirectoryName(_playerDataPath);
            LogInfo($"Directory to create: {directory}");
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                LogInfo($"Created directory: {directory}");
            }
            await File.WriteAllTextAsync(_playerDataPath, dataCopy);
            LogInfo($"Player data saved successfully to {_playerDataPath}");
            
            // Проверяем, что файл действительно создан
            if (File.Exists(_playerDataPath))
            {
                LogInfo($"File verification: {_playerDataPath} exists, size: {new FileInfo(_playerDataPath).Length} bytes");
            }
            else
            {
                LogError($"File verification failed: {_playerDataPath} does not exist after save!");
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to save player data: {ex.Message}");
            LogError($"Stack trace: {ex.StackTrace}");
        }
    }

    private void SavePlayerData()
    {
        // Запускаем асинхронное сохранение в фоне, не дожидаясь завершения
        _ = SavePlayerDataAsync();
    }

    private async Task LoadPlayerDataAsync()
    {
        try
        {
            if (File.Exists(_playerDataPath))
            {
                string json = await File.ReadAllTextAsync(_playerDataPath);
                _playerData = JsonSerializer.Deserialize<PlayerEffectsData>(json) ?? new PlayerEffectsData();
                LogInfo($"Loaded player data: {_playerData.PlayerEffects.Count} players");
            }
            else
            {
                _playerData = new PlayerEffectsData();
                LogInfo("Player data file not found, creating new");
                // Создаём директорию и сохраняем пустой файл
                EnsureDataDirectory();
                await SavePlayerDataAsync(); // Используем асинхронное сохранение
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to load player data: {ex.Message}");
            LogError($"Stack trace: {ex.StackTrace}");
            _playerData = new PlayerEffectsData();
        }
    }

    private void OnServerPrecacheResources(ResourceManifest manifest)
    {
        // Precache all particle effects from the list
        foreach (var kvp in Config.ParticleList)
        {
            manifest.AddResource(kvp.Value);
            LogInfo($"Precached particle effect: {kvp.Key} -> {kvp.Value}");
        }
    }

    private HookResult OnBombPlanted(EventBombPlanted @event, GameEventInfo info)
    {
        try
        {
            // Find planted bomb
            var bomb = Utilities.FindAllEntitiesByDesignerName<CPlantedC4>("planted_c4").FirstOrDefault();
            if (bomb == null || !bomb.IsValid)
                return HookResult.Continue;

            // Получаем игрока, который заложил бомбу
            var planter = @event.Userid;
            if (planter == null || !planter.IsValid)
                return HookResult.Continue;

            // Определяем какой эффект использовать
            string effectKey = string.Empty;
            ulong steamId = planter.AuthorizedSteamID?.SteamId64 ?? 0;
            
            if (steamId != 0)
            {
                // Пробуем получить персональный эффект игрока
                lock (_playerDataLock)
                {
                    effectKey = _playerData.GetPlayerEffect(steamId);
                }
                LogInfo($"Player {planter.PlayerName} (SteamID: {steamId}) planted bomb, effect: {effectKey}");
            }

            // Если у игрока нет персонального эффекта, используем глобальный
            if (string.IsNullOrEmpty(effectKey))
            {
                effectKey = Config.SelectedEffect;
                LogInfo($"Using global effect: {effectKey}");
            }

            // Если эффект пустой, пропускаем создание частицы
            if (string.IsNullOrEmpty(effectKey))
            {
                LogInfo(Localizer["c4effects.particle.no_effect"]);
                return HookResult.Continue;
            }

            // Get selected effect path
            if (!Config.ParticleList.TryGetValue(effectKey, out string? effectPath) || string.IsNullOrEmpty(effectPath))
            {
                LogError($"Effect '{effectKey}' not found in particle list");
                return HookResult.Continue;
            }

            // Create particle glow entity
            _currentParticle = Utilities.CreateEntityByName<CEnvParticleGlow>("env_particle_glow");
            if (_currentParticle == null || !_currentParticle.IsValid)
            {
                LogError("Failed to create env_particle_glow entity");
                return HookResult.Continue;
            }

            // Set particle effect
            _currentParticle.EffectName = effectPath;
            _currentParticle.StartActive = Config.StartActive;
            _currentParticle.DispatchSpawn();

            // Apply scale, alpha, selfillum via ControlPoint 17 (X=alpha, Y=scale, Z=selfillum)
            try
            {
                _currentParticle.AcceptInput("SetControlPoint", _currentParticle, _currentParticle, $"17 {Config.AlphaScale} {Config.Scale} {Config.SelfIllumScale}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to apply particle settings: {ex.Message}");
            }

            // Attach to bomb
            _currentParticle.AcceptInput("FollowEntity", bomb, _currentParticle, "!activator");

            LogInfo(Localizer["c4effects.particle.created", effectKey]);
        }
        catch (Exception ex)
        {
            LogError($"Error in OnBombPlanted: {ex.Message}");
        }

        return HookResult.Continue;
    }

    private HookResult OnBombExploded(EventBombExploded @event, GameEventInfo info)
    {
        if (_currentParticle == null || !_currentParticle.IsValid)
            return HookResult.Continue;

        // Remove particle
        _currentParticle.Remove();
        _currentParticle = null;
        LogInfo(Localizer["c4effects.particle.removed"]);
        return HookResult.Continue;
    }
}