namespace RoboKeep.Tests;

/// <summary>
/// I test di questa collection cambiano la lingua del PROCESSO (DefaultThreadCurrentUICulture)
/// o del thread: girano da soli, mai in parallelo con gli altri. Senza, un test che asserisce
/// su un testo localizzato puo' vedere la lingua sbagliata a caso (release v1.7.0 fallita in CI).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CultureCollection
{
    public const string Name = "Cultura";
}
