using Microsoft.EntityFrameworkCore;
using SiteVitrine.Api.Content;
using SiteVitrine.Api.Images;
using SiteVitrine.Api.Projects;
using SiteVitrine.Api.Resources;

namespace SiteVitrine.Api.Tests.Content;

/// <summary>
/// L'import et l'export du fichier de contenu, contre une vraie base.
///
/// C'est l'endroit le plus risqué du backend : il écrit la base entière d'un coup, et un import
/// mal maîtrisé écrase le travail fait dans le back-office. Les garanties qu'on veut figer : il est
/// idempotent, il ne détruit rien sans qu'on le demande, et il refuse un fichier douteux **avant**
/// d'écrire quoi que ce soit.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ContentImporterTests : IAsyncLifetime, IDisposable
{
    private readonly PostgresFixture postgres;

    /// <summary>Dossier d'images de l'API, et dossier « content/images » d'où l'import les tire.</summary>
    private readonly string storeRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly string sourceImages = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly ImageStore images;

    public ContentImporterTests(PostgresFixture postgres)
    {
        this.postgres = postgres;
        images = new ImageStore(storeRoot);
        Directory.CreateDirectory(sourceImages);
    }

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        Directory.Delete(storeRoot, recursive: true);
        Directory.Delete(sourceImages, recursive: true);
    }

    private static ResourceContent Resource(string slug, string avis = "Un avis.") =>
        new() { Slug = slug, Title = slug, Avis = avis, Note = 4 };

    private static ProjectContent Project(string slug, string description = "Une description.") =>
        new() { Slug = slug, Title = slug, Description = description };

    private async Task<ImportReport> ImportAsync(ContentFile file, bool prune = false)
    {
        await using var db = postgres.CreateDbContext();
        return await ContentImporter.ImportAsync(db, file, images, sourceImages, prune);
    }

    private async Task<List<Resource>> ReadResourcesAsync()
    {
        await using var db = postgres.CreateDbContext();
        return await db.Resources.AsNoTracking().OrderBy(r => r.SortOrder).ToListAsync();
    }

    private async Task<List<Project>> ReadProjectsAsync()
    {
        await using var db = postgres.CreateDbContext();
        return await db.Projects.AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync();
    }

    [Fact]
    public async Task Un_premier_import_crée_les_deux_types_de_contenu()
    {
        var file = new ContentFile
        {
            Resources = [Resource("linqpad"), Resource("vs-code")],
            Projects = [Project("docfileeditor")],
        };

        var report = await ImportAsync(file);

        Assert.Equal(2, report.Resources.Created);
        Assert.Equal(1, report.Projects.Created);
        Assert.Equal(2, (await ReadResourcesAsync()).Count);
        Assert.Single(await ReadProjectsAsync());
    }

    [Fact]
    public async Task L_ordre_des_entrées_du_fichier_devient_l_ordre_d_affichage()
    {
        await ImportAsync(new ContentFile
        {
            Resources = [Resource("premier"), Resource("deuxieme"), Resource("troisieme")],
        });

        var resources = await ReadResourcesAsync();

        Assert.Equal(["premier", "deuxieme", "troisieme"], resources.Select(r => r.Slug));
        Assert.Equal([0, 1, 2], resources.Select(r => r.SortOrder));
    }

    [Fact]
    public async Task Réordonner_le_fichier_réordonne_le_site()
    {
        var file = new ContentFile { Resources = [Resource("a"), Resource("b")] };
        await ImportAsync(file);

        file.Resources.Reverse();
        await ImportAsync(file);

        Assert.Equal(["b", "a"], (await ReadResourcesAsync()).Select(r => r.Slug));
    }

    [Fact]
    public async Task Rejouer_le_même_fichier_ne_change_rien()
    {
        var file = new ContentFile
        {
            Resources = [Resource("linqpad")],
            Projects = [Project("docfileeditor")],
        };
        await ImportAsync(file);

        var report = await ImportAsync(file);

        // Idempotence : c'est ce qui rend l'import rejouable sans crainte sur une base déjà remplie.
        Assert.Equal(0, report.Resources.Created);
        Assert.Equal(0, report.Resources.Updated);
        Assert.Equal(1, report.Resources.Unchanged);
        Assert.Equal(1, report.Projects.Unchanged);
    }

    [Fact]
    public async Task Une_entrée_modifiée_est_comptée_comme_mise_à_jour()
    {
        var file = new ContentFile { Resources = [Resource("linqpad", "Avis d'origine.")] };
        await ImportAsync(file);

        file.Resources[0].Avis = "Avis corrigé.";
        var report = await ImportAsync(file);

        Assert.Equal(1, report.Resources.Updated);
        Assert.Equal("Avis corrigé.", Assert.Single(await ReadResourcesAsync()).Avis);
    }

    [Fact]
    public async Task Une_entrée_en_base_absente_du_fichier_est_signalée_mais_conservée()
    {
        await ImportAsync(new ContentFile { Resources = [Resource("linqpad"), Resource("vs-code")] });

        var report = await ImportAsync(new ContentFile { Resources = [Resource("linqpad")] });

        Assert.Equal(["vs-code"], report.Resources.OrphanSlugs);
        Assert.Equal(0, report.Resources.Removed);
        // Non destructif par défaut : la suppression est un geste explicite.
        Assert.Equal(2, (await ReadResourcesAsync()).Count);
    }

    [Fact]
    public async Task Prune_supprime_réellement_les_orphelines()
    {
        await ImportAsync(new ContentFile
        {
            Resources = [Resource("linqpad"), Resource("vs-code")],
            Projects = [Project("docfileeditor"), Project("site-personnel")],
        });

        var report = await ImportAsync(
            new ContentFile { Resources = [Resource("linqpad")], Projects = [Project("docfileeditor")] },
            prune: true);

        Assert.Equal(1, report.Resources.Removed);
        Assert.Equal(1, report.Projects.Removed);
        Assert.Empty(report.Resources.OrphanSlugs);
        Assert.Single(await ReadResourcesAsync());
        Assert.Single(await ReadProjectsAsync());
    }

    [Fact]
    public async Task Les_orphelins_du_rapport_nomment_leur_section()
    {
        await ImportAsync(new ContentFile
        {
            Resources = [Resource("linqpad")],
            Projects = [Project("docfileeditor")],
        });

        var report = await ImportAsync(new ContentFile());

        Assert.Equal(["resources/linqpad", "projects/docfileeditor"], report.OrphanSlugs);
    }

    [Fact]
    public async Task Un_fichier_invalide_est_refusé_sans_rien_écrire()
    {
        await ImportAsync(new ContentFile { Resources = [Resource("linqpad")] });

        var invalide = new ContentFile
        {
            Resources = [Resource("vs-code"), Resource("MAJUSCULES")],
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ImportAsync(invalide));

        Assert.Contains("MAJUSCULES", exception.Message);
        // Rien n'a été écrit : « vs-code », pourtant valide, n'est pas entré en base.
        Assert.Equal(["linqpad"], (await ReadResourcesAsync()).Select(r => r.Slug));
    }

    [Fact]
    public async Task Un_slug_en_double_dans_le_fichier_est_refusé()
    {
        var file = new ContentFile { Resources = [Resource("linqpad"), Resource("linqpad")] };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ImportAsync(file));

        Assert.Contains("double", exception.Message);
    }

    [Fact]
    public async Task Le_message_d_erreur_situe_l_entrée_et_sa_section()
    {
        var file = new ContentFile { Projects = [new ProjectContent { Title = "Sans slug" }] };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ImportAsync(file));

        // Sans slug utilisable, l'entrée est repérée par son rang.
        Assert.Contains("projet n°1", exception.Message);
    }

    [Fact]
    public async Task Les_images_du_fichier_sont_copiées_dans_le_dossier_de_l_API()
    {
        await File.WriteAllTextAsync(Path.Combine(sourceImages, "logo.png"), "une image");
        var resource = Resource("linqpad");
        resource.ImageFile = "logo.png";

        await ImportAsync(new ContentFile { Resources = [resource] });

        // C'est ce qui permet à une base neuve de retrouver ses images depuis content/images/.
        Assert.True(images.Exists("logo.png"));
    }

    [Fact]
    public async Task Une_image_introuvable_fait_échouer_l_import()
    {
        var resource = Resource("linqpad");
        resource.ImageFile = "absente.png";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ImportAsync(new ContentFile { Resources = [resource] }));

        Assert.Contains("absente.png", exception.Message);
    }

    [Fact]
    public async Task L_export_rend_ce_que_l_import_a_chargé()
    {
        var file = new ContentFile
        {
            Resources = [Resource("linqpad"), Resource("vs-code")],
            Projects = [Project("docfileeditor")],
        };
        await ImportAsync(file);

        await using var db = postgres.CreateDbContext();
        var exported = await ContentImporter.ExportAsync(db);

        Assert.Equal(["linqpad", "vs-code"], exported.Resources.Select(r => r.Slug));
        Assert.Equal(["docfileeditor"], exported.Projects.Select(p => p.Slug));
    }

    [Fact]
    public async Task L_export_respecte_l_ordre_d_affichage()
    {
        await ImportAsync(new ContentFile { Projects = [Project("b"), Project("a")] });

        await using var db = postgres.CreateDbContext();
        var exported = await ContentImporter.ExportAsync(db);

        // L'ordre vient de SortOrder, pas de l'ordre d'insertion ni de l'alphabet.
        Assert.Equal(["b", "a"], exported.Projects.Select(p => p.Slug));
    }

    [Fact]
    public async Task Les_tableaux_natifs_PostgreSQL_font_l_aller_retour()
    {
        var resource = Resource("linqpad");
        resource.Positifs = ["rapide", "pratique"];
        resource.Negatifs = [];
        var project = Project("docfileeditor");
        project.Techs = ["C#", "WPF"];

        await ImportAsync(new ContentFile { Resources = [resource], Projects = [project] });

        // text[] mappé par Npgsql sur List<string> : c'est ce qu'un fournisseur en mémoire ne
        // vérifierait pas.
        Assert.Equal(["rapide", "pratique"], Assert.Single(await ReadResourcesAsync()).Positifs);
        Assert.Empty(Assert.Single(await ReadResourcesAsync()).Negatifs);
        Assert.Equal(["C#", "WPF"], Assert.Single(await ReadProjectsAsync()).Techs);
    }
}
