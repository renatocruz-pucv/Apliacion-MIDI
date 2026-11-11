using System;
using System.Collections.Generic;

/// <summary>
/// Datos de UN pad de un banco personalizado, tal como se guardan en el JSON.
/// No guarda el AudioClip (Unity no puede serializar un clip cargado en
/// ejecución): guarda el NOMBRE del archivo de audio que CustomBankLibrary copió
/// a su carpeta interna (persistentDataPath/CustomAudio).
/// </summary>
[Serializable]
public class CustomPadData
{
    public string displayName = "";
    public string audioFileName = "";

    public bool HasAudio => !string.IsNullOrEmpty(audioFileName);
}

/// <summary>Un banco personalizado: nombre + exactamente 8 pads (los mismos 8 slots de la grilla).</summary>
[Serializable]
public class CustomBankData
{
    public string id;
    public string name = "Mi banco";
    public List<CustomPadData> pads = new List<CustomPadData>();
}

/// <summary>Raíz del archivo custom_banks.json.</summary>
[Serializable]
public class CustomBankLibraryData
{
    public int version = 1;
    public List<CustomBankData> banks = new List<CustomBankData>();
}
