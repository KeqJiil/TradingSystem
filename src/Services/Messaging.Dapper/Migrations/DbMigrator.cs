using System.Reflection;
using DbUp;
using DbUp.Engine;

namespace Messaging.Dapper.Migrations;

public sealed record MigrationScript(string Name, string Sql);

public static class DbMigrator
{
    public static void ApplyMigrations(string connectionString, Assembly serviceAssembly,
        IEnumerable<MigrationScript> libraryScripts)
    {
        EnsureDatabase.For.SqlDatabase(connectionString);

        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(serviceAssembly)
            .WithScripts(libraryScripts.Select(s => new SqlScript(s.Name, s.Sql)))
            .LogToConsole()
            .WithTransactionPerScript()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful) throw new InvalidOperationException("Database migration failed.", result.Error);
    }
}

internal static class EmbeddedMigration
{
    public static MigrationScript Read(string fileName)
    {
        var assembly = typeof(EmbeddedMigration).Assembly;
        var resource = assembly.GetManifestResourceNames().SingleOrDefault(n => n.EndsWith($".Migrations.{fileName}"))
                       ?? throw new InvalidOperationException($"Embedded migration '{fileName}' not found.");
        using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
        return new MigrationScript($"Messaging.Dapper.Migrations.{fileName}", reader.ReadToEnd());
    }
}
