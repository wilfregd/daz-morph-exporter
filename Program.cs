using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DazStudio.DazMorphExporter;

/// <summary>
/// DAZ Studio .duf files are a compressed "DSON" (Daz Scene Object Notation) which is similar to json.
/// Since the two are formatted similarly, the DSON data can be parsed just like a json file.
/// The .duf files only need to be uncompressed first to access the DSON data.
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        if(args.Length == 0)
        {
            Console.WriteLine("Please drag and drop a .duf file.");
            Console.ReadKey();
            return;
        }

        string filePath = args[0];
        string dsonRaw = ReadDufFile(filePath);

        if(!string.IsNullOrEmpty(dsonRaw))
        {   
            JObject dsonDoc = JObject.Parse(dsonRaw);
            ExportMorphData(filePath, dsonDoc);
        }

        Console.WriteLine("\n.duf morph extraction done.");
        Console.ReadKey();
    }

    private static void ExportMorphData(string filePath, JObject dsonDoc)
    {
        JToken[] nodes = (JToken[])dsonDoc.SelectTokens("scene.nodes[*]").ToArray();
        
        //Find which nodes are figures, store them as "id/token"
        Dictionary<string, JToken> figures = new Dictionary<string, JToken>();
        Dictionary<string, string> geometries = new Dictionary<string, string>();
        foreach(var node in nodes)
        {
            var type = node.SelectToken("preview.type");
            if(type != null && type.Value<string>() == "figure")
            {
                string id = node.SelectToken("id").Value<string>();

                figures.Add("#" + id, node);
                Console.WriteLine("Found figure: " + node.SelectToken("name"));

                //Also store each geometry since they can be referenced as parents for morphs
                JToken[] geoTokens = node.SelectTokens("geometries[*]").ToArray();
                Console.WriteLine($"Found {geoTokens.Length} geometries for figure '{id}'");
                foreach(var geo in geoTokens)
                {
                    string geoId = geo.SelectToken("id").Value<string>();
                    geometries.Add("#" + geoId, "#" + id);
                }
            }
        }

        //List all of the morphs and pair them with each figure
        //> Prepare all the lists to export later on
        Dictionary<string, JArray> figureMorphData = new Dictionary<string, JArray>();
        foreach(var figure in figures)
        {
            figureMorphData.Add(figure.Key, new JArray());
        }

        JToken[] modifiers = (JToken[])dsonDoc.SelectTokens("scene.modifiers[*]").ToArray();
        foreach(var modifier in modifiers)
        {
            string parent = modifier.SelectToken("parent").Value<string>();

            //If the figure is not in the list, it's possibly a geometry, we need to fetch it
            if(!figures.ContainsKey(parent))
            {
                if(geometries.TryGetValue(parent, out string parentId))
                {
                    parent = parentId;
                }
                else
                {
                    Console.WriteLine($"Unable to find parent '{parent}', skipping morph.");
                    continue;
                }
            }

            if(figures.TryGetValue(parent, out JToken parentNode))
            {
                string id = modifier.SelectToken("id").Value<string>();
                Console.WriteLine($"Found morph for figure '{parentNode.SelectToken("name")}': {id}");

                //Get the corresponding figureMorphData and append the morph informationss
                string url = modifier.SelectToken("url").Value<string>();
                url = url.Replace("%20", " ").Replace("%28", "(").Replace("%29", ")");

                //Not every modifier is a morph, some don't have a value and will be skipped
                var valueToken = modifier.SelectToken("channel.current_value");
                if(valueToken == null)
                {
                    continue;
                }

                float value = valueToken.Value<float>();

                JObject morphData = new JObject();
                morphData["id"] = id;
                morphData["url"] = url.Split('#')[0];
                morphData["value"] = value;

                JArray morphArr = figureMorphData[parent];
                morphArr.Add(morphData);
            }
        }
        
        //Write each figure morph data to a corresponding file
        Console.Write("\n");
        foreach(var data in figureMorphData)
        {
            string fileDir = Path.GetDirectoryName(filePath);
            string filename = $"{fileDir}/morphdata_{data.Key.Replace("#", string.Empty)}.json";
            File.WriteAllText(filename, data.Value.ToString());
            Console.WriteLine("Created morph data file: " + filename);
        }
    }

    private static string ReadDufFile(string sourceFilePath)
    {
        try
        {
            Console.WriteLine("Parsing .duf file: " + sourceFilePath);

            if(Path.GetExtension(sourceFilePath) != ".duf")
            {
                throw new Exception("File format is not .duf");
            }

            using (FileStream sourceFileStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read))
            using (MemoryStream memoryStream = new MemoryStream())
            using (GZipStream gzipStream = new GZipStream(sourceFileStream, CompressionMode.Decompress))
            {
                gzipStream.CopyTo(memoryStream);
                byte[] decompressedBytes = memoryStream.ToArray();
                return Encoding.UTF8.GetString(decompressedBytes);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading .duf file: {ex.Message}");
            return string.Empty;
        }
    }
}
