namespace RobocopySW.Core.Models;

/// <summary>Tipo di supporto di una sorgente/destinazione, usato dalla creazione guidata
/// per consigliare le opzioni robocopy (soprattutto /MT). L'ordine corrisponde alle voci
/// della ComboBox del wizard.</summary>
public enum StorageKind
{
    Ssd,
    Hdd,
    Usb,
    Network,
}
