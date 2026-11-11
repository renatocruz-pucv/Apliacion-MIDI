using System;

/// <summary>
/// Abre el selector de archivos para que la persona elija un audio de su
/// dispositivo. Es el ÚNICO lugar del proyecto que depende del plugin
/// NativeFilePicker (yasirkula), para que el resto del código no sepa nada de él.
///
/// - En el Editor de Unity (para probar en Play / Simulator): usa el diálogo de
///   archivos del propio Editor.
/// - En Android: usa NativeFilePicker, que abre el selector del sistema. Ese
///   selector NO requiere permisos de almacenamiento y devuelve una copia
///   legible del archivo en la caché de la app.
///
/// INSTALACIÓN del plugin (una sola vez): Window > Package Manager > "+" >
/// "Install package from git URL..." y pegar:
///     https://github.com/yasirkula/UnityNativeFilePicker.git
/// Sin el plugin instalado, la compilación para Android falla en este archivo.
/// </summary>
public static class AudioFilePicker
{
    /// <summary>True mientras el selector del sistema está abierto (evita abrirlo dos veces).</summary>
    public static bool IsBusy
    {
        get
        {
#if UNITY_EDITOR
            return false;
#elif UNITY_ANDROID
            return NativeFilePicker.IsFilePickerBusy();
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// Pide un archivo de audio. onPicked recibe la ruta del archivo, o null si
    /// la persona canceló. onError recibe un mensaje legible si no se puede abrir.
    /// </summary>
    public static void PickAudio(Action<string> onPicked, Action<string> onError = null)
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("Elegir audio", "", "wav,mp3,ogg,m4a");
        onPicked?.Invoke(string.IsNullOrEmpty(path) ? null : path);
#elif UNITY_ANDROID
        NativeFilePicker.PickFile(path => onPicked?.Invoke(string.IsNullOrEmpty(path) ? null : path), "audio/*");
#else
        onError?.Invoke("Elegir archivos no está disponible en esta plataforma.");
#endif
    }
}
