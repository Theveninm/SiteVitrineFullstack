using Microsoft.EntityFrameworkCore;

namespace SiteVitrine.Api.Data;

/// <summary>
/// Mise à niveau du <b>schéma</b> uniquement — aucune donnée n'est touchée ici.
///
/// Cette opération ne tourne plus au démarrage de l'API mais dans un conteneur dédié
/// (<c>api-init</c> dans docker-compose.yml) qui l'exécute puis s'arrête. Deux bénéfices :
/// l'API ne démarre que sur un schéma déjà correct, et un échec de migration se voit comme un
/// conteneur en erreur plutôt que comme une API qui redémarre en boucle.
/// </summary>
public static class DatabaseMigrator
{
    public static async Task MigrateAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();

        if (pending.Count == 0)
        {
            logger.LogInformation("Schéma déjà à jour, rien à appliquer.");
            return;
        }

        logger.LogInformation(
            "Application de {Count} migration(s) : {Migrations}", pending.Count, string.Join(", ", pending));

        await db.Database.MigrateAsync(ct);

        logger.LogInformation("Schéma à jour.");
    }
}
