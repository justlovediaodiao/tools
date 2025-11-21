if (args.Length != 3)
{
    Console.WriteLine("Usage: Ass <file> <millisecond>");
    return;
}

var file = args[1];
if (!File.Exists(file))
{
    Console.WriteLine($"file {file} not found");
    return;
}

if (!int.TryParse(args[2], out var millisecond))
{
    Console.WriteLine("millisecond must be an integer");
    return;
}

var ass = new Ass.Ass(file);
if (millisecond > 0)
{
    ass.Delay(millisecond);
}
else
{
    ass.Hurry(millisecond);
}
ass.Save();
