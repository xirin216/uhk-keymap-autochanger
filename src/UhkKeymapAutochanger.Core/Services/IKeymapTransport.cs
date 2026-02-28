namespace UhkKeymapAutochanger.Core.Services;

public interface IKeymapTransport
{
    Task SwitchKeymapAsync(string keymapAbbreviation, CancellationToken cancellationToken = default);

    Task ExecuteMacroCommandAsync(string command, CancellationToken cancellationToken = default);
}
