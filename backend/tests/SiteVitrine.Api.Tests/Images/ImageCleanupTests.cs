using System.Text;
using SiteVitrine.Api.Data;
using SiteVitrine.Api.Images;
using SiteVitrine.Api.Resources;

namespace SiteVitrine.Api.Tests.Images;

/// <summary>
/// Le ménage des images inutilisées. C'est la partie la plus délicate d'ImageStore : trop stricte
/// elle efface une image en cours de dépôt, trop laxiste elle laisse le dossier gonfler.
///
/// Il lui faut la base, puisque c'est elle qui dit quelles images sont encore référencées — d'où la
/// collection partagée.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ImageCleanupTests : IAsyncLifetime, IDisposable
{
    private readonly PostgresFixture postgres;
    private readonly string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly ImageStore store;

    public ImageCleanupTests(PostgresFixture postgres)
    {
        this.postgres = postgres;
        store = new ImageStore(root);
    }

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => Directory.Delete(root, recursive: true);

    /// <summary>Pose un fichier dans le dossier, en datant sa dernière écriture.</summary>
    private string PutImage(string name, TimeSpan? age = null)
    {
        var path = Path.Combine(root, name);
        File.WriteAllText(path, "contenu");

        if (age is not null) File.SetLastWriteTimeUtc(path, DateTime.UtcNow - age.Value);

        return name;
    }

    private async Task AddResourceAsync(string slug, string? imageFile)
    {
        await using var db = postgres.CreateDbContext();
        db.Resources.Add(new Resource
        {
            Slug = slug,
            Title = slug,
            Avis = "Un avis.",
            ImageFile = imageFile,
        });
        await db.SaveChangesAsync();
    }

    private async Task<List<string>> CleanAsync(params string?[] released)
    {
        await using AppDbContext db = postgres.CreateDbContext();
        return await store.DeleteUnusedAsync(db, released, default);
    }

    [Fact]
    public async Task Une_image_encore_référencée_est_conservée()
    {
        PutImage("utilisée.png", age: TimeSpan.FromDays(30));
        await AddResourceAsync("linqpad", "utilisée.png");

        var deleted = await CleanAsync();

        Assert.Empty(deleted);
        Assert.True(store.Exists("utilisée.png"));
    }

    [Fact]
    public async Task L_ancienne_image_d_une_ressource_modifiée_part_tout_de_suite()
    {
        // Déposée à l'instant : seul le fait d'être *libérée* par l'écriture la condamne.
        PutImage("ancienne.png");
        PutImage("nouvelle.png");
        await AddResourceAsync("linqpad", "nouvelle.png");

        var deleted = await CleanAsync("ancienne.png");

        Assert.Equal(["ancienne.png"], deleted);
        Assert.False(store.Exists("ancienne.png"));
        Assert.True(store.Exists("nouvelle.png"));
    }

    [Fact]
    public async Task Une_image_déposée_à_l_instant_mais_jamais_enregistrée_est_gardée()
    {
        PutImage("en-cours.png");

        var deleted = await CleanAsync();

        // Le formulaire qui l'a déposée est peut-être encore ouvert : la supprimer maintenant la
        // ferait disparaître sous les pieds de l'utilisateur.
        Assert.Empty(deleted);
        Assert.True(store.Exists("en-cours.png"));
    }

    [Fact]
    public async Task Une_image_jamais_enregistrée_et_passée_de_mode_est_supprimée()
    {
        PutImage("abandonnée.png", age: TimeSpan.FromHours(2));

        var deleted = await CleanAsync();

        Assert.Equal(["abandonnée.png"], deleted);
        Assert.False(store.Exists("abandonnée.png"));
    }

    [Fact]
    public async Task Une_image_partagée_survit_tant_qu_une_ressource_l_utilise()
    {
        PutImage("partagée.png");
        await AddResourceAsync("linqpad", "partagée.png");
        await AddResourceAsync("vs-code", "partagée.png");

        // La première ressource la libère, la seconde l'utilise toujours.
        var deleted = await CleanAsync("partagée.png");

        Assert.Empty(deleted);
        Assert.True(store.Exists("partagée.png"));
    }

    [Fact]
    public async Task Un_fichier_temporaire_d_écriture_en_cours_n_est_pas_touché()
    {
        // ImageStore écrit à côté puis renomme : le .tmp d'un dépôt concurrent ne doit pas être
        // confondu avec une image orpheline.
        File.WriteAllText(Path.Combine(root, "3f9ac1.png.tmp"), "écriture en cours");

        var deleted = await CleanAsync();

        Assert.Empty(deleted);
        Assert.True(File.Exists(Path.Combine(root, "3f9ac1.png.tmp")));
    }

    [Fact]
    public async Task Le_ménage_ignore_les_null_de_la_liste_des_images_libérées()
    {
        // Une ressource sans image libère un `null` : il ne doit rien déclencher.
        PutImage("récente.png");

        var deleted = await CleanAsync(null, null);

        Assert.Empty(deleted);
    }

    [Fact]
    public async Task SaveAsync_puis_ménage_immédiat_conserve_l_image()
    {
        var name = await store.SaveAsync(
            new MemoryStream(Encoding.UTF8.GetBytes("une image")), ".png", default);

        var deleted = await CleanAsync();

        Assert.Empty(deleted);
        Assert.True(store.Exists(name));
    }
}
