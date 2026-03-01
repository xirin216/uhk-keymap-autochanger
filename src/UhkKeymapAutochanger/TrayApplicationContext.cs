using System.Drawing;
using UhkKeymapAutochanger.Core.Models;
using UhkKeymapAutochanger.Core.Services;
using UhkKeymapAutochanger.Core.Settings;
using UhkKeymapAutochanger.Diagnostics;
using UhkKeymapAutochanger.Services;
using UhkKeymapAutochanger.UI;

namespace UhkKeymapAutochanger;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IDebugLogger _logger;
    private readonly StartupRegistrationService _startupRegistrationService;
    private readonly KeymapRoutingService _routingService;
    private readonly ForegroundProcessWatcher _foregroundProcessWatcher;
    private readonly UhkHidTransport _hidTransport;
    private readonly KeymapSwitchingService _switchingService;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly ToolStripMenuItem _toggleSwitchingMenuItem;
    private readonly ToolStripMenuItem _startWithWindowsMenuItem;

    private AppConfig _config;
    private string _lastSwitchingStatus = "Initializing...";
    private bool _switchingEnabled = true;
    private bool _suppressStartWithWindowsCheckedChanged;

    public TrayApplicationContext(
        AppConfig config,
        ISettingsRepository settingsRepository,
        IDebugLogger logger)
    {
        _settingsRepository = settingsRepository;
        _logger = logger;
        _config = SettingsValidator.Validate(config).NormalizedConfig;

        _startupRegistrationService = new StartupRegistrationService(_logger);
        _routingService = new KeymapRoutingService(_config);
        _foregroundProcessWatcher = new ForegroundProcessWatcher(_config.PollIntervalMs);
        _hidTransport = new UhkHidTransport(reportId: 4, reconnectInterval: TimeSpan.FromSeconds(2), logger: _logger);
        _switchingService = new KeymapSwitchingService(
            _foregroundProcessWatcher,
            _routingService,
            _hidTransport,
            _logger,
            _config.PauseWhenUhkAgentRunning);
        _switchingService.StatusChanged += OnSwitchingStatusChanged;

        var contextMenu = new ContextMenuStrip();
        _statusMenuItem = new ToolStripMenuItem("Status: Initializing...")
        {
            Enabled = false,
        };
        contextMenu.Items.Add(_statusMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(new ToolStripMenuItem("Open Settings", null, (_, _) => OpenSettings()));
        _toggleSwitchingMenuItem = new ToolStripMenuItem("Stop Switching", null, (_, _) => ToggleSwitching());
        contextMenu.Items.Add(_toggleSwitchingMenuItem);

        _startWithWindowsMenuItem = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
        };
        _startWithWindowsMenuItem.CheckedChanged += OnStartWithWindowsCheckedChanged;
        contextMenu.Items.Add(_startWithWindowsMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()));

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "UHK Keymap Autochanger",
            ContextMenuStrip = contextMenu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => OpenSettings();

        ApplyStartupRegistration(_config.StartWithWindows);
        _switchingService.Start();
    }

    protected override void ExitThreadCore()
    {
        _switchingService.StatusChanged -= OnSwitchingStatusChanged;
        _switchingService.Stop();
        _switchingService.Dispose();
        _hidTransport.Dispose();
        _foregroundProcessWatcher.Dispose();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        base.ExitThreadCore();
    }

    private void ToggleSwitching()
    {
        if (_switchingEnabled)
        {
            _switchingService.Stop();
            _toggleSwitchingMenuItem.Text = "Start Switching";
            _switchingEnabled = false;
            _logger.Log("Switching stopped.");
            return;
        }

        _switchingService.Start();
        _toggleSwitchingMenuItem.Text = "Stop Switching";
        _switchingEnabled = true;
        _logger.Log("Switching started.");
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_config, _lastSwitchingStatus);
        EventHandler<string>? statusHandler = (_, status) =>
        {
            if (form.IsDisposed || !form.IsHandleCreated)
            {
                return;
            }

            void UpdateStatus()
            {
                form.UpdateRuntimeStatus(status);
            }

            if (form.InvokeRequired)
            {
                form.BeginInvoke((MethodInvoker)UpdateStatus);
                return;
            }

            UpdateStatus();
        };

        _switchingService.StatusChanged += statusHandler;
        try
        {
            if (form.ShowDialog() != DialogResult.OK || form.SavedConfig is null)
            {
                return;
            }

            ApplyConfig(form.SavedConfig);
        }
        finally
        {
            _switchingService.StatusChanged -= statusHandler;
        }
    }

    private void ApplyConfig(AppConfig config)
    {
        try
        {
            var validated = SettingsValidator.Validate(config);
            if (!validated.IsValid)
            {
                throw new InvalidDataException(string.Join(Environment.NewLine, validated.Errors));
            }

            _config = validated.NormalizedConfig;
            _settingsRepository.Save(_config);
            ApplyStartupRegistration(_config.StartWithWindows);
            _switchingService.ApplyConfig(_config);
            _logger.Log("Config updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to apply config: {ex.Message}");
            MessageBox.Show(
                $"Failed to save settings.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "UHK Keymap Autochanger",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OnStartWithWindowsCheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressStartWithWindowsCheckedChanged)
        {
            return;
        }

        try
        {
            _config.StartWithWindows = _startWithWindowsMenuItem.Checked;
            ApplyStartupRegistration(_config.StartWithWindows);
            _settingsRepository.Save(_config);
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to update startup registration: {ex.Message}");
            MessageBox.Show(
                $"Failed to update startup setting.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "UHK Keymap Autochanger",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ApplyStartupRegistration(bool enabled)
    {
        _startupRegistrationService.SetEnabled(enabled, Application.ExecutablePath);

        _suppressStartWithWindowsCheckedChanged = true;
        _startWithWindowsMenuItem.Checked = enabled;
        _suppressStartWithWindowsCheckedChanged = false;
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    private void OnSwitchingStatusChanged(object? sender, string status)
    {
        if (_notifyIcon.ContextMenuStrip is null || _notifyIcon.ContextMenuStrip.IsDisposed)
        {
            return;
        }

        void Update()
        {
            _lastSwitchingStatus = status;
            _statusMenuItem.Text = $"Status: {status}";
        }

        if (_notifyIcon.ContextMenuStrip.InvokeRequired)
        {
            _notifyIcon.ContextMenuStrip.BeginInvoke((MethodInvoker)Update);
            return;
        }

        Update();
    }
}
