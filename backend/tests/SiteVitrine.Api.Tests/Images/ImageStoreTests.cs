using System.Text;
using SiteVitrine.Api.Images;

namespace SiteVitrine.Api.Tests.Images;

/// <summary>
/// Le dossier d'images, testé sur un vrai dossier temporaire — c'est le système de fichiers qu'on
/// veut vérifier, pas une abstraction par-dessus.
///
/// Le ménage (<c>DeleteUnusedAsync</c>) a besoin de la base : il est testé à part, dans
/// <see cref="ImageCleanupTests"/>.
/// </summary>
public sealed class ImageStoreTests : IDisposable
{
    private readonly string root =
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    private readonly ImageStore store;

    public ImageStoreTests() => store = new ImageStore(root);

    public void Dispose() => Directory.Delete(root, recursive: true);

    private Task<string> SaveAsync(string content, string extension = ".png") =>
        store.SaveAsync(new MemoryStream(Encoding.UTF8.GetBytes(content)), extension, default);

    [Theory]
    [InlineData("image.png")]
    [InlineData("3f9ac1.JPG")] // l'extension est comparée sans tenir compte de la casse
    [InlineData("logo.svg")]
    public void IsValidName_accepte_un_simple_nom_de_fichier_d_un_format_autorisé(string name) =>
        Assert.True(ImageStore.IsValidName(name));

    [Theory]
    [InlineData("../appsettings.json")]
    [InlineData("../../image.png")] // remontée de dossier
    [InlineData("sous-dossier/image.png")]
    [InlineData("C:/Windows/image.png")]
    [InlineData(".htaccess")] // fichier caché
    [InlineData("script.exe")] // format hors liste blanche
    [InlineData("image")] // sans extension
    public void IsValidName_refuse_tout_ce_qui_n_est_pas_un_nom_simple(string name) =>
        Assert.False(ImageStore.IsValidName(name));

    [Fact]
    public async Task Une_image_déposée_est_nommée_d_après_l_empreinte_de_son_contenu()
    {
        var name = await SaveAsync("le contenu");

        // 32 caractères hexadécimaux (128 bits de SHA-256) + l'extension, en minuscules.
        Assert.Matches("^[0-9a-f]{32}\\.png$", name);
        Assert.True(store.Exists(name));
    }

    [Fact]
    public async Task La_même_image_déposée_deux_fois_n_est_stockée_qu_une_fois()
    {
        var first = await SaveAsync("le contenu");
        var second = await SaveAsync("le contenu");

        Assert.Equal(first, second);
        Assert.Single(Directory.EnumerateFiles(root));
    }

    [Fact]
    public async Task Deux_contenus_différents_donnent_deux_noms_différents()
    {
        var first = await SaveAsync("un contenu");
        var second = await SaveAsync("un autre contenu");

        Assert.NotEqual(first, second);
        Assert.Equal(2, Directory.EnumerateFiles(root).Count());
    }

    [Fact]
    public async Task L_extension_est_normalisée_en_minuscules()
    {
        var name = await SaveAsync("le contenu", ".JPG");

        Assert.EndsWith(".jpg", name);
    }

    [Fact]
    public async Task Un_nouveau_dépôt_de_la_même_image_relance_son_délai_de_grâce()
    {
        var name = await SaveAsync("le contenu");
        var path = Path.Combine(root, name);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-1));

        await SaveAsync("le contenu");

        // Sans ce rafraîchissement, le ménage jugerait abandonnée une image que l'utilisateur vient
        // de redéposer dans un formulaire encore ouvert.
        Assert.True(File.GetLastWriteTimeUtc(path) > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void Find_renvoie_null_pour_un_nom_invalide_sans_toucher_au_disque() =>
        Assert.Null(store.Find("../appsettings.json"));

    [Fact]
    public void Find_renvoie_null_pour_un_fichier_absent() => Assert.Null(store.Find("absente.png"));

    [Fact]
    public async Task CopyMissingFrom_copie_les_absentes_et_ne_touche_pas_aux_présentes()
    {
        var source = Directory.CreateDirectory(Path.Combine(root, "..", Path.GetRandomFileName()));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(source.FullName, "nouvelle.png"), "nouvelle");
            await File.WriteAllTextAsync(Path.Combine(source.FullName, "déjà-là.png"), "version du fichier");

            await File.WriteAllTextAsync(Path.Combine(root, "déjà-là.png"), "version déjà en place");

            store.CopyMissingFrom(source.FullName, ["nouvelle.png", "déjà-là.png"]);

            Assert.Equal("nouvelle", await File.ReadAllTextAsync(Path.Combine(root, "nouvelle.png")));
            // La base fait foi : une image déjà déposée ne doit pas être écrasée par le fichier.
            Assert.Equal(
                "version déjà en place",
                await File.ReadAllTextAsync(Path.Combine(root, "déjà-là.png")));
        }
        finally
        {
            source.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("image.png", "image/png")]
    [InlineData("image.JPG", "image/jpeg")]
    [InlineData("logo.svg", "image/svg+xml")]
    public void ContentTypeOf_déduit_le_type_de_l_extension(string name, string expected) =>
        Assert.Equal(expected, ImageStore.ContentTypeOf(name));
}
