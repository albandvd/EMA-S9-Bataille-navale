namespace Naval.App.Services;

using Microsoft.JSInterop;

/// <summary>
/// E-06 : persistance du <c>playerToken</c> pour reprendre une partie après rafraîchissement.
/// Appelle directement les fonctions globales du navigateur via IJSRuntime (aucun fichier .js
/// à ajouter au projet), dans le même esprit que le choix déjà pris pour l'audio.
/// </summary>
public sealed class LocalStorageService(IJSRuntime js)
{
    public async Task<string?> GetItemAsync(string key)
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    public async Task SetItemAsync(string key, string value)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch (JSException)
        {
            // Stockage indisponible (navigation privée, quota) : la reconnexion automatique
            // sera simplement indisponible, ce n'est pas une erreur bloquante pour la partie.
        }
    }

    public async Task RemoveItemAsync(string key)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch (JSException)
        {
        }
    }
}
