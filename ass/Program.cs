if (args is not [var inputFile, var offsetValue, var outputFile])
{
    Console.Error.WriteLine("Usage: ass <input.ass> <milliseconds> <output.ass>");
    return;
}

if (!File.Exists(inputFile))
{
    Console.Error.WriteLine($"Input file not found: {inputFile}");
    return;
}

if (!int.TryParse(offsetValue, out var milliseconds))
{
    Console.Error.WriteLine("Milliseconds must be an integer.");
    return;
}

try
{
    var subtitle = new Ass.Ass(inputFile);
    subtitle.Adjust(milliseconds);
    subtitle.Save(outputFile);
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
}
