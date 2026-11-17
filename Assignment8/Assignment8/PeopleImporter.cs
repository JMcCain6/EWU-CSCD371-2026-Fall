using System.Globalization;
using System.IO;

namespace Assignment8;

public sealed class PeopleImporter : IDisposable
{
    private readonly TextReader _reader;
    private bool _disposed;
    private bool _imported;

    public PeopleImporter(TextReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public PeopleImporter(string filePath) 
        : this(new StreamReader(filePath ?? throw new ArgumentNullException(nameof(filePath))))
    {
    }

    public IReadOnlyList<Person> Import(ImportMode mode = ImportMode.FailFast,
        Action<InvalidPersonException>? onInvalid = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_imported)
        {
            throw new InvalidOperationException("This importer can only be read once.");
        }
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        _imported = true;
        var result = new List<Person>();
        string? line;
        int lineNumber = 0;
        while ((line = _reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (lineNumber == 1 && line.TrimStart('\uFEFF').Equals(
                "Id,FirstName,LastName,Email,StreetAddress,City,State,Zip", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            try
            {
                result.Add(ParsePerson(line));
            }
            catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
            {
                var failure = new InvalidPersonException(
                    $"Invalid person at CSV line {lineNumber}: {ex.Message}", lineNumber, line, ex);
                if (mode == ImportMode.FailFast)
                {
                    throw failure;
                }
                onInvalid?.Invoke(failure);
            }
        }
        return result;
    }

    public IReadOnlyList<Person> ImportPeople(ImportMode mode = ImportMode.FailFast,
        Action<InvalidPersonException>? onInvalid = null) => Import(mode, onInvalid);

    private static Person ParsePerson(string line)
    {
        string[] values = line.Split(',');
        if (values.Length != 8)
        {
            throw new FormatException($"Expected 8 columns but found {values.Length}.");
        }
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = values[i].Trim();
            if (values[i].Length == 0)
            {
                throw new FormatException($"Column {i + 1} is required.");
            }
        }
        int id = int.Parse(values[0], NumberStyles.None, CultureInfo.InvariantCulture);
        if (id <= 0)
        {
            throw new FormatException("Id must be positive.");
        }
        if (!values[3].Contains('@', StringComparison.Ordinal) ||
            values[3].StartsWith('@') || values[3].EndsWith('@'))
        {
            throw new FormatException("Email must contain a local part and domain.");
        }
        if (values[6].Length != 2 || !values[6].All(char.IsLetter))
        {
            throw new FormatException("State must be a two-letter abbreviation.");
        }
        if (values[7].Length != 5 || !values[7].All(char.IsDigit))
        {
            throw new FormatException("Zip must contain five digits.");
        }
        return new Person
        {
            Id = id,
            FirstName = values[1],
            LastName = values[2],
            Email = values[3],
            Address = new Address
            {
                StreetAddress = values[4],
                City = values[5],
                State = values[6],
                Zip = values[7]
            }
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _reader.Dispose();
    }
}
