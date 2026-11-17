using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Assignment8.Tests;

[TestClass]
public sealed class PeopleImporterTests
{
    private const string Header = "Id,FirstName,LastName,Email,StreetAddress,City,State,Zip";
    private const string Good = "1,Jane,Doe,jane@example.com,100 Main St,Spokane,WA,99201";
    private const string Bad = "oops,John,Doe,john@example.com,200 Main St,Spokane,WA,99201";

    [TestMethod]
    public void Import_ValidRows_MapsAllFieldsAndSkipsHeader()
    {
        using var importer = NewImporter(Header, Good);
        var people = importer.Import();
        Assert.AreEqual(1, people.Count);
        Assert.AreEqual(1, people[0].Id);
        Assert.AreEqual("Jane", people[0].FirstName);
        Assert.AreEqual("Doe", people[0].LastName);
        Assert.AreEqual("jane@example.com", people[0].Email);
        Assert.AreEqual("100 Main St", people[0].Address.StreetAddress);
        Assert.AreEqual("Spokane", people[0].Address.City);
        Assert.AreEqual("WA", people[0].Address.State);
        Assert.AreEqual("99201", people[0].Address.Zip);
    }

    [TestMethod]
    public void Import_FailFast_ThrowsWithLineRawAndInnerException()
    {
        using var importer = NewImporter(Header, Good, Bad);
        var exception = Assert.ThrowsExactly<InvalidPersonException>(() => importer.Import());
        Assert.AreEqual(3, exception.LineNumber);
        Assert.AreEqual(Bad, exception.RawLine);
        Assert.IsNotNull(exception.InnerException);
        StringAssert.Contains(exception.Message, "3");
    }

    [TestMethod]
    public void Import_SkipInvalid_ReportsFailuresAndContinues()
    {
        using var importer = NewImporter(Header, Bad, Good, "3,Invalid");
        var errors = new List<InvalidPersonException>();
        var people = importer.Import(ImportMode.SkipInvalid, errors.Add);
        Assert.AreEqual(1, people.Count);
        Assert.AreEqual(2, errors.Count);
        Assert.AreEqual(2, errors[0].LineNumber);
        Assert.AreEqual(4, errors[1].LineNumber);
    }

    [TestMethod]
    public void Import_SkipInvalid_WithoutCallback_StillReturnsValidRecords()
    {
        using var importer = NewImporter(Header, Bad, Good);
        Assert.AreEqual(1, importer.Import(ImportMode.SkipInvalid).Count);
    }

    [TestMethod]
    public void Import_ValidatesRequiredFieldsEmailStateAndZip()
    {
        string[] invalidRows =
        [
            "1,,Doe,jane@example.com,100 Main St,Spokane,WA,99201",
            "1,Jane,Doe,janeexample.com,100 Main St,Spokane,WA,99201",
            "1,Jane,Doe,jane@example.com,100 Main St,Spokane,Washington,99201",
            "1,Jane,Doe,jane@example.com,100 Main St,Spokane,WA,XYZ"
        ];
        foreach (string row in invalidRows)
        {
            using var importer = NewImporter(Header, row);
            Assert.ThrowsExactly<InvalidPersonException>(() => importer.Import());
        }
    }

    [TestMethod]
    public void Import_FilePath_ReadsRealTemporaryFile()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path, [Header, Good]);
            using var importer = new PeopleImporter(path);
            Assert.AreEqual(1, importer.Import().Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Dispose_DisposesOwnedReader_AndCanBeCalledTwice()
    {
        var reader = new TrackingReader(Good);
        var importer = new PeopleImporter(reader);
        importer.Dispose();
        importer.Dispose();
        Assert.IsTrue(reader.WasDisposed);
        Assert.ThrowsExactly<ObjectDisposedException>(() => importer.Import());
    }

    [TestMethod]
    public void Import_ReadFailure_IsNotMistakenForInvalidRow()
    {
        using var importer = new PeopleImporter(new ThrowingReader());
        Assert.ThrowsExactly<IOException>(() => importer.Import(ImportMode.SkipInvalid));
    }

    [TestMethod]
    public void Import_InvalidCallbackException_Propagates()
    {
        using var importer = NewImporter(Header, Bad);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            importer.Import(ImportMode.SkipInvalid, _ => throw new InvalidOperationException("callback")));
    }

    private static PeopleImporter NewImporter(params string[] lines) =>
        new(new StringReader(string.Join(Environment.NewLine, lines)));

    private sealed class TrackingReader(string content) : StringReader(content)
    {
        public bool WasDisposed { get; private set; }
        protected override void Dispose(bool disposing)
        {
            if (disposing) WasDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class ThrowingReader : TextReader
    {
        public override string? ReadLine() => throw new IOException("Simulated read error.");
    }
}
