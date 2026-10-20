using SiteVitrine.Api.Resources;

namespace SiteVitrine.Api.Tests.Resources;

/// <summary>
/// La validation et la recopie des champs éditables d'une ressource. Ces règles servent à la fois à
/// l'import du fichier YAML et aux écritures du back-office : les tester une fois les couvre toutes
/// les deux.
/// </summary>
public sealed class ResourceContentTests
{
    /// <summary>Une ressource valide, que chaque test abîme sur le champ qui l'intéresse.</summary>
    private static ResourceContent Valid() =>
        new()
        {
            Slug = "linqpad",
            Title = "LINQPad",
            Avis = "Un avis.",
            Note = 4,
            Positifs = ["rapide"],
            Negatifs = ["payant"],
        };

    [Fact]
    public void Une_ressource_complète_ne_produit_aucune_erreur() => Assert.Empty(Valid().Validate());

    [Fact]
    public void Les_champs_obligatoires_sont_signalés_par_leur_nom_camelCase()
    {
        var content = new ResourceContent();

        var errors = content.Validate();

        // Le nom de la clé est un contrat : le frontend s'en sert pour afficher l'erreur sous le
        // bon champ du formulaire.
        Assert.Contains("slug", errors.Keys);
        Assert.Contains("title", errors.Keys);
        Assert.Contains("avis", errors.Keys);
    }

    [Fact]
    public void Un_slug_mal_formé_est_refusé()
    {
        var content = Valid();
        content.Slug = "LINQ Pad";

        Assert.Contains("slug", content.Validate().Keys);
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(5.5)]
    public void Une_note_hors_de_zéro_cinq_est_refusée(double note)
    {
        var content = Valid();
        content.Note = (decimal)note;

        Assert.Contains("note", content.Validate().Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2.5)]
    [InlineData(5)]
    public void Les_notes_de_zéro_à_cinq_passent(double note)
    {
        var content = Valid();
        content.Note = (decimal)note;

        Assert.Empty(content.Validate());
    }

    [Fact]
    public void Une_adresse_de_site_relative_est_refusée()
    {
        var content = Valid();
        content.SiteUrl = "csharpplayersguide.com";

        Assert.Contains("siteUrl", content.Validate().Keys);
    }

    [Fact]
    public void Un_nom_d_image_qui_sort_du_dossier_est_refusé()
    {
        var content = Valid();
        content.ImageFile = "../../appsettings.json";

        Assert.Contains("imageFile", content.Validate().Keys);
    }

    [Fact]
    public void Un_champ_facultatif_vide_devient_null_en_base()
    {
        var content = Valid();
        content.SiteUrl = "   ";
        content.ImageFile = string.Empty;

        var resource = content.ToNewResource();

        // « Aucune image » ne doit avoir qu'une seule représentation en base, sans quoi les
        // requêtes devraient tester à la fois null et la chaîne vide.
        Assert.Null(resource.SiteUrl);
        Assert.Null(resource.ImageFile);
    }

    [Fact]
    public void ApplyTo_recopie_tous_les_champs_éditables()
    {
        var resource = Valid().ToNewResource();
        var modified = new ResourceContent
        {
            Slug = "linqpad-7",
            Title = "LINQPad 7",
            Avis = "Un autre avis.",
            Note = 5,
            SiteUrl = "https://www.linqpad.net/",
            Positifs = ["toujours rapide"],
            Negatifs = [],
        };

        modified.ApplyTo(resource);

        Assert.Equal("linqpad-7", resource.Slug);
        Assert.Equal("LINQPad 7", resource.Title);
        Assert.Equal(5, resource.Note);
        Assert.Equal("https://www.linqpad.net/", resource.SiteUrl);
        Assert.Equal(["toujours rapide"], resource.Positifs);
        Assert.Empty(resource.Negatifs);
    }
}
