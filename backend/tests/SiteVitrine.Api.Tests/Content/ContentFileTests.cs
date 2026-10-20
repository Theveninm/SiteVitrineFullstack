using SiteVitrine.Api.Content;
using SiteVitrine.Api.Projects;
using SiteVitrine.Api.Resources;

namespace SiteVitrine.Api.Tests.Content;

/// <summary>
/// Le contrat entre <c>content/content.yaml</c> et l'application. Ce qui compte ici : qu'un
/// aller-retour base → fichier → base ne perde rien, et qu'une faute de frappe dans le fichier
/// produise une erreur plutôt qu'un silence.
///
/// <c>IDisposable</c> : chaque test écrit dans son propre dossier temporaire, supprimé ensuite.
/// xUnit construit une instance de la classe par test, et appelle Dispose après chacun.
/// </summary>
public sealed class ContentFileTests : IDisposable
{
    private readonly string directory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())).FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private async Task<ContentFile> LoadAsync(string yaml)
    {
        var path = Path.Combine(directory, "content.yaml");
        await File.WriteAllTextAsync(path, yaml);
        return await ContentFile.LoadAsync(path);
    }

    [Fact]
    public async Task Un_aller_retour_par_le_fichier_ne_perd_rien()
    {
        var original = new ContentFile
        {
            Resources =
            [
                new ResourceContent
                {
                    Slug = "cs-players-guide",
                    Title = "C# Player's Guide",
                    SiteUrl = "https://csharpplayersguide.com/",
                    ImageFile = "CsPlayerGuide.jpg",
                    // Assez long pour déclencher l'écriture en bloc littéral, et truffé de ce qui
                    // casserait un scalaire non quoté : apostrophes, guillemets, « : ».
                    Avis = "Un avis « long » : il dépasse les deux cents caractères, contient des "
                        + "apostrophes, des \"guillemets\", et des deux-points suivis d'une espace — "
                        + "tout ce qui rend un scalaire YAML ambigu quand il n'est pas en bloc.",
                    Positifs = ["ludique", "bien structuré"],
                    Negatifs = ["n'aborde pas les frameworks"],
                    Note = 4.5m,
                },
            ],
            Projects =
            [
                new ProjectContent
                {
                    Slug = "docfileeditor",
                    Title = "DocFileEditor",
                    Description = "Une description.",
                    Techs = ["C#", "WPF"],
                    GithubUrl = "https://github.com/Theveninm/DocFileEditor",
                },
            ],
        };

        var reloaded = await LoadAsync(original.ToYaml());

        var resource = Assert.Single(reloaded.Resources);
        Assert.Equal(original.Resources[0].Slug, resource.Slug);
        Assert.Equal(original.Resources[0].Title, resource.Title);
        Assert.Equal(original.Resources[0].Avis, resource.Avis);
        Assert.Equal(original.Resources[0].Positifs, resource.Positifs);
        Assert.Equal(original.Resources[0].Note, resource.Note);

        var project = Assert.Single(reloaded.Projects);
        Assert.Equal(original.Projects[0].Slug, project.Slug);
        Assert.Equal(original.Projects[0].Techs, project.Techs);
        Assert.Equal(original.Projects[0].GithubUrl, project.GithubUrl);
    }

    [Fact]
    public void Un_long_texte_est_écrit_en_bloc_littéral()
    {
        var file = new ContentFile
        {
            Resources =
            [
                new ResourceContent
                {
                    Slug = "x",
                    Title = "X",
                    Avis = new string('a', 250),
                },
            ],
        };

        // Sans cela, un export réécrirait chaque avis en une ligne unique de 1000 caractères :
        // le fichier deviendrait pénible à relire après chaque aller-retour.
        Assert.Contains("avis: |-", file.ToYaml());
    }

    [Fact]
    public void Les_clés_nulles_ne_sont_pas_écrites()
    {
        var file = new ContentFile
        {
            Resources = [new ResourceContent { Slug = "x", Title = "X", Avis = "Un avis." }],
        };

        var yaml = file.ToYaml();

        Assert.DoesNotContain("imageFile", yaml);
        Assert.DoesNotContain("siteUrl", yaml);
    }

    [Fact]
    public async Task Un_dièse_collé_au_texte_n_ouvre_pas_un_commentaire()
    {
        // « C# » sans quotes reste valide : en YAML, # n'ouvre un commentaire que précédé d'une
        // espace. Les projets du site en dépendent — autant le figer dans un test.
        var reloaded = await LoadAsync(
            """
            projects:
              - slug: docfileeditor
                title: DocFileEditor
                description: Une description.
                techs:
                  - C#
                  - WPF
            """);

        Assert.Equal(["C#", "WPF"], Assert.Single(reloaded.Projects).Techs);
    }

    [Fact]
    public async Task Une_clé_en_double_lève_au_lieu_d_être_écrasée_en_silence()
    {
        var yaml = """
            resources:
              - slug: linqpad
                title: LINQPad
                title: LINQPad 7
                avis: Un avis.
            """;

        // Une clé en double est une faute de frappe, pas une extension silencieuse.
        await Assert.ThrowsAnyAsync<Exception>(() => LoadAsync(yaml));
    }

    [Fact]
    public async Task Un_fichier_absent_dit_où_il_était_attendu()
    {
        var path = Path.Combine(directory, "absent.yaml");

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(
            () => ContentFile.LoadAsync(path));

        Assert.Contains(Path.GetFullPath(path), exception.Message);
    }

    [Fact]
    public async Task Un_fichier_vide_donne_un_contenu_vide()
    {
        var reloaded = await LoadAsync(string.Empty);

        Assert.Empty(reloaded.Resources);
        Assert.Empty(reloaded.Projects);
    }
}
