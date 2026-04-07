using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Text.Json;

namespace Morpho25.IO
{
    /// <summary>
    /// Library material class.
    /// </summary>
    public class Library
    {
        public const string SOIL = "SOIL";
        public const string PROFILE = "PROFILE";
        public const string MATERIAL = "MATERIAL";
        public const string WALL = "WALL";
        public const string SOURCE = "SOURCE";
        public const string PLANT = "PLANT";
        public const string PLANT3D = "PLANT3D";
        public const string GREENING = "GREENING";

        public List<string> Code { get; private set; }
        public List<string> Description { get; private set; }
        public List<string> Detail { get; private set; }

        public Library(string file, string type, string keyword)
        {
            Code = new List<string>();
            Description = new List<string>();
            Detail = new List<string>();

            SetLibrary(file, type, keyword);
        }

        private void SetLibrary(string file, string type, string keyword)
        {
            if (!File.Exists(file))
                throw new FileNotFoundException($"{file} not found.");

            // Deduce if it's JSON or XML
            string firstLine = File.ReadLines(file).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
            bool isJson = firstLine != null && firstLine.TrimStart().StartsWith("{");

            string text = GetText(file, isJson);

            if (isJson)
            {
                LoadFromJson(text, type, keyword);
            }
            else
            {
                LoadFromXml(text, type, keyword);
            }
        }

        private string GetText(string file, bool isJson)
        {
            if (isJson)
            {
                return File.ReadAllText(file, Encoding.UTF8);
            }
            else
            {
                string characters = @"[^\s()_<>/,\.A-Za-z0-9=""]+";
                Encoding iso = Encoding.GetEncoding("ISO-8859-1");
                string text = File.ReadAllText(file, iso);
                return Regex.Replace(text, characters, "");
            }
        }

        private void LoadFromXml(string text, string type, string keyword)
        {
            // Legacy XML uses "Name" for Greening, "Description" for others
            string word = (type != GREENING) ? "Description" : "Name";

            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(text);
            XmlNodeList data = xmlDocument.DocumentElement.SelectNodes(type);

            if (data == null) return;

            var idContainer = new string[data.Count];
            var descriptionContainer = new string[data.Count];
            var dataContainer = new string[data.Count];

            Parallel.For(0, data.Count, i =>
            {
                var descriptionNode = data[i].SelectSingleNode(word);
                var idNode = data[i].SelectSingleNode("ID");

                if (descriptionNode == null || idNode == null) return;

                var description = descriptionNode.InnerText;
                if (keyword != null && !description.ToUpper().Contains(keyword.ToUpper())) return;

                dataContainer[i] = data[i].OuterXml;
                descriptionContainer[i] = description;
                idContainer[i] = idNode.InnerText.Replace(" ", "");
            });

            for (int i = 0; i < data.Count; i++)
            {
                if (idContainer[i] != null)
                {
                    Code.Add(idContainer[i]);
                    Description.Add(descriptionContainer[i]);
                    Detail.Add(dataContainer[i]);
                }
            }
        }

        private void LoadFromJson(string text, string type, string keyword)
        {
            using var doc = JsonDocument.Parse(text);
            var rootElement = doc.RootElement;
            
            if (!rootElement.TryGetProperty("envimetDatafile", out var dataFile))
                return;

            // Map standard plugin types to their corresponding JSON arrays
            // Using an array to allow combining multiple JSON sections into one plugin category
            string[] arrayNames;
            switch (type)
            {
                case SOIL: arrayNames = new[] { "soils" }; break;
                case PROFILE: arrayNames = new[] { "soilProfiles" }; break;
                case MATERIAL: arrayNames = new[] { "materials" }; break;
                case WALL: arrayNames = new[] { "walls" }; break;
                
                // Combine both water sources and emitters (pollutants) for JSON
                case SOURCE: arrayNames = new[] { "waterSources", "emitters" }; break; 
                
                case PLANT: arrayNames = new[] { "simplePlants" }; break;
                case PLANT3D: arrayNames = new[] { "plants3d" }; break;
                case GREENING: arrayNames = new[] { "greening" }; break;
                default: throw new ArgumentException("Unknown type: " + type);
            }

            // Loop through the mapped arrays (1 for most types, 2 for SOURCES)
            foreach (var arrayName in arrayNames)
            {
                if (!dataFile.TryGetProperty(arrayName, out var array)) continue;

                foreach (var item in array.EnumerateArray())
                {
                    // Everything in JSON uniformly uses "desc" and "id" now
                    if (!item.TryGetProperty("desc", out var descProp)) continue;
                    string desc = descProp.GetString();

                    if (keyword != null && !desc.ToUpper().Contains(keyword.ToUpper())) continue;
                    
                    if (!item.TryGetProperty("id", out var idProp)) continue;
                    string id = idProp.GetString()?.Replace(" ", "");

                    Code.Add(id);
                    Description.Add(desc);
                    Detail.Add(item.ToString()); // Stores the raw JSON segment for the detail property
                }
            }
        }
    }
}