using System.Diagnostics;
using System.Text.Json;
using reflex;

internal static class Program
{
    private const int Iterations = 1_000;

    // Включаем публичные поля, чтобы JSON тоже содержал массив mas.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true
    };

    private static void Main()
    {
        var value = new F();

        // Сначала превращаем объект в строки для вывода.
        var csv = CsvSerializer.Serialize(value);
        var json = JsonSerializer.Serialize(value, JsonOptions);

        // Прогреваем сериализаторы перед замером сериализации.
        for (var i = 0; i < 100; i++)
        {
            _ = CsvSerializer.Serialize(value);
            _ = JsonSerializer.Serialize(value, JsonOptions);
        }

        // Замеряем сериализацию в обоих форматах.
        var csvSerialize = Measure(() => CsvSerializer.Serialize(value));
        var jsonSerialize = Measure(() => JsonSerializer.Serialize(value, JsonOptions));

        Console.WriteLine("CSV (рефлексия):");
        var outputTimer = Stopwatch.StartNew();
        Console.WriteLine(csv);
        Console.Out.Flush();
        outputTimer.Stop();

        Console.WriteLine("JSON:");
        Console.WriteLine(json);

        // Проверяем, что после загрузки CSV данные остались прежними.
        var restored = CsvSerializer.Deserialize<F>(csv);
        Console.WriteLine($"Данные после загрузки CSV совпадают: {csv == CsvSerializer.Serialize(restored)}");

        // Сохраняем CSV в файл и загружаем его обратно.
        CsvSerializer.Save(value, "data.csv");
        var fromFile = CsvSerializer.Load<F>("data.csv");
        Console.WriteLine($"Данные после чтения файла совпадают: {csv == CsvSerializer.Serialize(fromFile)}");

        Console.WriteLine($"\nЧисло повторений: {Iterations}");
        Console.WriteLine($"Сериализация CSV: {csvSerialize.TotalMilliseconds:F3} мс");
        Console.WriteLine($"Сериализация JSON: {jsonSerialize.TotalMilliseconds:F3} мс");
        Console.WriteLine($"Разница сериализации (CSV − JSON): {(csvSerialize - jsonSerialize).TotalMilliseconds:F3} мс");
        Console.WriteLine($"Вывод CSV в консоль: {outputTimer.Elapsed.TotalMilliseconds:F3} мс");
        Console.WriteLine("JSON содержит публичное поле mas, приватные поля i1–i5 сериализатор JSON не включает.");
    }

    // Выполняет действие Iterations раз и возвращает общее время.
    private static TimeSpan Measure<T>(Func<T> action)
    {
        var timer = Stopwatch.StartNew();

        for (var i = 0; i < Iterations; i++)
        {
            _ = action();
        }

        timer.Stop();
        return timer.Elapsed;
    }
}
