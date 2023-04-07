using Symbol_Mapper_Project.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Unicode;
using Windows.Storage;

namespace Symbol_Mapper_Project.Ligatures
{
    internal static class SymbolMapper
    {
        public static IDictionary<string, string> symbolMap;

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
        }
        
        public static List<UnicodeData> MapStringToSymbol(string input)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            List<UnicodeData> search_result = new();

            foreach (string key in symbolMap.Keys)
            {
                if (key.Contains('_'))
                {
                    bool contains = false;

                    foreach (string split in input.Split(" "))
                    {
                        contains = key.Contains(split);

                        if (contains == false) break;
                    }

                    if (contains)
                    {
                        if (search_result.Count < 100)
                        {
                            if (symbolMap.ContainsKey(key))
                            {
                                string description = key;

                                if (description.Contains("_(0x") &&
                                    localSettings.Values["hex_values"] != null &&
                                    ((bool) localSettings.Values["hex_values"]))
                                {
                                    description = description.Split("_(0x", 2)[0];
                                }

                                search_result.Add(new UnicodeData() 
                                {
                                    UnicodeCharacter = symbolMap[key],
                                    Desciption = description.Replace("_", " ")
                                });
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else
                {
                    if (key.Contains(input))
                    {
                        string description = key;

                        if (localSettings.Values["hex_values"] != null &&
                            ((bool) localSettings.Values["hex_values"]) &&
                            description.Contains("_(0x"))
                        {
                            description = description.Split("_(0x", 2)[0];
                        }

                        search_result.Add(new UnicodeData()
                        {
                            UnicodeCharacter = symbolMap[key],
                            Desciption = description.Replace("_", " ")
                        });
                    }
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
