using Microsoft.EntityFrameworkCore;
using SiteVitrine.Api.Data;
using Testcontainers.PostgreSql;

namespace SiteVitrine.Api.Tests;

/// <summary>
/// Une vraie base PostgreSQL, jetable, pour les tests qui en ont besoin.
///
/// <b>Pourquoi pas un fournisseur en mémoire.</b> Les règles qui comptent ici sont portées par la
/// base : unicité du slug, contrainte de domaine sur la note, et surtout les tableaux natifs
/// <c>text[]</c> sur lesquels Npgsql mappe les <c>List&lt;string&gt;</c>. Un fournisseur en mémoire
/// n'en rejoue aucune — il laisserait passer précisément les bugs que ces tests cherchent.
///
/// Testcontainers démarre le conteneur au lancement de la collection et le supprime à la fin ;
/// Docker doit donc tourner pour exécuter ces tests. La même image que la production
/// (postgres:18-alpine) est utilisée, pour que ce qui passe ici passe là-bas.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // L'image se passe au constructeur depuis Testcontainers 4.x (WithImage() et le constructeur
    // sans paramètre sont obsolètes). La même qu'en production : ce qui passe ici passe là-bas.
    private readonly PostgreSqlContainer container =
        new PostgreSqlBuilder("postgres:18-alpine").Build();

    public async Task InitializeAsync()
    {
        await container.StartAsync();

        // Le schéma vient des migrations, pas d'EnsureCreated : on teste donc aussi qu'elles
        // s'appliquent sur une base neuve.
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// Un contexte neuf, à disposer par l'appelant. Un par test (voire plusieurs) : chacun a son
    /// propre cache de tracking, ce qui permet de vérifier qu'une écriture est bien allée en base
    /// plutôt que de relire l'objet gardé en mémoire.
    /// </summary>
    public AppDbContext CreateDbContext() =>
        new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(container.GetConnectionString())
                .Options);

    /// <summary>
    /// Vide les tables entre deux tests. Moins coûteux que recréer une base, et suffisant : aucun
    /// test ne modifie le schéma. RESTART IDENTITY remet aussi les séquences d'Id à zéro, pour que
    /// les tests ne dépendent pas de l'ordre dans lequel ils ont tourné.
    /// </summary>
    public async Task ResetAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE resources, projects RESTART IDENTITY");
    }

    public async Task DisposeAsync() => await container.DisposeAsync();
}

/// <summary>
/// Déclare la collection qui partage <see cref="PostgresFixture"/>. xUnit exécute en parallèle les
/// classes de test de collections différentes, mais sérialise celles d'une même collection : les
/// tests qui partagent la base ne se marchent donc pas dessus quand ils la vident.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
