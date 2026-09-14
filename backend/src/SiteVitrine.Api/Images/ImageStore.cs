using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SiteVitrine.Api.Data;

namespace SiteVitrine.Api.Images;

/// <summary>
/// Le dossier des images du contenu : dépôt, lecture, et ménage des images inutilisées.
///
/// <b>Nommage par empreinte.</b> Une image déposée est renommée d'après le SHA-256 de son contenu
/// (« 3f9a…c1.png »). Trois bénéfices : aucun nom choisi par le client n'arrive sur le disque (pas
/// de « ../ » possible), la même image déposée deux fois n'est stockée qu'une fois, et un nom ne
/// désigne jamais qu'un seul contenu — le navigateur peut donc le garder en cache sans revalider.
///
/// Enregistré en singleton : l'objet ne porte que le chemin du dossier, rien de propre à une requête.
/// </summary>
public sealed class ImageStore
{
    public const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Durée pendant laquelle une image déposée mais encore jamais enregistrée est conservée. Le
    /// glisser-déposer envoie le fichier immédiatement, bien avant que le formulaire ne soit validé :
    /// sans ce délai, le ménage déclenché par une autre écriture la supprimerait sous nos pieds.
    /// </summary>
    private static readonly TimeSpan UploadGracePeriod = TimeSpan.FromHours(1);

    /// <summary>
    /// Formats acceptés, et type MIME servi pour chacun. Liste blanche indexée par extension : le type
    /// annoncé par le navigateur au dépôt n'est pas fiable, c'est l'extension qui fait foi.
    /// </summary>
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".avif"] = "image/avif",
        [".svg"] = "image/svg+xml",
    };

    public ImageStore(string root)
    {
        Root = Path.GetFullPath(root);
        Directory.CreateDirectory(Root);
    }

    /// <summary>Chemin absolu du dossier.</summary>
    public string Root { get; }

    public static IEnumerable<string> AllowedExtensions => ContentTypes.Keys;

    public static bool HasAllowedExtension(string fileName) => ContentTypes.ContainsKey(Path.GetExtension(fileName));

    /// <summary>
    /// Un nom utilisable tel quel dans le dossier : un simple nom de fichier (ni sous-dossier, ni
    /// « .. », ni fichier caché) d'un format autorisé. À vérifier sur tout nom venu de l'extérieur
    /// (URL, YAML, formulaire) avant de le combiner à un chemin.
    /// </summary>
    public static bool IsValidName(string fileName) =>
        Path.GetFileName(fileName) == fileName && !fileName.StartsWith('.') && HasAllowedExtension(fileName);

    public static string ContentTypeOf(string fileName) => ContentTypes[Path.GetExtension(fileName)];

    /// <returns>Le chemin complet de l'image, ou null si le nom est invalide ou le fichier absent.</returns>
    public string? Find(string fileName)
    {
        if (!IsValidName(fileName)) return null;

        var path = Path.Combine(Root, fileName);
        return File.Exists(path) ? path : null;
    }

    public bool Exists(string fileName) => Find(fileName) is not null;

    /// <summary>Enregistre une image déposée et renvoie son nom dans le dossier.</summary>
    /// <param name="extension">Extension d'origine, déjà vérifiée par <see cref="HasAllowedExtension"/>.</param>
    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct)
    {
        // En mémoire pour pouvoir à la fois calculer l'empreinte et écrire le fichier ; la taille est
        // bornée par MaxBytes, vérifiée avant l'appel.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();

        // 32 caractères hexadécimaux = 128 bits d'empreinte : aucune collision à craindre, sans nom à rallonge.
        var name = Convert.ToHexStringLower(SHA256.HashData(bytes))[..32] + extension.ToLowerInvariant();
        var path = Path.Combine(Root, name);

        if (File.Exists(path))
        {
            // Déjà présente (même image déposée à nouveau) : on relance son délai de grâce, sinon le
            // ménage pourrait la juger abandonnée avant que le formulaire ne soit enregistré.
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
        else
        {
            // Écrire à côté puis renommer : un renommage est atomique, donc une image servie n'est
            // jamais à moitié écrite.
            var temporaryPath = path + ".tmp";
            await File.WriteAllBytesAsync(temporaryPath, bytes, ct);
            File.Move(temporaryPath, path, overwrite: true);
        }

        return name;
    }

    /// <summary>
    /// Copie depuis <paramref name="sourceDirectory"/> les images absentes du dossier. Sert à l'import :
    /// une base neuve chargée depuis <c>content/</c> y récupère ses images. Un fichier déjà présent
    /// n'est jamais écrasé.
    /// </summary>
    /// <param name="fileNames">Noms déjà validés par <see cref="IsValidName"/>.</param>
    public void CopyMissingFrom(string sourceDirectory, IEnumerable<string> fileNames)
    {
        foreach (var name in fileNames.Distinct().Where(name => !Exists(name)))
        {
            File.Copy(Path.Combine(sourceDirectory, name), Path.Combine(Root, name));
        }
    }

    /// <summary>
    /// Supprime les images que plus aucune ressource n'utilise. À appeler <b>après</b> chaque écriture
    /// en base susceptible d'en libérer une : modification, suppression, import.
    ///
    /// Deux cas, deux traitements :
    /// <list type="bullet">
    ///   <item>une image <b>libérée</b> par l'écriture qui vient d'avoir lieu (l'ancienne image d'une
    ///   ressource modifiée ou supprimée) disparaît immédiatement ;</item>
    ///   <item>une image non référencée qui n'a pas été libérée — typiquement déposée dans un formulaire
    ///   jamais enregistré — n'est supprimée qu'après le délai de grâce : ce formulaire est peut-être
    ///   encore ouvert.</item>
    /// </list>
    /// Une image partagée par plusieurs ressources n'est supprimée qu'avec la dernière qui l'utilise.
    /// </summary>
    /// <param name="released">Les images référencées avant l'écriture (les null sont ignorés).</param>
    /// <returns>Les noms des fichiers supprimés.</returns>
    public async Task<List<string>> DeleteUnusedAsync(AppDbContext db, IEnumerable<string?> released, CancellationToken ct)
    {
        // Relu en base plutôt que déduit de l'écriture en cours : c'est la seule source qui connaisse
        // aussi les autres ressources.
        var used = await db.Resources
            .Where(r => r.ImageFile != null)
            .Select(r => r.ImageFile!)
            .ToHashSetAsync(ct);

        var releasedNow = released.OfType<string>().ToHashSet();
        var graceLimit = DateTime.UtcNow - UploadGracePeriod;
        var deleted = new List<string>();

        // Filtre sur l'extension : les fichiers .tmp d'une écriture en cours ne sont pas concernés.
        foreach (var file in new DirectoryInfo(Root).EnumerateFiles().Where(f => HasAllowedExtension(f.Name)))
        {
            if (used.Contains(file.Name)) continue;

            if (releasedNow.Contains(file.Name) || file.LastWriteTimeUtc < graceLimit)
            {
                file.Delete();
                deleted.Add(file.Name);
            }
        }

        return deleted;
    }
}
