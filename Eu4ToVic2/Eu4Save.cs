﻿using Eu4Helper;
using PdxFile;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Eu4ToVic2
{
	public class Eu4Save : Eu4WorldBase
	{
		

		//public string ModPath { get; set; }
		public PdxSublist RootList { get; set; }

		internal List<Eu4DiploRelation> Relations { get; private set; }

		public string Version { get; set; }
		public string Date { get; private set; }

		public Eu4Save(string filePath, string modFilePath): base(modFilePath)
		{
			
			//ModPath = modFilePath;
			//LoadHistory();
			//LoadRegions();
			//LoadBuildingData();
			//LoadLocalisation();
			ReadSave(filePath);
			Date = RootList.KeyValuePairs["date"];
			PlayerTag = RootList.GetString("player");
			var versionSub = RootList.Sublists["savegame_version"];
			Version = $"{versionSub.FloatValues["first"].Single()}.{versionSub.FloatValues["second"].Single()}.0.0";
			//LoadCountryTags();
			LoadCountryData();
			LoadDiploRelations();
			//Console.WriteLine($"Average merc: {Countries.Where(c => c.Value.Exists).Sum(c => c.Value.Mercantilism) / Countries.Count}");
			LoadProvinceData();
			GreatestPower = Countries.OrderByDescending(c => c.Value.GreatPowerScore).First().Value;
			Console.WriteLine("The greatest power is " + GreatestPower.CountryTag);
			//LoadReligionData();
			//LoadCultureData();
			PostInitLoad();
			Console.WriteLine("EU4 data loaded.");
		}

		private void LoadHistory()
		{
			Console.WriteLine("Loading history..");
			CountryHistory = new Dictionary<string, PdxSublist>();
			var ctryHistory = GetFilesFor(@"history\countries");
			foreach (var ctry in ctryHistory)
			{
				CountryHistory.Add(Path.GetFileName(ctry).Substring(0, 3), PdxSublist.ReadFile(ctry));
			}

			//ProvinceHistory = new Dictionary<string, PdxSublist>();
			//var provHistory = GetFilesFor(@"history\provinces");
			//foreach (var prov in provHistory)
			//{
			//	ProvinceHistory.Add(Path.GetFileName(prov).Substring(0, 3), PdxSublist.ReadFile(prov));
			//}
		}

		private void LoadRegions()
		{
			Console.WriteLine("Loading EU4 areas..");
			var files = GetFilesFor("map");
			var areaFile = files.Find(f => Path.GetFileName(f) == "area.txt");
			var areas = PdxSublist.ReadFile(areaFile);
			Areas = new Dictionary<string, Eu4Area>();
			foreach (var ar in areas.Sublists)
			{
				//Areas[ar.Key] = new HashSet<int>(ar.Value.FloatValues.Values.SelectMany(f => f.Select(e => (int)e)));
				Areas[ar.Key] = new Eu4Area(ar.Key, ar.Value);
			}

			Console.WriteLine("Loading EU4 regions...");
			var regionFile = files.Find(f => Path.GetFileName(f) == "region.txt");
			var regions = PdxSublist.ReadFile(regionFile);
			Regions = new Dictionary<string, Eu4Region>();
			foreach(var reg in regions.Sublists)
			{
				Regions[reg.Key] = new Eu4Region(reg.Key, reg.Value, this);
			}

			Console.WriteLine("Loading EU4 areas..");
			
			var continentFile = files.Find(f => Path.GetFileName(f) == "continent.txt");
			var continents = PdxSublist.ReadFile(continentFile);
			Continents = new Dictionary<string, Eu4Continent>();
			foreach (var con in continents.Sublists)
			{
				//Areas[ar.Key] = new HashSet<int>(ar.Value.FloatValues.Values.SelectMany(f => f.Select(e => (int)e)));
				if (con.Key != "island_check_provinces")
				{
					Continents[con.Key] = new Eu4Continent(con.Key, con.Value);
				}
			}


		}

		private void LoadDiploRelations()
		{
			Console.WriteLine("Loading countries...");
			Relations = new List<Eu4DiploRelation>();
			var relations = RootList.GetSublist("diplomacy");
			relations.ForEachSublist(relation =>
			{
				if(Enum.GetNames(typeof(Relation)).Contains(relation.Key))
				{
					Relations.Add(new Eu4DiploRelation(relation.Value, this));
				}
			});
			//var distinctRelations = relations.Sublists.Keys.Distinct();
			//foreach (var dr in distinctRelations)
			//{
			//	Console.WriteLine(dr);
			//}
			//Console.WriteLine();
		}

		private void LoadLocalisation()
		{
			Console.WriteLine("Loading EU4 localisation...");
			Localisation = new Dictionary<string, string>();
			var files = GetFilesFor("localisation");
			foreach (var file in files)
			{
				if (Path.GetFileNameWithoutExtension(file).EndsWith("l_english"))
				{
					LoadLocalisationFile(file);
				}
			}
			
		}

		private void LoadLocalisationFile(string path)
		{
			using (var file = new StreamReader(path))
			{

				var first = file.ReadLine().Trim();
				if (first == "l_english:")
				{

					var key = new StringBuilder();
					var value = new StringBuilder();
					var readKey = true;
					var readValue = false;
					var inQuotes = false;
					while (!file.EndOfStream)
					{
						var ch = Convert.ToChar(file.Read());
						if (char.IsWhiteSpace(ch) && !inQuotes)
						{
							if (!readKey)
							{
								if (readValue)
								{
									Localisation[key.ToString()] = value.ToString();
									key = new StringBuilder();
									value = new StringBuilder();
									readValue = false;
									readKey = true;
								}
								else
								{
									readValue = true;
								}
							}

							continue;
						}
						if (ch == '"')
						{
							inQuotes = !inQuotes;
							continue;
						}
						if (ch == ':' && readKey && !inQuotes)
						{
							readKey = false;
							continue;
						}
						if (readKey)
						{
							key.Append(ch);
						}
						if (readValue)
						{
							value.Append(ch);
						}
					}

					//Console.WriteLine(Localisation);
				}
			}
		}

		private void LoadBuildingData()
		{
			Buildings = new List<string>();
			var buildFiles = GetFilesFor(@"common\buildings");
			foreach (var buildFile in buildFiles)
			{
				var buildings = PdxSublist.ReadFile(buildFile);
				Buildings.AddRange(buildings.Sublists.Keys);
			}
		}

		private void LoadCultureData()
		{
			Cultures = new Dictionary<string, Eu4Culture>();
			CultureGroups = new Dictionary<string, Eu4CultureGroup>();
			var relFiles = GetFilesFor(@"common\cultures");
			var ignores = new string[] {"male_names", "female_names", "dynasty_names" };
			foreach (var relFile in relFiles)
			{
				var cultures = PdxSublist.ReadFile(relFile);
				cultures.ForEachSublist(culGroup =>
				{
					if (!CultureGroups.ContainsKey(culGroup.Key))
					{
						CultureGroups[culGroup.Key] = new Eu4CultureGroup(culGroup.Value, this);
					}
					culGroup.Value.ForEachSublist(cul =>
					{
						if (!Cultures.ContainsKey(cul.Key) && ignores.All(ign => ign != cul.Key))
						{
							Cultures[cul.Key] = CultureGroups[culGroup.Key].AddCulture(cul.Value, this);
						}
					});

				});
			}
		}

		private void LoadReligionData()
		{
			Religions = new Dictionary<string, Eu4Religion>();
			ReligiousGroups = new Dictionary<string, Eu4ReligionGroup>();
			var relFiles = GetFilesFor(@"common\religions");
			var rgx = new Regex(@"\d+$");
			foreach (var relFile in relFiles)
			{
				var religions = PdxSublist.ReadFile(relFile);
				religions.ForEachSublist(relGroup =>
				{
					var key = rgx.Replace(relGroup.Key, string.Empty);
					if (!ReligiousGroups.ContainsKey(key))
					{
						ReligiousGroups[relGroup.Key] = new Eu4ReligionGroup(key, this);
					}
					relGroup.Value.ForEachSublist(rel =>
					{
						if (!Religions.ContainsKey(rel.Key) && rel.Key != "flag_emblem_index_range")
						{
							Religions[rel.Key] = ReligiousGroups[key].AddReligion(rel.Value, this);
						}
					});

				});
			}
			
		}

		private void LoadCountryData()
		{
			Console.WriteLine("Loading countries...");
			Countries = new Dictionary<string, Eu4CountryBase>();
			var countries = RootList.GetSublist("countries");
			countries.ForEachSublist(countryList =>
			{
				//if (!countryList.Value.KeyValuePairs.ContainsKey("primary_culture") || !countryList.Value.KeyValuePairs.ContainsKey("estimated_monthly_income") || float.Parse(countryList.Value.KeyValuePairs["estimated_monthly_income"]) <= 0.001)
				if (!countryList.Value.Sublists.ContainsKey("core_provinces"))
				{
					//country does not exist
					return;
				}
				var country = new Eu4Country(countryList.Value, this);
				Countries.Add(country.CountryTag, country);
			});
			Console.WriteLine($"Loaded {Countries.Count} countries.");
			Console.WriteLine($"{Countries.Count(c => c.Value.Exists)} countries exist.");
			Console.WriteLine($"{Countries.Count(c => c.Value.Institutions["enlightenment"]) } countries have embraced enlightenment.");
		}

		private void LoadProvinceData()
		{
			Console.WriteLine("Loading provinces...");
			Provinces = new Dictionary<int, Eu4ProvinceBase>();
			var provinces = RootList.GetSublist("provinces");
			provinces.ForEachSublist(provList =>
			{
				if (!provList.Value.KeyValuePairs.ContainsKey("culture"))
				{
					return;
				}
				var province = new Eu4Province(provList.Value, this);
				Provinces.Add(province.ProvinceID, province);
			});

			Console.WriteLine($"Loaded {Provinces.Count} provinces.");
		}

		//private void LoadCountryTags()
		//{
		//	Console.WriteLine("Loading list of countries...");
		//	var countryTagFiles = GetFilesFor(@"common\country_tags");
		//	CountryTags = new List<string>();
		//	foreach (var ctf in countryTagFiles)
		//	{
		//		var file = new StreamReader(ctf);
		//		string line;
		//		while ((line = file.ReadLine()) != null)
		//		{
		//			if (line.First() !='#' && line.Contains('='))
		//			{
		//				var tag = line.Substring(0, line.IndexOf('=')).Trim();
		//				CountryTags.Add(tag);
		//			}
		//		}
		//	}
		//
		//	var dynCountries = RootList.Sublists["dynamic_countries"].Values.Select(s => s.Substring(1, 3));
		//	CountryTags.AddRange(dynCountries);
		//	Console.WriteLine(CountryTags[0]);
		//}

		
		private void ReadSave(string filePath)
		{
			Console.WriteLine("Reading save file...");
			RootList = PdxSublist.ReadFile(filePath, "EU4txt");
			Console.WriteLine("Save reading complete.");
			//	
			//	var file = new StreamReader(filePath);
			//	var line = file.ReadLine();
			//	if (line != "EU4txt")
			//	{
			//		throw new Exception("Not an EU4 file");
			//	}
			//	RootList = new PdxSublist(null);
			//	var currentList = RootList;
			//	//var lineNumber = 0;
			//	while ((line = file.ReadLine()) != null)
			//	{
			//		//lineNumber++;
			//		currentList = PdxSublist.RunLine(line, currentList);
			//	}
			//	if(currentList != RootList)
			//	{
			//		throw new Exception("An unknown error occurred.");
			//	}
			//	
		}

		

		//private PdxSublist RunLine(string line, PdxSublist currentList)
		//{
		//	string key = null;
		//	var value = RemoveWhitespace(line.Substring(line.IndexOf('=') + 1));

		//	if (line.Contains('='))
		//	{
		//		key = RemoveWhitespace(line.Substring(0, line.IndexOf('=')));
		//	}
		//	else if (value == "}")
		//	{
		//		return currentList.Parent;

		//	}
		//	var parent = false;
		//	if (value.Contains('}'))
		//	{
		//		value = RemoveWhitespace(value.Substring(0, value.IndexOf('}')));
		//		parent = true;
		//	}

		//	if (value.FirstOrDefault() == '{')
		//	{
		//		var list = new PdxSublist(currentList, key);
		//		currentList.AddSublist(key, list);

		//		if (value.Contains('}'))
		//		{
		//			parent = false;
		//			value = value.Substring(1, value.IndexOf('}'));
		//			SingleLineArray(key, value, list);
		//		}
		//		else {
		//			currentList = list;
		//		}


		//	}
		//	else if (key == null && !value.Contains('"'))
		//	{
		//		// awkward single line array of numbers
		//		value = line.Substring(line.IndexOf('=') + 1).Trim();
		//		SingleLineArray(key, value, currentList);
		//	}
		//	else
		//	{
		//		currentList.AddString(key, value);
		//	}
		//	return parent ? currentList.Parent : currentList;
		//}


	}
}
