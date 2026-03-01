using UhkKeymapAutochanger.Core.Models;
using UhkKeymapAutochanger.Core.Settings;

namespace UhkKeymapAutochanger.UI;

internal sealed class SettingsForm : Form
{
    private readonly TextBox _defaultKeymapTextBox = new();
    private readonly TextBox _runtimeStatusTextBox = new();
    private readonly NumericUpDown _pollIntervalNumeric = new();
    private readonly CheckBox _startWithWindowsCheckBox = new();
    private readonly CheckBox _pauseWhenAgentRunningCheckBox = new();
    private readonly DataGridView _rulesGrid = new();

    public SettingsForm(AppConfig config, string initialRuntimeStatus)
    {
        Text = "UHK Keymap Autochanger Settings";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 760;
        Height = 560;
        MinimizeBox = false;
        MaximizeBox = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var fieldsTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 5,
        };
        fieldsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        fieldsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        fieldsTable.Controls.Add(new Label
        {
            Text = "Default keymap abbreviation",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
        }, 0, 0);
        _defaultKeymapTextBox.Dock = DockStyle.Fill;
        fieldsTable.Controls.Add(_defaultKeymapTextBox, 1, 0);

        fieldsTable.Controls.Add(new Label
        {
            Text = $"Poll interval (ms, {SettingsValidator.MinPollIntervalMs}-{SettingsValidator.MaxPollIntervalMs})",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
        }, 0, 1);
        _pollIntervalNumeric.Minimum = SettingsValidator.MinPollIntervalMs;
        _pollIntervalNumeric.Maximum = SettingsValidator.MaxPollIntervalMs;
        _pollIntervalNumeric.Dock = DockStyle.Left;
        _pollIntervalNumeric.Width = 120;
        fieldsTable.Controls.Add(_pollIntervalNumeric, 1, 1);

        _startWithWindowsCheckBox.Text = "Start with Windows";
        _startWithWindowsCheckBox.AutoSize = true;
        _startWithWindowsCheckBox.Anchor = AnchorStyles.Left;
        fieldsTable.Controls.Add(_startWithWindowsCheckBox, 1, 2);

        _pauseWhenAgentRunningCheckBox.Text = "Pause switching while UHK Agent is running (recommended)";
        _pauseWhenAgentRunningCheckBox.AutoSize = true;
        _pauseWhenAgentRunningCheckBox.Anchor = AnchorStyles.Left;
        fieldsTable.Controls.Add(_pauseWhenAgentRunningCheckBox, 1, 3);

        fieldsTable.Controls.Add(new Label
        {
            Text = "Runtime status",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
        }, 0, 4);
        _runtimeStatusTextBox.Dock = DockStyle.Fill;
        _runtimeStatusTextBox.ReadOnly = true;
        _runtimeStatusTextBox.TabStop = false;
        _runtimeStatusTextBox.Text = initialRuntimeStatus;
        fieldsTable.Controls.Add(_runtimeStatusTextBox, 1, 4);

        var rulesPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 12, 0, 0),
        };

        var rulesLabel = new Label
        {
            Text = "Rules (processName -> keymap + layer):",
            Dock = DockStyle.Top,
            AutoSize = true,
        };

        _rulesGrid.Dock = DockStyle.Fill;
        _rulesGrid.AllowUserToAddRows = true;
        _rulesGrid.AllowUserToDeleteRows = true;
        _rulesGrid.RowHeadersVisible = false;
        _rulesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ProcessName",
            HeaderText = "Process Name (e.g. Code.exe)",
        });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Keymap",
            HeaderText = "Keymap Abbreviation (e.g. DEV)",
        });
        var layerColumn = new DataGridViewComboBoxColumn
        {
            Name = "Layer",
            HeaderText = "Layer (e.g. base, fn, mod)",
            FlatStyle = FlatStyle.Flat,
        };
        layerColumn.Items.AddRange(SettingsValidator.SupportedLayers.Cast<object>().ToArray());
        _rulesGrid.Columns.Add(layerColumn);
        _rulesGrid.DefaultValuesNeeded += (_, e) => e.Row.Cells["Layer"].Value = SettingsValidator.DefaultLayer;

        rulesPanel.Controls.Add(_rulesGrid);
        rulesPanel.Controls.Add(rulesLabel);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0),
        };

        var saveButton = new Button
        {
            Text = "Save",
            AutoSize = true,
        };
        saveButton.Click += (_, _) => SaveAndClose();

        var cancelButton = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            DialogResult = DialogResult.Cancel,
        };

        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(cancelButton);

        root.Controls.Add(fieldsTable, 0, 0);
        root.Controls.Add(rulesPanel, 0, 1);
        root.Controls.Add(buttonPanel, 0, 2);

        Controls.Add(root);

        PopulateFromConfig(config);
    }

    public AppConfig? SavedConfig { get; private set; }

    public void UpdateRuntimeStatus(string status)
    {
        _runtimeStatusTextBox.Text = status;
    }

    private void PopulateFromConfig(AppConfig config)
    {
        _defaultKeymapTextBox.Text = config.DefaultKeymap;
        _pollIntervalNumeric.Value = Math.Clamp(
            config.PollIntervalMs,
            SettingsValidator.MinPollIntervalMs,
            SettingsValidator.MaxPollIntervalMs);
        _startWithWindowsCheckBox.Checked = config.StartWithWindows;
        _pauseWhenAgentRunningCheckBox.Checked = config.PauseWhenUhkAgentRunning;

        _rulesGrid.Rows.Clear();
        foreach (var rule in config.Rules)
        {
            _rulesGrid.Rows.Add(rule.ProcessName, rule.Keymap, rule.Layer);
        }
    }

    private void SaveAndClose()
    {
        var config = new AppConfig
        {
            DefaultKeymap = _defaultKeymapTextBox.Text,
            PollIntervalMs = (int)_pollIntervalNumeric.Value,
            StartWithWindows = _startWithWindowsCheckBox.Checked,
            PauseWhenUhkAgentRunning = _pauseWhenAgentRunningCheckBox.Checked,
            Rules = ReadRules(),
        };

        var validation = SettingsValidator.Validate(config);
        if (!validation.IsValid)
        {
            MessageBox.Show(
                string.Join(Environment.NewLine, validation.Errors),
                "Invalid settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        SavedConfig = validation.NormalizedConfig;
        DialogResult = DialogResult.OK;
        Close();
    }

    private List<ProcessRule> ReadRules()
    {
        var rules = new List<ProcessRule>();

        foreach (DataGridViewRow row in _rulesGrid.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var processName = (row.Cells[0].Value?.ToString() ?? string.Empty).Trim();
            var keymap = (row.Cells[1].Value?.ToString() ?? string.Empty).Trim();
            var layer = (row.Cells[2].Value?.ToString() ?? string.Empty).Trim();

            if (processName.Length == 0 && keymap.Length == 0 && layer.Length == 0)
            {
                continue;
            }

            rules.Add(new ProcessRule
            {
                ProcessName = processName,
                Keymap = keymap,
                Layer = layer,
            });
        }

        return rules;
    }
}
