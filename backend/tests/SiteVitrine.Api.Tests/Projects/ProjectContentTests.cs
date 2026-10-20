using SiteVitrine.Api.Projects;

namespace SiteVitrine.Api.Tests.Projects;

/// <summary>
/// Pendant de <see cref="Tests.Resources.ResourceContentTests"/> pour les projets : les règles
/// communes (slug, adresse web) sont couvertes par ContentRulesTests, on teste ici ce qui est
/// propre au projet.
/// </summary>
public sealed class ProjectContentTests
{
    private static ProjectContent Valid() =>
        new()
        {
            Slug = "docfileeditor",
            Title = "DocFileEditor",
            Description = "Une description.",
            Techs = ["C#", "WPF"],
        };

    [Fact]
    public void Un_projet_complet_ne_produit_aucune_erreur() => Assert.Empty(Valid().Validate());

    [Fact]
    public void Les_champs_obligatoires_sont_signalés_par_leur_nom_camelCase()
    {
        var errors = new ProjectContent().Validate();

        Assert.Contains("slug", errors.Keys);
        Assert.Contains("title", errors.Keys);
        Assert.Contains("description", errors.Keys);
    }

    [Fact]
    public void Un_projet_sans_technologie_reste_valide()
    {
        var content = Valid();
        content.Techs = [];

        Assert.Empty(content.Validate());
    }

    [Fact]
    public void Une_technologie_vide_est_refusée()
    {
        var content = Valid();
        content.Techs = ["C#", "  "];

        // Elle s'afficherait comme une étiquette creuse sur la page Projets.
        Assert.Contains("techs", content.Validate().Keys);
    }

    [Fact]
    public void Une_technologie_trop_longue_est_refusée()
    {
        var content = Valid();
        content.Techs = [new string('x', Project.TechMaxLength + 1)];

        Assert.Contains("techs", content.Validate().Keys);
    }

    [Theory]
    [InlineData("githubUrl")]
    [InlineData("demoUrl")]
    public void Les_deux_liens_sont_validés_séparément(string field)
    {
        var content = Valid();
        if (field == "githubUrl") content.GithubUrl = "github.com/Theveninm";
        else content.DemoUrl = "github.com/Theveninm";

        var errors = content.Validate();

        Assert.Contains(field, errors.Keys);
        Assert.Single(errors);
    }

    [Fact]
    public void Les_liens_vides_deviennent_null_en_base()
    {
        var content = Valid();
        content.GithubUrl = "   ";
        content.DemoUrl = string.Empty;

        var project = content.ToNewProject();

        Assert.Null(project.GithubUrl);
        Assert.Null(project.DemoUrl);
    }

    [Fact]
    public void ApplyTo_recopie_tous_les_champs_éditables()
    {
        var project = Valid().ToNewProject();
        var modified = new ProjectContent
        {
            Slug = "site-personnel",
            Title = "Site personnel",
            Description = "Une autre description.",
            Techs = ["Angular"],
            DemoUrl = "https://mthevenin.fr/",
        };

        modified.ApplyTo(project);

        Assert.Equal("site-personnel", project.Slug);
        Assert.Equal("Site personnel", project.Title);
        Assert.Equal("Une autre description.", project.Description);
        Assert.Equal(["Angular"], project.Techs);
        Assert.Equal("https://mthevenin.fr/", project.DemoUrl);
        Assert.Null(project.GithubUrl);
    }
}
