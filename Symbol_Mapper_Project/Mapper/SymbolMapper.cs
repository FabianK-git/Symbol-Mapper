using Symbol_Mapper_Project.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Unicode;
using Windows.Storage;
using Microsoft.Data.Sqlite;
using System.Collections;
using System.IO;
using System;
using System.Reflection;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using System.Linq;

namespace Symbol_Mapper_Project.Mapper
{
    internal static class SymbolMapper
    {
        public static IDictionary<string, string> symbolMap;

        private readonly static string db_path;

        static SymbolMapper()
        {
            symbolMap = new Dictionary<string, string>
            {
                { "~=", "≈" },
                { "!=", "≠" },
                { "!~", "≉" },
                { ">=", "⩾" },
                { "<=", "⩽" },

                { "delta", "Δ" },
                { "theta", "Θ" },
                { "ohm", "Ω" },
                
                { "->_right", "→" },
                { "=>_right", "⇒" },
                { "==>_right", "⟹" },

                { "->_left", "←" },
                { "=>_left", "⇐" },
                { "==>_left", "⟸" },

                { "->_up", "↑" },
                { "=>_up", "⇑" },

                { "->_down", "↓" },
                { "=>_down", "⇓" },

                { "natural_numbers", "ℕ" },
                { "integer_numbers", "ℤ" },
                { "rational_numbers", "ℚ" },
                { "real_numbers", "ℝ" },
                { "complex_numbers", "ℂ" },

                { ":=", "≔" },

                { "for_all", "∀" },

                { "plus_minus", "±" },

                { "infinity", "∞" },

                { "element_of", "∈" },
                { "no_element_of", "∉" },

                { "?!", "⁈" },

                { "checkmark", "✓" }
            };

            db_path = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Database", "characters.db");

            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
        }

        public static List<UnicodeData> MapStringToSymbol(string input)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            List<UnicodeData> search_result = new();

            using (SqliteConnection db = new($"Filename={db_path}"))
            {
                db.Open();

                string where_statement = string.Empty;

                foreach (string word in input.Split(" "))
                {
                    if (string.IsNullOrEmpty(where_statement))
                    {
                        where_statement += $" WHERE LOWER(Description) LIKE '%{word}%' "; ;
                    }
                    else
                    {
                        where_statement += $" AND LOWER(Description) LIKE '%{word}%' ";
                    }
                }
                
                SqliteCommand command = new($"SELECT CodePoint, Character, LOWER(Description) FROM characters {where_statement} LIMIT 100;", db);
                
                Debug.WriteLine(command.CommandText);
                
                SqliteDataReader query = command.ExecuteReader();

                while (query.Read())
                {
                    Debug.WriteLine($"{query.GetString(0)}, {query.GetString(1)}, {query.GetString(2)}");

                    string desciption = query.GetString(2);

                    // Include hex values if wanted
                    if (localSettings.Values["hex_values"] != null &&
                        (bool) localSettings.Values["hex_values"])
                    {
                        string code_point = query.GetString(0);

                        desciption += $" (0x{code_point})";
                    }

                    search_result.Add(new UnicodeData()
                    {
                        UnicodeCharacter = query.GetString(1),
                        Desciption = desciption
                    });
                }
            }

            return search_result;
        }

        public static void FetchUnicodeData()
        {
            ParallelLoopResult loop = Parallel.For(0, 11141110, (i) =>
            {
                string name = UnicodeInfo.GetName(i);

                if (name != null &&
                    !name.Contains("SURROGATE") &&
                    !name.ToLower().Contains("private use") &&
                    !name.ToLower().Contains("variation selector") &&
                    !UnicodeInfo.GetBlockName(i).Contains("Specials"))
                {
                    string item = char.ConvertFromUtf32(i);

                    name = name.Replace(" ", "_")
                               .Replace("-", "_")
                               .ToLower();

                    if (name.Contains("lamda"))
                    {
                        name = name.Replace("lamda", "lambda");
                    }
                    
                    // Add hex value of unicode char on end of name
                    name += $"_(0x{i:X4})";
                    
                    lock (symbolMap)
                    {
                        if (!symbolMap.ContainsKey(name))
                        {
                            symbolMap.Add(name, item);
                        }
                        else
                        {
                            Debug.WriteLine($"Already had: {name} with: {symbolMap[name]}, Current item: {item}");
                        }
                    }
                }
            });

            while (!loop.IsCompleted)
            {
                Task.Delay(100);
            }
        }
    }
}
