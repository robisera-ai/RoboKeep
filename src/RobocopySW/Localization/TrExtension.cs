using System.Windows.Data;
using System.Windows.Markup;

namespace RobocopySW.Localization;

/// <summary>
/// Markup extension per le stringhe localizzate: <c>{l:Tr Main_New}</c>.
/// Restituisce un binding sull'indicizzatore di <see cref="Loc"/>, così il testo
/// si aggiorna automaticamente al cambio lingua.
/// </summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }
    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay,
        };
        return binding.ProvideValue(serviceProvider);
    }
}
