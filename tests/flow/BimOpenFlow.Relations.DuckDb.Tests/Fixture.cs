using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.Utils;

namespace BimOpenFlow.Relations.DuckDb.Tests;

/// <summary>A temp folder holding walls.csv and levels.duckdb, registered as "files" and "db".</summary>
public sealed class Fixture : IDisposable
{
    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "bimopenflow-relations-tests", Guid.NewGuid().ToString("N"));
    public ConnectionRegistry Registry { get; }
    public DuckDbCatalog Catalog { get; }

    public Fixture()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "walls.csv"), "id,height,level_id,name\n1,2.5,10,A\n2,3.0,10,B\n3,2.1,20,C\n4,4.0,30,D\n");
        var db = Path.Combine(Folder, "levels.duckdb");
        using (var conn = BosDuckDb.Open(new FilePath(db)))
        {
            conn.Execute("CREATE TABLE levels (id BIGINT, name VARCHAR, built DATE)");
            conn.Execute("INSERT INTO levels VALUES (10, 'Ground', '2020-01-01'), (20, 'First', '2021-06-30')");
        }
        Registry = ConnectionRegistry.Of(("files", SourceType.FileRoot, Folder), ("db", SourceType.DuckDbFile, db));
        Catalog = new DuckDbCatalog(Registry);
    }

    public static Plan Walls => Plans.Csv("files", "walls.csv");
    public static Plan Levels => Plans.Table("db", "levels");

    public void Dispose()
    {
        try { Directory.Delete(Folder, recursive: true); }
        catch (IOException) { }
    }
}
