using Microsoft.EntityFrameworkCore;
using SiteVitrine.Api.Resources;

namespace SiteVitrine.Api.Data;

/// <summary>
/// Point d'entrée unique vers la base. Un DbContext EF Core, c'est deux choses à la fois :
///   - un <i>modèle</i> : la description des tables/colonnes (construit dans OnModelCreating) ;
///   - une <i>unité de travail</i> : il suit ("tracking") les entités chargées, et
///     <c>SaveChangesAsync()</c> génère les INSERT/UPDATE/DELETE correspondant aux modifications.
///
/// Il est enregistré en DI avec une durée de vie <c>Scoped</c> (= une instance par requête HTTP).
/// C'est voulu : un DbContext n'est pas thread-safe et son cache de tracking doit rester court.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Un <c>DbSet&lt;T&gt;</c> = une table interrogeable en LINQ. C'est aussi ce qui signale à EF
    /// que le type <c>Resource</c> fait partie du modèle.
    ///
    /// Extensibilité : chaque nouvelle feature (auth, messages, réservations…) ajoute ici sa ou ses
    /// lignes, et sa classe <c>IEntityTypeConfiguration&lt;T&gt;</c> dans son propre dossier.
    /// </summary>
    public DbSet<Resource> Resources => Set<Resource>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Plutôt que d'écrire ici la configuration de toutes les entités (ce fichier deviendrait un
        // fourre-tout), on scanne l'assembly : chaque classe implémentant IEntityTypeConfiguration<T>
        // est appliquée automatiquement. La config d'une entité vit donc à côté de son entité.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
