namespace BimOpenFlow.Publishing.Tests;

public class VizBundleTests
{
    [Test]
    public void FindInRepo_ProbesUpward()
    {
        var root = Directory.CreateTempSubdirectory("bof-viz-test").FullName;
        try
        {
            var dist = Path.Combine(root, "bimopenflow", "web", "packages", "viz", "dist");
            Directory.CreateDirectory(dist);
            var bundlePath = Path.Combine(dist, "viz.iife.js");
            File.WriteAllText(bundlePath, "var BofViz = {};");

            var nested = Path.Combine(root, "src", "SomeProject", "bin");
            Directory.CreateDirectory(nested);

            Assert.That(VizBundle.FindInRepo(nested), Is.EqualTo(bundlePath));
            Assert.That(VizBundle.FromFile(bundlePath).Js, Is.EqualTo("var BofViz = {};"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void FindInRepo_ReturnsNullWhenAbsent()
    {
        var root = Directory.CreateTempSubdirectory("bof-viz-none").FullName;
        try
        {
            Assert.That(VizBundle.FindInRepo(root), Is.Null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
