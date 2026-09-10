using SiteVitrine.Api.Content;
using SiteVitrine.Api.Data;

namespace SiteVitrine.Api.Admin;

/// <summary>
/// Les tâches d'administration en ligne de commande. Lancée avec un verbe, l'application exécute
/// la tâche puis s'arrête au lieu de servir des requêtes — c'est le mécanisme du conteneur
/// d'initialisation, et de fait le même code que celui appelé par le back-office
/// (cf. <see cref="AdminEndpoints"/>).
///
/// Le code de sortie est ce que Docker observe : 0 = succès (le conteneur est « completed »),
/// 1 = échec (la stack s'arrête là plutôt que de démarrer une API sur une base douteuse).
/// </summary>
public static class AdminCommands
{
    public static async Task<int> RunAsync(WebApplication app, string command, string[] args)
    {
        // Les services sont enregistrés en Scoped : hors requête HTTP, c'est à nous d'ouvrir le scope.
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Admin");

        try
        {
            switch (command)
            {
                case "migrate":
                    await DatabaseMigrator.MigrateAsync(db, logger);
                    return 0;

                case "import":
                {
                    var path = ContentFile.PathFrom(config);
                    logger.LogInformation("Import du contenu depuis {Path}…", Path.GetFullPath(path));

                    var file = await ContentFile.LoadAsync(path);
                    var report = await ContentImporter.ImportAsync(db, file, prune: args.Contains("--prune"));

                    logger.LogInformation("Import terminé : {Summary}", report.Summary);
                    if (report.OrphanSlugs.Count > 0)
                    {
                        // Signalé mais pas supprimé : voir --prune.
                        logger.LogWarning(
                            "Ressources en base absentes du fichier : {Slugs}", string.Join(", ", report.OrphanSlugs));
                    }

                    return 0;
                }

                case "export":
                {
                    var file = await ContentImporter.ExportAsync(db);
                    // Sur la sortie standard : `docker compose run --rm api export > content/resources.yaml`.
                    Console.Out.Write(file.ToYaml());
                    return 0;
                }

                default:
                    logger.LogError("Commande inconnue : {Command}. Attendu : migrate | import | export.", command);
                    return 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Échec de la commande {Command}.", command);
            return 1;
        }
    }
}
