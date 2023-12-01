# DAZ Morph exporter

This tool allows DAZ Studio users to export morph informations from a .duf file to separate json files. They can then be used in any external app if needed.

For now, the morph data includes the **Id**, **url**, **value** and **category/region** of each morph used in the scene and each figure has it's own file.

### To do:
- Implement handler for geografts

### Notes:
- Requires dotnet 7.0
- Libraries used: 
    - Json.NET - Newtonsoft