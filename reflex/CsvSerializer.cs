using System.Reflection;
using System.Text;
using System.Text.Json;

namespace reflex;

public static class CsvSerializer
{
    public static string Serialize<T>(T value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var members = GetMembers(value.GetType());
        var headers = new List<string>();
        var row = new List<string>();

        foreach (var member in members)
        {
            headers.Add(member.Name);

            var memberValue = GetValue(member, value);
            var jsonValue = JsonSerializer.Serialize(memberValue, GetMemberType(member));
            row.Add(Escape(jsonValue));
        }

        return string.Join(",", headers.Select(Escape)) + "\r\n" + string.Join(",", row);
    }

    public static T Deserialize<T>(string csv) where T : new()
    {
        if (csv == null)
        {
            throw new ArgumentNullException(nameof(csv));
        }

        var records = ParseRecords(csv);

        if (records.Count < 2)
        {
            throw new FormatException("CSV должен содержать заголовок и значения.");
        }

        var headers = records[0];
        var values = records[1];

        if (headers.Count != values.Count)
        {
            throw new FormatException("Количество заголовков не совпадает с количеством значений.");
        }

        if (headers.Count != headers.Distinct().Count())
        {
            throw new FormatException("Названия колонок должны быть уникальными.");
        }

        var result = new T();
        var members = GetMembers(typeof(T)).ToDictionary(x => x.Name);

        for (var i = 0; i < headers.Count; i++)
        {
            if (!members.TryGetValue(headers[i], out var member))
            {
                continue;
            }

            var parsedValue = JsonSerializer.Deserialize(values[i], GetMemberType(member));
            SetValue(member, result, parsedValue);
        }

        return result;
    }

    public static void Save<T>(T value, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Путь не может быть пустым.", nameof(path));
        }

        File.WriteAllText(path, Serialize(value), Encoding.UTF8);
    }

    public static T Load<T>(string path) where T : new()
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Путь не может быть пустым.", nameof(path));
        }

        return Deserialize<T>(File.ReadAllText(path, Encoding.UTF8));
    }

    private static MemberInfo[] GetMembers(Type type)
    {
        return type
            .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(member => member is FieldInfo field
                ? !field.IsStatic && !field.IsInitOnly && !field.IsLiteral && !field.Name.StartsWith("<")
                : member is PropertyInfo property
                    && property.GetIndexParameters().Length == 0
                    && property.GetGetMethod(true) != null
                    && property.GetSetMethod(true) != null)
            .OrderBy(x => x.MetadataToken)
            .ToArray();
    }

    private static Type GetMemberType(MemberInfo member)
    {
        if (member is FieldInfo field)
        {
            return field.FieldType;
        }

        if (member is PropertyInfo property)
        {
            return property.PropertyType;
        }

        throw new InvalidOperationException("Неизвестный член класса.");
    }

    private static object? GetValue(MemberInfo member, object instance)
    {
        if (member is FieldInfo field)
        {
            return field.GetValue(instance);
        }

        if (member is PropertyInfo property)
        {
            return property.GetValue(instance);
        }

        throw new InvalidOperationException("Неизвестный член класса.");
    }

    private static void SetValue(MemberInfo member, object instance, object? value)
    {
        if (member is FieldInfo field)
        {
            field.SetValue(instance, value);
            return;
        }

        if (member is PropertyInfo property)
        {
            property.SetValue(instance, value);
            return;
        }

        throw new InvalidOperationException("Неизвестный член класса.");
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static List<List<string>> ParseRecords(string csv)
    {
        var records = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;
        var afterQuote = false;

        for (var i = 0; i < csv.Length; i++)
        {
            var ch = csv[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                        afterQuote = true;
                    }
                }
                else
                {
                    cell.Append(ch);
                }

                continue;
            }

            if (afterQuote)
            {
                if (ch == ',')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    afterQuote = false;
                    continue;
                }

                if (ch == '\r' || ch == '\n')
                {
                    row.Add(cell.ToString());
                    records.Add(row);
                    row = new List<string>();
                    cell.Clear();
                    afterQuote = false;

                    if (ch == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                    {
                        i++;
                    }

                    continue;
                }

                throw new FormatException("После закрывающей кавычки ожидалась запятая или перенос строки.");
            }

            if (ch == '"')
            {
                if (cell.Length == 0)
                {
                    inQuotes = true;
                }
                else
                {
                    throw new FormatException("Кавычка внутри ячейки без кавычек.");
                }
            }
            else if (ch == ',')
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if (ch == '\r' || ch == '\n')
            {
                row.Add(cell.ToString());
                records.Add(row);
                row = new List<string>();
                cell.Clear();

                if (ch == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                {
                    i++;
                }
            }
            else
            {
                cell.Append(ch);
            }
        }

        if (inQuotes)
        {
            throw new FormatException("CSV не закрыт кавычкой.");
        }

        if (cell.Length > 0 || row.Count > 0 || (csv.Length > 0 && csv[^1] == ','))
        {
            row.Add(cell.ToString());
            records.Add(row);
        }

        return records;
    }
}