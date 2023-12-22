using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// Define a class to hold the mapping between table name, column name, and types
public class TypeMapping
{
    public string TableName { get; set; }
    public string KeyColumnName { get; set; }
    public string ColumnName { get; set; }
    public string KeyColumnType { get; set; }
    public string ColumnType { get; set; }
}

class Program
{
    // Assume you have a list of mappings
    static List<TypeMapping> typeMappings = new List<TypeMapping>
    {
        // Add your mappings here
        new TypeMapping { TableName = "ProductFieldSelections", KeyColumnName = "ProductType", KeyColumnType = "nvarchar(2)", ColumnName = "Selections", ColumnType = "nvarchar(max)" },
        new TypeMapping { TableName = "AEPlanDefaults", KeyColumnName = "PlanName", KeyColumnType = "nvarchar(25)", ColumnName = "PlanDefaults", ColumnType = "nvarchar(max)" },
    };

    static void Main(string[] args)
    {
        // Check if a command-line argument is provided
        if (args.Length == 0)
        {
            Console.WriteLine("Please provide the path to the input file as a command-line argument.");
            return;
        }

        // Get the input file path from the command-line arguments
        string inputFilePath = args[0];

        // Read all lines from the input file
        string[] lines = File.ReadAllLines(inputFilePath);

        List<string> upList = new List<string>();
        List<string> downList = new List<string>();

        bool isUpMethod = false;
        bool isDownMethod = false;
        StringBuilder currentMethod = new StringBuilder();

        // Process each line to format the input
        for (int i = 0; i < lines.Length; i++)
        {
            // Combine lines until a complete entry is found
            string entry = lines[i].Trim();
            currentMethod.AppendLine(entry);

            // Check if the current method is an "Up" or "Down" method
            if (entry.Contains("protected override void Up("))
            {
                isUpMethod = true;
                isDownMethod = false;
            }
            else if (entry.Contains("protected override void Down("))
            {
                isUpMethod = false;
                isDownMethod = true;
            }

            // Check for the closing brace '}' to determine the end of a method
            if (entry.EndsWith("}"))
            {
                if (isUpMethod)
                {
                    upList.Add(currentMethod.ToString());
                    isUpMethod = false;
                }
                else if (isDownMethod)
                {
                    downList.Add(currentMethod.ToString());
                    isDownMethod = false;
                }

                // Reset for the next method
                currentMethod.Clear();
            }
        }

        // Join the formatted entries into a single string for "Up" and "Down" separately
        string formattedUpEntries = string.Join(Environment.NewLine, upList);
        string formattedDownEntries = string.Join(Environment.NewLine, downList);

        int currentIndex = 0;
        // Process entries
        ProcessEntries(formattedUpEntries, "Up", ref lines, ref currentIndex);
        ProcessEntries(formattedDownEntries, "Down", ref lines, ref currentIndex);

        List<string> finalLines = new List<string>();

        foreach (var line in lines)
        {
            if (line != "DELETE_ME")
            {
                finalLines.Add(line);
            }
        }

        // Write the modified content back to the input file
        File.WriteAllText(inputFilePath, string.Join(Environment.NewLine, finalLines.ToArray()));
    }

    static void ProcessEntries(string formattedEntries, string methodName, ref string[] lines, ref int currentIndex)
    {
        // Split the formatted entries based on "migrationBuilder"
        string[] entriesArray = formattedEntries.Split(new[] { "migrationBuilder" }, StringSplitOptions.RemoveEmptyEntries);

        // Process each entry separately
        foreach (string entry in entriesArray)
        {
            if (entry.Contains(".UpdateData"))
            {
                string finalEntry = "migrationBuilder" + entry.Trim();
                TransformInput(ref lines, finalEntry, methodName, currentIndex);
            }

            currentIndex = currentIndex + entry.Split('\n').Length - 1;
        }
    }

    static void TransformInput(ref string[] lines, string input, string methodName, int currentIndex)
    {
        // Extract values from the input using regular expressions
        string schema = ExtractValue(input, "schema");
        string table = ExtractValue(input, "table");
        string keyColumn = ExtractValue(input, "keyColumn");
        string keyValue = ExtractValue(input, "keyValue");
        string column = ExtractValue(input, "column");
        string value = ExtractValue(input, "value", methodName);

        // Look up keyColumnType and columnType based on the table name, key column name, and column name
        TypeMapping mapping = GetTypeMapping(table, keyColumn, column);
        string keyColumnType = mapping?.KeyColumnType ?? "UNKNOWN_KEY_COLUMN_TYPE";
        string columnType = mapping?.ColumnType ?? "UNKNOWN_COLUMN_TYPE";


        // Determine the indentation level
        int indentation = lines[currentIndex].IndexOf("migrationBuilder.UpdateData");

        // Generate the transformed output with proper indentation
        string output = $"{new string(' ', indentation)}migrationBuilder.UpdateData(" +
            $"{Environment.NewLine}{new string(' ', indentation + 4)}schema: \"{schema}\"," +
            $"{Environment.NewLine}{new string(' ', indentation + 4)}table: \"{table}\"," +
            $"{Environment.NewLine}{new string(' ', indentation + 4)}keyColumnTypes: new string[] {{ \"{keyColumnType}\" }}," +
            $"{Environment.NewLine}{new string(' ', indentation + 4)}keyColumns: new string[] {{ \"{keyColumn}\" }}," +
            $"{Environment.NewLine}{new string(' ', indentation + 4)}keyValues: new string[] {{ \"{keyValue}\" }}," +
            $"{Environment.NewLine}{new string(' ', indentation + 4)}columnTypes: new string[] {{ \"{columnType}\" }}," +
            $"{Environment.NewLine}{new string(' ', indentation + 4)}columns: new string[] {{ \"{column}\" }}," +
            $"{Environment.NewLine}{new string(' ', indentation + 4)}values: new object[] {{ {value} }});";

        var linesToDelete = input.Split('\n').Length;

        if (input[input.Length - 1] == '}')
        {
            linesToDelete--;
        }

        for (int i = 0; i < linesToDelete; i++)
        {
            lines[i + currentIndex] = "DELETE_ME";
        }

        // Replace the original input with the transformed output in the 'lines' array
        lines[currentIndex] = output;
    }


    static string ExtractValue(string input, string fieldName)
    {
        if (fieldName.Equals("value", StringComparison.OrdinalIgnoreCase))
        {
            string pattern = $@"{fieldName}:\s*([^)]+)";
            Regex regex = new Regex(pattern);
            Match match = regex.Match(input);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }
        else
        {
            string pattern = $@"{fieldName}:\s*\""(.*?)\""";
            Regex regex = new Regex(pattern);
            Match match = regex.Match(input);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        throw new ArgumentException($"Field '{fieldName}' not found in the input string.");
    }

    static string ExtractValue(string input, string fieldName, string methodName)
    {
        // If the method is "Down" and the field is "value", look for the value in the Down portion
        if (fieldName.Equals("value", StringComparison.OrdinalIgnoreCase))
        {
            string pattern = $@"{fieldName}:\s*([^)]+)";
            Regex regex = new Regex(pattern);
            Match match = regex.Match(input);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }
        else
        {
            // For other cases, use the existing ExtractValue function
            return ExtractValue(input, fieldName);
        }

        throw new ArgumentException($"Field '{fieldName}' not found in the input string.");
    }

    // Method to look up keyColumnType and columnType based on table name, key column name, and column name
    static TypeMapping GetTypeMapping(string tableName, string keyColumnName, string columnName)
    {
        return typeMappings.FirstOrDefault(mapping =>
            mapping.TableName == tableName &&
            mapping.KeyColumnName == keyColumnName &&
            mapping.ColumnName == columnName);
    }
}
